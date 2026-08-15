using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MicroJam.Game.Editor
{
    public static class PhaseFiveSetupTool
    {
        public const string WallPrefabPath = "Assets/Game/Prefabs/Buildings/Wall.prefab";
        public const string DoorPrefabPath = "Assets/Game/Prefabs/Buildings/Door.prefab";
        public const string WallDefinitionPath = "Assets/Game/ScriptableObjects/Buildings/Wall.asset";
        public const string DoorDefinitionPath = "Assets/Game/ScriptableObjects/Buildings/Door.asset";
        public const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private const string SquareSpritePath = "Assets/Game/Art/Placeholders/Square.png";
        private const string DefinitionFolder = "Assets/Game/ScriptableObjects/Buildings";

        [MenuItem("Tools/MicroJam/Phase 5/Build Grid Building System")]
        public static void BuildFromMenu() => ApplyBuildingSystem(true);

        public static void ApplyBuildingSystem(bool logSuccess = true)
        {
            EnsureDefinitionFolder();
            GameObject wallPrefab = LoadRequiredPrefab(WallPrefabPath);
            GameObject doorPrefab = LoadRequiredPrefab(DoorPrefabPath);
            BuildingDefinition wall = EnsureDefinition(
                WallDefinitionPath, BuildingType.Wall, "Wall", wallPrefab, 5,
                blocksPlayer: true, blocksDinosaur: true, placeholderColor: new Color(0.35f, 0.38f, 0.42f));
            BuildingDefinition door = EnsureDefinition(
                DoorDefinitionPath, BuildingType.Door, "Door", doorPrefab, 10,
                blocksPlayer: false, blocksDinosaur: true, placeholderColor: new Color(0.55f, 0.28f, 0.08f));

            ConfigureBuildingPrefab(WallPrefabPath, wall);
            ConfigureBuildingPrefab(DoorPrefabPath, door);
            ConfigureScene(wall, door);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logSuccess)
            {
                Debug.Log("Phase 5 grid building, preview, Wall, and Door placement configured successfully.");
            }
        }

        public static void RunFromBatch()
        {
            ApplyBuildingSystem();
            PhaseOneValidator.ValidateFromBatch();
            PhaseTwoValidator.ValidateFromBatch();
            PhaseThreeValidator.ValidateFromBatch();
            PhaseFourValidator.ValidateFromBatch();
            PhaseFiveValidator.ValidateFromBatch();
        }

        private static void EnsureDefinitionFolder()
        {
            if (!AssetDatabase.IsValidFolder(DefinitionFolder))
            {
                Directory.CreateDirectory(DefinitionFolder);
                AssetDatabase.Refresh();
            }
        }

        private static BuildingDefinition EnsureDefinition(
            string path,
            BuildingType type,
            string displayName,
            GameObject prefab,
            int woodCost,
            bool blocksPlayer,
            bool blocksDinosaur,
            Color placeholderColor)
        {
            BuildingDefinition definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.Configure(type, displayName, prefab, woodCost, Vector2Int.one,
                blocksPlayer, blocksDinosaur, placeholderColor);
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
                if (health == null || footprint == null || root.GetComponent<BoxCollider2D>() == null)
                {
                    throw new InvalidOperationException($"{root.name} must retain its prefab-authored Health, GridFootprint, and collider.");
                }

                footprint.Configure(definition.FootprintSize);
                BuildingInstance instance = root.GetComponent<BuildingInstance>();
                if (instance == null)
                {
                    instance = root.AddComponent<BuildingInstance>();
                }

                instance.Configure(definition, health, footprint);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureScene(BuildingDefinition wall, BuildingDefinition door)
        {
            Scene scene = EditorSceneManager.OpenScene(PhaseOneSetupTool.ScenePath, OpenSceneMode.Single);
            GameObject game = GameObject.Find("Game");
            Transform systems = game != null ? game.transform.Find("Systems") : null;
            Transform runtime = game != null ? game.transform.Find("Runtime") : null;
            Transform player = game != null ? game.transform.Find("Actors/Player") : null;
            WorldGridService grid = UnityEngine.Object.FindFirstObjectByType<WorldGridService>();
            GridOccupancyService occupancy = UnityEngine.Object.FindFirstObjectByType<GridOccupancyService>();
            SquareGameplayViewport viewport = UnityEngine.Object.FindFirstObjectByType<SquareGameplayViewport>();
            PlayerResourceWallet wallet = player != null ? player.GetComponent<PlayerResourceWallet>() : null;
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (systems == null || runtime == null || grid == null || occupancy == null || viewport == null ||
                wallet == null || input == null || square == null)
            {
                throw new InvalidOperationException("Game scene and prior phases must be complete before configuring the building system.");
            }

            Transform buildings = FindOrCreateChild(runtime, "Buildings").transform;
            GameObject systemObject = FindOrCreateChild(systems, "BuildingSystem");
            GameObject previewObject = FindOrCreateChild(systemObject.transform, "BuildPreview");
            previewObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(previewObject);
            renderer.sprite = square;
            renderer.sortingOrder = 20;
            BuildPlacementPreview preview = GetOrAdd<BuildPlacementPreview>(previewObject);
            preview.Configure(renderer, new Color(0.15f, 1f, 0.25f, 0.52f), new Color(1f, 0.12f, 0.12f, 0.52f));

            BuildingSystem buildingSystem = GetOrAdd<BuildingSystem>(systemObject);
            LayerMask dynamicOccupants = (1 << GameLayers.PlayerIndex) | (1 << GameLayers.DinosaurIndex);
            buildingSystem.Configure(input, viewport, grid, occupancy, wallet, preview, buildings, wall, door, dynamicOccupants);

            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(preview);
            EditorUtility.SetDirty(buildingSystem);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PhaseOneSetupTool.ScenePath);
        }

        private static GameObject LoadRequiredPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Required prefab is missing: {path}");
            }

            return prefab;
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                ResetTransform(existing);
                return existing.gameObject;
            }

            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }

        private static void ResetTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }
    }
}
