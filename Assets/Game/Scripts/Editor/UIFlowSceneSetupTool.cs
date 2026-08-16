using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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
    public static class UIFlowSceneSetupTool
    {
        public const string GameScenePath = "Assets/Game/Scenes/Game.unity";
        public const string MainMenuScenePath = "Assets/Game/Scenes/SampleScene.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private static readonly Vector2 ReferenceResolution = new(1024f, 1024f);

        [MenuItem("Tools/MicroJam/UI Flow/Apply Scene-Bound UI")]
        public static void ApplyFromMenu() => Apply(true);

        [MenuItem("Tools/MicroJam/UI Flow/Validate Scene-Bound UI")]
        public static void ValidateFromMenu() => ValidateScenes();

        public static void RunFromBatch()
        {
            Apply(true);
            ValidateScenes();
        }

        public static void Apply(bool logSuccess)
        {
            SetupGameScene();
            SetupMainMenuScene();
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            if (logSuccess) Debug.Log("Scene-bound UI flow setup completed for Game and Main Menu scenes.");
        }

        public static void ValidateScenes()
        {
            List<string> failures = new();
            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Require(game.IsValid(), "Game scene failed to open.", failures);
            Require(FindInScene<PauseMenuController>(game) != null, "Game PauseMenuController is missing.", failures);
            Require(FindInScene<PlayerRespawnController>(game) != null, "Game PlayerRespawnController is missing.", failures);
            Require(FindNamed(game, "PauseMenu") != null, "Scene-bound PauseMenu is missing.", failures);
            Require(FindNamed(game, "DeathOverlay") != null, "Scene-bound DeathOverlay is missing.", failures);
            Require(FindNamed(game, "SceneTransitionOverlay") != null, "Game transition overlay is missing.", failures);
            BuildHotbarHintsUI hotbar = FindInScene<BuildHotbarHintsUI>(game);
            Require(hotbar != null && hotbar.WallSlot != null && hotbar.DoorSlot != null &&
                    hotbar.BowTowerSlot != null && hotbar.StoneTowerSlot != null,
                "Four-slot Build toolbar is not fully wired.", failures);
            Canvas gameplayCanvas = hotbar != null ? hotbar.GetComponentInParent<Canvas>() : null;
            Require(gameplayCanvas != null && gameplayCanvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                    gameplayCanvas.overrideSorting && gameplayCanvas.sortingOrder >= 200,
                "Gameplay UI Canvas must render as a top-level Screen Space Overlay.", failures);
            Camera gameplayCamera = FindInScene<SquareGameplayViewport>(game)?.GetComponent<Camera>();
            Require(gameplayCamera != null && gameplayCamera.rect == new Rect(0f, 0f, 1f, 1f),
                "Gameplay Camera must use the full 1024x1024 viewport.", failures);
            CanvasGroup gameTransition = FindNamed(game, "SceneTransitionOverlay")?.GetComponent<CanvasGroup>();
            Require(gameTransition != null && Mathf.Approximately(gameTransition.alpha, 0f) && !gameTransition.blocksRaycasts,
                "Gameplay transition overlay must remain transparent and editable outside Play Mode.", failures);
            Require(FindNamed(game, "BuildSlot4_StoneTower") != null, "Stone Tower toolbar slot is missing.", failures);
            Require(FindObjectsInScene<CanvasScaler>(game).All(IsResponsiveScaler), "A gameplay CanvasScaler is not standardized.", failures);

            Scene menu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Require(FindInScene<MainMenuController>(menu) != null, "Main Menu controller is missing.", failures);
            Require(FindNamed(menu, "MainMenuRoot") != null, "MainMenuRoot is missing.", failures);
            Require(FindNamed(menu, "Title")?.GetComponent<TMP_Text>()?.text == "Cogito Ergo Sum", "Main Menu title is incorrect.", failures);
            Require(FindNamed(menu, "PlayButton")?.GetComponent<Button>() != null, "Play button is missing.", failures);
            Require(FindNamed(menu, "ExitButton")?.GetComponent<Button>() != null, "Exit button is missing.", failures);
            Require(FindNamed(menu, "SceneTransitionOverlay") != null, "Main Menu transition overlay is missing.", failures);
            CanvasGroup menuTransition = FindNamed(menu, "SceneTransitionOverlay")?.GetComponent<CanvasGroup>();
            Require(menuTransition != null && Mathf.Approximately(menuTransition.alpha, 0f) && !menuTransition.blocksRaycasts,
                "Main Menu transition overlay must remain transparent and editable outside Play Mode.", failures);
            Require(FindInScene<EventSystem>(menu) != null, "Main Menu EventSystem is missing.", failures);
            Require(FindObjectsInScene<CanvasScaler>(menu).All(IsResponsiveScaler), "Main Menu CanvasScaler is not responsive.", failures);

            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
            Debug.Log("UI flow scene validation passed.");
        }

        private static void SetupGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            BuildHotbarHintsUI hotbar = FindInScene<BuildHotbarHintsUI>(scene);
            BuildingSystem buildingSystem = FindInScene<BuildingSystem>(scene);
            WorldInteractionController interactions = FindInScene<WorldInteractionController>(scene);
            if (hotbar == null || buildingSystem == null || interactions == null)
                throw new InvalidOperationException("Game scene requires the existing toolbar, BuildingSystem, and WorldInteractionController.");

            Canvas canvas = hotbar.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Build toolbar is not under a Canvas.");
            SquareGameplayViewport viewport = FindInScene<SquareGameplayViewport>(scene);
            if (viewport != null) viewport.GetComponent<Camera>().rect = new Rect(0f, 0f, 1f, 1f);
            ConfigureOverlayCanvas(canvas, 200);
            Canvas interactionCanvas = interactions.GetComponentInParent<Canvas>();
            if (interactionCanvas != null && interactionCanvas != canvas) ConfigureOverlayCanvas(interactionCanvas, 100);
            foreach (CanvasScaler scaler in FindObjectsInScene<CanvasScaler>(scene)) ConfigureScaler(scaler);

            RectTransform hotbarRect = hotbar.transform as RectTransform;
            ConfigureRect(hotbarRect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(316f, 104f));
            Image wall = EnsureToolbarSlot(hotbarRect, "BuildSlot1_Wall", 0, "1", "WALL", new Color(0.42f, 0.22f, 0.08f, 1f));
            Image door = EnsureToolbarSlot(hotbarRect, "BuildSlot2_Door", 1, "2", "DOOR", new Color(0.24f, 0.43f, 0.55f, 1f));
            Image bow = EnsureToolbarSlot(hotbarRect, "BuildSlot3_BowTower", 2, "3", "BOW", new Color(0.28f, 0.42f, 0.25f, 1f));
            Image stone = EnsureToolbarSlot(hotbarRect, "BuildSlot4_StoneTower", 3, "4", "STONE", new Color(0.38f, 0.4f, 0.46f, 1f));
            GameObject legacy = FindDirectChild(hotbarRect, "BuildSlot3_Turret");
            if (legacy != null && legacy != bow.gameObject) UnityEngine.Object.DestroyImmediate(legacy);
            hotbar.Configure(buildingSystem, wall, door, bow, stone);

            SceneTransitionController transition = EnsureTransitionOverlay(canvas.transform);
            ConfigureExistingPopups(scene, transition);

            RectTransform pauseRoot = EnsureFullScreenPanel(canvas.transform, "PauseMenu", new Color(0f, 0f, 0f, 0.72f));
            RectTransform pauseContent = EnsurePanelContent(pauseRoot, "PauseContent", new Vector2(360f, 350f));
            EnsureText(pauseContent, "PauseTitle", "PAUSED", 40f, new Vector2(0f, 112f), new Vector2(300f, 70f));
            Button resume = EnsureButton(pauseContent, "ResumeButton", "Resume", new Vector2(0f, 38f));
            Button restart = EnsureButton(pauseContent, "RestartButton", "Restart", new Vector2(0f, -42f));
            Button mainMenu = EnsureButton(pauseContent, "MainMenuButton", "Main Menu", new Vector2(0f, -122f));
            UIPanelTween pauseTween = EnsurePanelTween(pauseRoot.gameObject, pauseContent, true);
            pauseRoot.gameObject.SetActive(false);

            RectTransform deathRoot = EnsureFullScreenPanel(canvas.transform, "DeathOverlay", new Color(0f, 0f, 0f, 0.65f));
            RectTransform deathContent = EnsureRectChild(deathRoot, "DeathContent");
            ConfigureRect(deathContent, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(480f, 220f));
            TMP_Text status = EnsureText(deathContent, "RespawnStatus", "RESPAWNING", 38f, new Vector2(0f, 48f), new Vector2(460f, 70f));
            TMP_Text countdown = EnsureText(deathContent, "RespawnCountdown", "10", 64f, new Vector2(0f, -42f), new Vector2(240f, 100f));
            UIPanelTween deathTween = EnsurePanelTween(deathRoot.gameObject, deathContent, true);
            deathRoot.gameObject.SetActive(false);

            RectTransform controllersRect = EnsureRectChild(canvas.transform as RectTransform, "GameplayFlowControllers");
            ConfigureRect(controllersRect, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            PauseMenuController pauseController = GetOrAdd<PauseMenuController>(controllersRect.gameObject);
            pauseController.Configure(pauseTween, resume, restart, mainMenu, transition, interactions, "SampleScene");

            PlayerInputController playerInput = FindInScene<PlayerInputController>(scene);
            Health playerHealth = playerInput != null ? playerInput.GetComponent<Health>() : null;
            Rigidbody2D playerBody = playerInput != null ? playerInput.GetComponent<Rigidbody2D>() : null;
            Collider2D playerCollider = playerInput != null ? playerInput.GetComponent<Collider2D>() : null;
            GameObject playerVisual = playerInput != null ? playerInput.transform.Find("Visual")?.gameObject : null;
            CampfireInteraction campfire = FindInScene<CampfireInteraction>(scene);
            WorldGridService worldGrid = FindInScene<WorldGridService>(scene);
            PlayerRespawnController respawn = GetOrAdd<PlayerRespawnController>(controllersRect.gameObject);
            int blockedMask = LayerMaskFor(GameLayers.Building, GameLayers.Door, GameLayers.Dinosaur, GameLayers.Resource);
            respawn.Configure(playerHealth, playerBody, playerCollider, playerVisual, campfire, worldGrid,
                buildingSystem, interactions, blockedMask, deathTween, status, countdown, 10f, 3f);

            foreach (Button button in FindObjectsInScene<Button>(scene)) EnsureButtonTween(button);
            transition.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void SetupMainMenuScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            foreach (Canvas existing in FindObjectsInScene<Canvas>(scene)) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            foreach (EventSystem existing in FindObjectsInScene<EventSystem>(scene)) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject canvasObject = new("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            ConfigureScaler(canvasObject.GetComponent<CanvasScaler>());

            RectTransform root = EnsureRectChild(canvasObject.transform as RectTransform, "MainMenuRoot");
            Stretch(root);
            Image background = GetOrAdd<Image>(root.gameObject);
            background.color = new Color(0.025f, 0.09f, 0.055f, 1f);
            TMP_Text title = EnsureText(root, "Title", "Cogito Ergo Sum", 56f, new Vector2(0f, 145f), new Vector2(720f, 100f));
            title.fontStyle = FontStyles.Bold;
            Button play = EnsureButton(root, "PlayButton", "Play", new Vector2(0f, 25f), new Vector2(280f, 72f));
            Button exit = EnsureButton(root, "ExitButton", "Exit", new Vector2(0f, -70f), new Vector2(280f, 72f));
            SceneTransitionController transition = EnsureTransitionOverlay(canvasObject.transform);
            MainMenuController menu = GetOrAdd<MainMenuController>(root.gameObject);
            menu.Configure(play, exit, transition, "Game");
            EnsureButtonTween(play);
            EnsureButtonTween(exit);
            EnsureEventSystem(scene);
            transition.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void ConfigureExistingPopups(Scene scene, SceneTransitionController transition)
        {
            BuildingInteractionPopup building = FindInScene<BuildingInteractionPopup>(scene);
            if (building != null)
            {
                UIPanelTween tween = EnsurePanelTween(building.gameObject, building.transform as RectTransform, true);
                building.ConfigureTween(tween);
                building.gameObject.SetActive(false);
            }

            CampfireInteractionPopup campfire = FindInScene<CampfireInteractionPopup>(scene);
            if (campfire != null)
            {
                UIPanelTween tween = EnsurePanelTween(campfire.gameObject, campfire.transform as RectTransform, true);
                campfire.ConfigureTween(tween);
                campfire.gameObject.SetActive(false);
            }

            GameOverController gameOver = FindInScene<GameOverController>(scene);
            if (gameOver != null)
            {
                SerializedObject serialized = new(gameOver);
                GameObject panel = serialized.FindProperty("endedInfoPanel")?.objectReferenceValue as GameObject;
                if (panel != null)
                {
                    UIPanelTween tween = EnsurePanelTween(panel, panel.transform as RectTransform, true);
                    gameOver.ConfigureTween(tween, transition);
                    panel.SetActive(false);
                }
            }
        }

        private static Image EnsureToolbarSlot(RectTransform parent, string name, int index, string key, string label, Color color)
        {
            GameObject existing = FindDirectChild(parent, name);
            if (existing == null && index == 2) existing = FindDirectChild(parent, "BuildSlot3_Turret");
            GameObject slotObject = existing ?? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            slotObject.name = name;
            slotObject.layer = LayerMask.NameToLayer("UI");
            if (slotObject.transform.parent != parent) slotObject.transform.SetParent(parent, false);
            RectTransform rect = slotObject.transform as RectTransform;
            ConfigureRect(rect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(index * 82f, 0f), new Vector2(70f, 70f));
            Image image = GetOrAdd<Image>(slotObject);
            image.color = color;
            Button button = GetOrAdd<Button>(slotObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            EnsureButtonTween(button);
            TMP_Text text = slotObject.GetComponentInChildren<TMP_Text>(true) ?? EnsureText(rect, "Label", string.Empty, 15f, Vector2.zero, new Vector2(68f, 68f));
            text.text = $"{key}\n{label}";
            text.fontSize = 15f;
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform);
            return image;
        }

        private static SceneTransitionController EnsureTransitionOverlay(Transform canvas)
        {
            RectTransform overlayRect = EnsureRectChild(canvas as RectTransform, "SceneTransitionOverlay");
            Stretch(overlayRect);
            Image image = GetOrAdd<Image>(overlayRect.gameObject);
            image.color = Color.black;
            image.raycastTarget = true;
            CanvasGroup group = GetOrAdd<CanvasGroup>(overlayRect.gameObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            SceneTransitionController controller = GetOrAdd<SceneTransitionController>(overlayRect.gameObject);
            controller.Configure(group, 0.4f, true);
            return controller;
        }

        private static RectTransform EnsureFullScreenPanel(Transform canvas, string name, Color color)
        {
            RectTransform rect = EnsureRectChild(canvas as RectTransform, name);
            Stretch(rect);
            Image image = GetOrAdd<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = true;
            GetOrAdd<CanvasGroup>(rect.gameObject);
            return rect;
        }

        private static RectTransform EnsurePanelContent(RectTransform parent, string name, Vector2 size)
        {
            RectTransform rect = EnsureRectChild(parent, name);
            ConfigureRect(rect, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, size);
            Image image = GetOrAdd<Image>(rect.gameObject);
            image.color = new Color(0.075f, 0.09f, 0.12f, 0.98f);
            return rect;
        }

        private static UIPanelTween EnsurePanelTween(GameObject owner, RectTransform animatedRoot, bool hidden)
        {
            CanvasGroup group = GetOrAdd<CanvasGroup>(owner);
            UIPanelTween tween = GetOrAdd<UIPanelTween>(owner);
            tween.Configure(group, animatedRoot, hidden, 0.25f, 0.17f, 0.85f, 0.9f, true);
            return tween;
        }

        private static Button EnsureButton(Transform parent, string name, string text, Vector2 position, Vector2? size = null)
        {
            RectTransform rect = EnsureRectChild(parent as RectTransform, name);
            ConfigureRect(rect, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size ?? new Vector2(280f, 62f));
            Image image = GetOrAdd<Image>(rect.gameObject);
            image.color = new Color(0.22f, 0.27f, 0.35f, 1f);
            Button button = GetOrAdd<Button>(rect.gameObject);
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.38f, 0.5f, 1f);
            colors.pressedColor = new Color(0.16f, 0.2f, 0.28f, 1f);
            button.colors = colors;
            TMP_Text label = rect.GetComponentInChildren<TMP_Text>(true) ?? EnsureText(rect, "Label", text, 28f, Vector2.zero, rect.sizeDelta);
            label.text = text;
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            EnsureButtonTween(button);
            return button;
        }

        private static UIButtonTween EnsureButtonTween(Button button)
        {
            UIButtonTween tween = GetOrAdd<UIButtonTween>(button.gameObject);
            tween.Configure(button.transform as RectTransform, 1.05f, 0.95f, 1.08f, 0.1f, 0.07f);
            return tween;
        }

        private static TMP_Text EnsureText(Transform parent, string name, string value, float fontSize, Vector2 position, Vector2 size)
        {
            RectTransform rect = EnsureRectChild(parent as RectTransform, name);
            ConfigureRect(rect, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
            text.text = value;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            GameObject owner = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(owner, scene);
            InputSystemUIInputModule module = owner.GetComponent<InputSystemUIInputModule>();
            module.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            module.point = LoadInputReference("UI_Point");
            module.leftClick = LoadInputReference("UI_Click");
            module.middleClick = LoadInputReference("UI_MiddleClick");
            module.rightClick = LoadInputReference("UI_RightClick");
            module.scrollWheel = LoadInputReference("UI_ScrollWheel");
            module.move = LoadInputReference("UI_Navigate");
            module.submit = LoadInputReference("UI_Submit");
            module.cancel = LoadInputReference("UI_Cancel");
        }

        private static InputActionReference LoadInputReference(string name) =>
            AssetDatabase.LoadAssetAtPath<InputActionReference>($"Assets/Game/ScriptableObjects/Settings/UIInput/{name}.asset");

        private static void EnsureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            EnsureBuildScene(scenes, GameScenePath);
            EnsureBuildScene(scenes, MainMenuScenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureBuildScene(List<EditorBuildSettingsScene> scenes, string path)
        {
            EditorBuildSettingsScene existing = scenes.FirstOrDefault(entry => entry.path == path);
            if (existing != null) existing.enabled = true;
            else scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        private static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void ConfigureOverlayCanvas(Canvas canvas, int sortingOrder)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.overrideSorting = true;
            canvas.sortingLayerID = 0;
            canvas.sortingOrder = sortingOrder;
        }

        private static bool IsResponsiveScaler(CanvasScaler scaler) => scaler != null &&
            scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
            scaler.referenceResolution == ReferenceResolution && Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f);

        private static RectTransform EnsureRectChild(RectTransform parent, string name)
        {
            GameObject existing = FindDirectChild(parent, name);
            GameObject owner = existing ?? new GameObject(name, typeof(RectTransform));
            owner.layer = LayerMask.NameToLayer("UI");
            if (owner.transform.parent != parent) owner.transform.SetParent(parent, false);
            return owner.transform as RectTransform;
        }

        private static GameObject FindDirectChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i).gameObject;
            return null;
        }

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            ConfigureRect(rect, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        }

        private static int LayerMaskFor(params string[] layerNames)
        {
            int mask = 0;
            foreach (string layerName in layerNames)
            {
                int layer = LayerMask.NameToLayer(layerName);
                if (layer >= 0) mask |= 1 << layer;
            }
            return mask;
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T existing = owner.GetComponent<T>();
            return existing != null ? existing : owner.AddComponent<T>();
        }

        private static T FindInScene<T>(Scene scene) where T : Component => FindObjectsInScene<T>(scene).FirstOrDefault();

        private static IEnumerable<T> FindObjectsInScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        private static GameObject FindNamed(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == name)?.gameObject;

        private static void Require(bool condition, string message, ICollection<string> failures)
        {
            if (!condition) failures.Add(message);
        }
    }
}
