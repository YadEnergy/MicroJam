using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace MicroJam.Game.Editor
{
    public static class DinosaurNavigationSetupTool
    {
        public static readonly string[] DinosaurPrefabPaths =
        {
            "Assets/Game/Prefabs/Enemies/Dinosaur A.prefab",
            "Assets/Game/Prefabs/Enemies/Dinosaur B.prefab",
            "Assets/Game/Prefabs/Enemies/Dinosaur C.prefab"
        };

        [MenuItem("Tools/MicroJam/Dinosaurs/Restore Buildings and Configure Navigation")]
        public static void BuildFromMenu() => Apply(true);

        public static void Apply(bool logSuccess = true)
        {
            // These idempotent setup methods restore only the two known merge-damaged phases.
            PhaseFiveSetupTool.ApplyBuildingSystem(false);
            PhaseSixSetupTool.ApplyInteractionAndRepair(false);
            foreach (string path in DinosaurPrefabPaths) ConfigureDinosaurPrefab(path);
            ConfigureScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logSuccess)
            {
                Debug.Log("Building interaction recovery and Dinosaur grid navigation configured without changing wave bank assets or DinosaurSpawner selection logic.");
            }
        }

        public static void RunFromBatch()
        {
            Apply();
            DinosaurNavigationValidator.ValidateFromBatch();
        }

        public static void ImportTmpEssentialsFromBatch()
        {
            string settings = Path.Combine(Application.dataPath, "TextMesh Pro", "Resources", "TMP Settings.asset");
            if (File.Exists(settings))
            {
                EditorApplication.Exit(0);
                return;
            }

            UnityEditor.PackageManager.PackageInfo info =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string package = info != null
                ? Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage")
                : string.Empty;
            if (!File.Exists(package))
            {
                Debug.LogError($"TMP Essential Resources package was not found: {package}");
                EditorApplication.Exit(1);
                return;
            }

            AssetDatabase.importPackageCompleted += OnTmpImportCompleted;
            AssetDatabase.importPackageFailed += OnTmpImportFailed;
            AssetDatabase.ImportPackage(package, false);
        }

        private static void OnTmpImportCompleted(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImportCompleted;
            AssetDatabase.importPackageFailed -= OnTmpImportFailed;
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Debug.Log($"TMP package import completed: {packageName}");
            EditorApplication.Exit(0);
        }

        private static void OnTmpImportFailed(string packageName, string error)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImportCompleted;
            AssetDatabase.importPackageFailed -= OnTmpImportFailed;
            Debug.LogError($"TMP package import failed ({packageName}): {error}");
            EditorApplication.Exit(1);
        }

        private static void ConfigureDinosaurPrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) throw new InvalidOperationException($"Missing teammate Dinosaur prefab: {path}");

            try
            {
                Health health = Require<Health>(root, path);
                Rigidbody2D body = Require<Rigidbody2D>(root, path);
                DinosaurAgent agent = Require<DinosaurAgent>(root, path);
                DinosaurMovement movement = Require<DinosaurMovement>(root, path);
                DinosaurAttack attack = Require<DinosaurAttack>(root, path);
                DinosaurTargeting targeting = Require<DinosaurTargeting>(root, path);
                SpriteRenderer visual = root.transform.Find("Visual")?.GetComponent<SpriteRenderer>();
                Transform origin = root.transform.Find("Combat/AttackOrigin");
                SpriteRenderer feedback = root.transform.Find("Combat/AttackOrigin/AttackVisual")?.GetComponent<SpriteRenderer>();
                if (visual == null || origin == null || feedback == null)
                {
                    throw new InvalidOperationException($"{path} lost its prefab-authored visual or attack feedback hierarchy.");
                }

                movement.Configure(body, visual);
                attack.Configure(body, visual, origin, feedback);
                targeting.Configure(health, movement, attack);
                agent.Configure(health, movement, attack, targeting);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureScene()
        {
            Scene scene = EditorSceneManager.OpenScene(PhaseOneSetupTool.ScenePath, OpenSceneMode.Single);
            GameObject game = GameObject.Find("Game");
            Transform systems = game != null ? game.transform.Find("Systems") : null;
            WorldGridService grid = UnityEngine.Object.FindFirstObjectByType<WorldGridService>();
            GridOccupancyService occupancy = UnityEngine.Object.FindFirstObjectByType<GridOccupancyService>();
            if (systems == null || grid == null || occupancy == null)
            {
                throw new InvalidOperationException("The scene must retain Systems, WorldGridService, and GridOccupancyService.");
            }

            Transform existing = systems.Find("DinosaurNavigation");
            GameObject owner = existing != null ? existing.gameObject : new GameObject("DinosaurNavigation");
            owner.transform.SetParent(systems, false);
            DinosaurNavigationGrid navigation = owner.GetComponent<DinosaurNavigationGrid>();
            if (navigation == null) navigation = owner.AddComponent<DinosaurNavigationGrid>();
            navigation.Configure(grid, occupancy);
            ConfigureResponsiveScreenUi(game, UnityEngine.Object.FindFirstObjectByType<SquareGameplayViewport>());
            EditorUtility.SetDirty(navigation);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PhaseOneSetupTool.ScenePath);
        }

        private static void ConfigureResponsiveScreenUi(GameObject game, SquareGameplayViewport viewport)
        {
            if (game == null || viewport == null)
            {
                throw new InvalidOperationException("Responsive HUD setup requires the Game root and square gameplay viewport.");
            }

            Transform ui = game.transform.Find("UI");
            Transform hudRoot = ui != null ? ui.Find("Canvas") : null;
            Transform interactionRoot = ui != null ? ui.Find("WorldInteraction") : null;
            Camera gameplayCamera = viewport.GetComponent<Camera>();
            if (hudRoot == null || interactionRoot == null || gameplayCamera == null)
            {
                throw new InvalidOperationException("The existing HUD Canvas, WorldInteraction Canvas, and gameplay Camera must be retained.");
            }

            Canvas hudCanvas = Require<Canvas>(hudRoot.gameObject, "Game/UI/Canvas");
            CanvasScaler hudScaler = Require<CanvasScaler>(hudRoot.gameObject, "Game/UI/Canvas");
            gameplayCamera.rect = new Rect(0f, 0f, 1f, 1f);
            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hudCanvas.worldCamera = gameplayCamera;
            hudCanvas.overrideSorting = true;
            hudCanvas.sortingOrder = 200;
            ConfigureScaler(hudScaler);

            ConfigureHudText(hudRoot.Find("DayNightUI/DayNightText"),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(260f, 56f),
                TextAlignmentOptions.TopLeft, 18f, 32f);
            ConfigureHudText(hudRoot.Find("WaveInfo/WaveInfoText"),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(240f, 56f),
                TextAlignmentOptions.Top, 18f, 32f);
            ConfigureHudText(hudRoot.Find("PointsInfo/PointsInfoUI"),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(260f, 56f),
                TextAlignmentOptions.TopRight, 18f, 32f);
            ConfigureHudText(hudRoot.Find("EndedInfo/EndedInfoText"),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 140f),
                TextAlignmentOptions.Center, 20f, 48f);

            Canvas interactionCanvas = Require<Canvas>(interactionRoot.gameObject, "Game/UI/WorldInteraction");
            CanvasScaler interactionScaler = Require<CanvasScaler>(interactionRoot.gameObject, "Game/UI/WorldInteraction");
            interactionCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            interactionCanvas.worldCamera = gameplayCamera;
            interactionCanvas.overrideSorting = true;
            interactionCanvas.sortingOrder = 100;
            ConfigureScaler(interactionScaler);
            CenterPopup(interactionRoot.Find("BuildingPopup"));
            CenterPopup(interactionRoot.Find("CampfirePopup"));

            EditorUtility.SetDirty(hudCanvas);
            EditorUtility.SetDirty(hudScaler);
            EditorUtility.SetDirty(interactionCanvas);
            EditorUtility.SetDirty(interactionScaler);
        }

        private static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(683f, 683f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void ConfigureHudText(
            Transform target,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            TextAlignmentOptions alignment,
            float minimumFontSize,
            float maximumFontSize)
        {
            if (target == null) throw new InvalidOperationException("A teammate HUD text object is missing from the scene.");
            RectTransform rect = Require<RectTransform>(target.gameObject, target.name);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TMP_Text text = Require<TMP_Text>(target.gameObject, target.name);
            text.enableAutoSizing = true;
            text.fontSizeMin = minimumFontSize;
            text.fontSizeMax = maximumFontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            EditorUtility.SetDirty(rect);
            EditorUtility.SetDirty(text);
        }

        private static void CenterPopup(Transform popup)
        {
            if (popup == null) throw new InvalidOperationException("A scene-authored interaction popup is missing.");
            RectTransform rect = Require<RectTransform>(popup.gameObject, popup.name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(280f, 220f);
            EditorUtility.SetDirty(rect);
        }

        private static T Require<T>(GameObject root, string path) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null) throw new InvalidOperationException($"{path} must retain {typeof(T).Name}; runtime component creation is forbidden.");
            return component;
        }
    }
}
