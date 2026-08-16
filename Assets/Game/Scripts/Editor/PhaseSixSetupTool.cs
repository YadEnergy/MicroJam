using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MicroJam.Game.Editor
{
    public static class PhaseSixSetupTool
    {
        public const string CampfirePrefabPath = "Assets/Game/Prefabs/World/Campfire.prefab";
        public const string UiInputReferenceFolder = "Assets/Game/ScriptableObjects/Settings/UIInput";
        public const float DefaultRefundPercent = 0.5f;
        public const float DefaultRegenerationDelay = 10f;
        public const float DefaultRegenerationPerSecond = 10f;
        public const int DefaultCampfireRepairCost = 20;
        public const float DefaultCampfireRepairPercent = 0.1f;

        [MenuItem("Tools/MicroJam/Phase 6/Build Interaction, Removal, Regeneration, and Repair")]
        public static void BuildFromMenu() => ApplyInteractionAndRepair(true);

        public static void ApplyInteractionAndRepair(bool logSuccess = true)
        {
            EnsureFolders();
            BuildingDefinition wall = ConfigureDefinition(PhaseFiveSetupTool.WallDefinitionPath);
            BuildingDefinition door = ConfigureDefinition(PhaseFiveSetupTool.DoorDefinitionPath);
            ConfigureBuildingPrefab(PhaseFiveSetupTool.WallPrefabPath, wall);
            ConfigureBuildingPrefab(PhaseFiveSetupTool.DoorPrefabPath, door);
            ConfigureCampfirePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ConfigureScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logSuccess)
            {
                Debug.Log("Phase 6 building removal/refunds, regeneration, and scene-bound Campfire interaction UI configured successfully.");
            }
        }

        public static void RunFromBatch()
        {
            ApplyInteractionAndRepair();
            PhaseOneValidator.ValidateFromBatch();
            PhaseTwoValidator.ValidateFromBatch();
            PhaseThreeValidator.ValidateFromBatch();
            PhaseFourValidator.ValidateFromBatch();
            PhaseFiveValidator.ValidateFromBatch();
            PhaseSixValidator.ValidateFromBatch();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(UiInputReferenceFolder))
            {
                Directory.CreateDirectory(UiInputReferenceFolder);
                AssetDatabase.Refresh();
            }
        }

        private static BuildingDefinition ConfigureDefinition(string path)
        {
            BuildingDefinition definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            if (definition == null || definition.Prefab == null)
            {
                throw new InvalidOperationException($"Phase 5 building definition is incomplete: {path}");
            }

            definition.Configure(
                definition.BuildingType,
                definition.DisplayName,
                definition.Prefab,
                definition.WoodCost,
                definition.FootprintSize,
                definition.BlocksPlayer,
                definition.BlocksDinosaur,
                definition.PlaceholderColor,
                DefaultRefundPercent);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigureBuildingPrefab(string path, BuildingDefinition definition)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException($"Could not load building prefab: {path}");
            }

            try
            {
                Health health = root.GetComponent<Health>();
                GridFootprint footprint = root.GetComponent<GridFootprint>();
                BuildingInstance instance = root.GetComponent<BuildingInstance>();
                if (health == null || footprint == null || instance == null)
                {
                    throw new InvalidOperationException($"{root.name} must retain Phase 5 Health, footprint, and BuildingInstance components.");
                }

                instance.Configure(definition, health, footprint);
                BuildingRegeneration regeneration = GetOrAdd<BuildingRegeneration>(root);
                regeneration.Configure(health, DefaultRegenerationDelay, DefaultRegenerationPerSecond);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureCampfirePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CampfirePrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException($"Could not load Campfire prefab: {CampfirePrefabPath}");
            }

            try
            {
                Health health = root.GetComponent<Health>();
                if (health == null)
                {
                    throw new InvalidOperationException("Campfire must retain its universal Health component.");
                }

                CampfireInteraction interaction = GetOrAdd<CampfireInteraction>(root);
                interaction.Configure(health, DefaultCampfireRepairCost, DefaultCampfireRepairPercent);
                BuildingRegeneration accidentalRegeneration = root.GetComponent<BuildingRegeneration>();
                if (accidentalRegeneration != null)
                {
                    UnityEngine.Object.DestroyImmediate(accidentalRegeneration);
                }

                PrefabUtility.SaveAsPrefabAsset(root, CampfirePrefabPath);
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
            Transform ui = game != null ? game.transform.Find("UI") : null;
            Transform player = game != null ? game.transform.Find("Actors/Player") : null;
            BuildingSystem buildingSystem = UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
            SquareGameplayViewport viewport = UnityEngine.Object.FindFirstObjectByType<SquareGameplayViewport>();
            PlayerResourceWallet wallet = player != null ? player.GetComponent<PlayerResourceWallet>() : null;
            CampfireInteraction campfire = UnityEngine.Object.FindFirstObjectByType<CampfireInteraction>();
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PhaseFiveSetupTool.InputActionsPath);
            if (ui == null || buildingSystem == null || viewport == null || wallet == null || campfire == null || input == null)
            {
                throw new InvalidOperationException("Prior scene phases and the updated Campfire prefab must be complete before configuring Phase 6.");
            }

            int uiLayer = LayerMask.NameToLayer("UI");
            GameObject interactionRoot = FindOrCreateRectChild(ui, "WorldInteraction", uiLayer);
            Stretch(interactionRoot.GetComponent<RectTransform>());
            Canvas canvas = GetOrAdd<Canvas>(interactionRoot);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = viewport.GetComponent<Camera>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = GetOrAdd<CanvasScaler>(interactionRoot);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024f, 1024f);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(interactionRoot);

            BuildingInteractionPopup buildingPopup = ConfigureBuildingPopup(interactionRoot.transform, uiLayer);
            CampfireInteractionPopup campfirePopup = ConfigureCampfirePopup(interactionRoot.transform, uiLayer);
            WorldInteractionController controller = GetOrAdd<WorldInteractionController>(interactionRoot);
            LayerMask interactionLayers = (1 << GameLayers.BuildingIndex) | (1 << GameLayers.DoorIndex);
            controller.Configure(input, buildingSystem, viewport, wallet, buildingPopup, campfirePopup, interactionLayers);

            ConfigureEventSystem(ui, input, uiLayer);

            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(scaler);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(buildingPopup);
            EditorUtility.SetDirty(campfirePopup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PhaseOneSetupTool.ScenePath);
        }

        private static BuildingInteractionPopup ConfigureBuildingPopup(Transform parent, int layer)
        {
            GameObject panel = ConfigurePanel(parent, "BuildingPopup", Vector2.zero, layer);
            Text title = ConfigureText(panel.transform, "Title", "Building", new Vector2(0f, 80f), new Vector2(244f, 32f), 22, FontStyle.Bold);
            Text health = ConfigureText(panel.transform, "Health", "HP: --", new Vector2(0f, 43f), new Vector2(244f, 28f), 17, FontStyle.Normal);
            Text prompt = ConfigureText(panel.transform, "RemovalPrompt", "Remove this building?", new Vector2(0f, 2f), new Vector2(244f, 50f), 16, FontStyle.Normal);
            Button remove = ConfigureButton(panel.transform, "RemoveButton", "Remove", new Vector2(-62f, -72f), new Color(0.72f, 0.2f, 0.16f), layer);
            Button close = ConfigureButton(panel.transform, "CloseButton", "Close", new Vector2(62f, -72f), new Color(0.26f, 0.3f, 0.36f), layer);
            BuildingInteractionPopup popup = GetOrAdd<BuildingInteractionPopup>(panel);
            popup.Configure(title, health, prompt, remove, close);
            panel.SetActive(false);
            return popup;
        }

        private static CampfireInteractionPopup ConfigureCampfirePopup(Transform parent, int layer)
        {
            GameObject panel = ConfigurePanel(parent, "CampfirePopup", Vector2.zero, layer);
            Text title = ConfigureText(panel.transform, "Title", "Campfire", new Vector2(0f, 80f), new Vector2(244f, 32f), 22, FontStyle.Bold);
            Text health = ConfigureText(panel.transform, "Health", "HP: --", new Vector2(0f, 43f), new Vector2(244f, 28f), 17, FontStyle.Normal);
            Text description = ConfigureText(panel.transform, "RepairDescription", "Repair: 20 Wood\n+10% Max HP", new Vector2(0f, 2f), new Vector2(244f, 50f), 16, FontStyle.Normal);
            Button repair = ConfigureButton(panel.transform, "RepairButton", "Repair", new Vector2(-62f, -72f), new Color(0.65f, 0.38f, 0.12f), layer);
            Button close = ConfigureButton(panel.transform, "CloseButton", "Close", new Vector2(62f, -72f), new Color(0.26f, 0.3f, 0.36f), layer);
            CampfireInteractionPopup popup = GetOrAdd<CampfireInteractionPopup>(panel);
            popup.Configure(title, health, description, repair, close);
            panel.SetActive(false);
            return popup;
        }

        private static GameObject ConfigurePanel(Transform parent, string name, Vector2 anchoredPosition, int layer)
        {
            GameObject panel = FindOrCreateRectChild(parent, name, layer);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(280f, 220f);
            Image image = GetOrAdd<Image>(panel);
            image.color = new Color(0.07f, 0.09f, 0.12f, 0.96f);
            image.raycastTarget = true;
            return panel;
        }

        private static Text ConfigureText(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, FontStyle style)
        {
            GameObject owner = FindOrCreateRectChild(parent, name, LayerMask.NameToLayer("UI"));
            RectTransform rect = owner.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = GetOrAdd<Text>(owner);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static Button ConfigureButton(Transform parent, string name, string label, Vector2 position, Color color, int layer)
        {
            GameObject owner = FindOrCreateRectChild(parent, name, layer);
            RectTransform rect = owner.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(112f, 38f);
            Image image = GetOrAdd<Image>(owner);
            image.color = color;
            image.raycastTarget = true;
            Button button = GetOrAdd<Button>(owner);
            button.targetGraphic = image;
            Text buttonText = ConfigureText(owner.transform, "Label", label, Vector2.zero, rect.sizeDelta, 16, FontStyle.Bold);
            Stretch(buttonText.rectTransform);
            return button;
        }

        private static void ConfigureEventSystem(Transform ui, InputActionAsset input, int layer)
        {
            EventSystem[] existingSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem selected = ui.Find("EventSystem")?.GetComponent<EventSystem>();
            if (selected == null && existingSystems.Length > 0)
            {
                selected = existingSystems[0];
                selected.gameObject.name = "EventSystem";
                selected.transform.SetParent(ui, false);
            }

            for (int i = 0; i < existingSystems.Length; i++)
            {
                if (existingSystems[i] != selected)
                {
                    UnityEngine.Object.DestroyImmediate(existingSystems[i].gameObject);
                }
            }

            GameObject owner = selected != null ? selected.gameObject : FindOrCreateChild(ui, "EventSystem", layer);
            owner.layer = layer;
            ResetTransform(owner.transform);
            GetOrAdd<EventSystem>(owner);
            InputSystemUIInputModule module = GetOrAdd<InputSystemUIInputModule>(owner);
            module.actionsAsset = input;
            module.point = EnsureInputReference(input, "UI/Point");
            module.leftClick = EnsureInputReference(input, "UI/Click");
            module.rightClick = EnsureInputReference(input, "UI/RightClick");
            module.middleClick = EnsureInputReference(input, "UI/MiddleClick");
            module.scrollWheel = EnsureInputReference(input, "UI/ScrollWheel");
            module.move = EnsureInputReference(input, "UI/Navigate");
            module.submit = EnsureInputReference(input, "UI/Submit");
            module.cancel = EnsureInputReference(input, "UI/Cancel");
            EditorUtility.SetDirty(module);
        }

        private static InputActionReference EnsureInputReference(InputActionAsset asset, string actionPath)
        {
            InputAction action = asset.FindAction(actionPath, true);
            string safeName = actionPath.Replace('/', '_');
            string path = $"{UiInputReferenceFolder}/{safeName}.asset";
            InputActionReference reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference == null)
            {
                reference = InputActionReference.Create(action);
                AssetDatabase.CreateAsset(reference, path);
            }
            else
            {
                reference.Set(action);
            }

            EditorUtility.SetDirty(reference);
            return reference;
        }

        private static GameObject FindOrCreateChild(Transform parent, string name, int layer)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.layer = layer;
                ResetTransform(existing);
                return existing.gameObject;
            }

            GameObject child = new(name) { layer = layer };
            child.transform.SetParent(parent, false);
            return child;
        }

        private static GameObject FindOrCreateRectChild(Transform parent, string name, int layer)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                if (existing is not RectTransform)
                {
                    throw new InvalidOperationException($"UI object must use RectTransform: {name}");
                }

                existing.gameObject.layer = layer;
                ResetTransform(existing);
                return existing.gameObject;
            }

            GameObject child = new(name, typeof(RectTransform)) { layer = layer };
            child.transform.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void ResetTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }
    }
}
