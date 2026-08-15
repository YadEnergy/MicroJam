using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroJam.Game.Editor
{
    public static class PhaseFourSetupTool
    {
        public const string TreePrefabPath = "Assets/Game/Prefabs/Resources/Tree.prefab";
        public const string RockPrefabPath = "Assets/Game/Prefabs/Resources/Rock.prefab";
        public const string BushPrefabPath = "Assets/Game/Prefabs/Resources/Bush.prefab";

        [MenuItem("Tools/MicroJam/Phase 4/Build Resource System")]
        public static void BuildFromMenu() => ApplyResourceSystem(true);

        public static void ApplyResourceSystem(bool logSuccess = true)
        {
            ConfigureResourcePrefab(TreePrefabPath, ResourceNodeType.Tree, 1, 0.1f);
            ConfigureResourcePrefab(RockPrefabPath, ResourceNodeType.Rock, 1, 0.1f);
            ConfigureResourcePrefab(BushPrefabPath, ResourceNodeType.Bush, 1, 0.1f);
            ConfigureScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logSuccess)
            {
                Debug.Log("Phase 4 resource gathering and population system configured successfully.");
            }
        }

        public static void RunFromBatch()
        {
            ApplyResourceSystem();
            PhaseOneValidator.ValidateFromBatch();
            PhaseTwoValidator.ValidateFromBatch();
            PhaseThreeValidator.ValidateFromBatch();
            PhaseFourValidator.ValidateFromBatch();
        }

        private static void ConfigureResourcePrefab(string path, ResourceNodeType type, int rewardPerHit, float healPercent)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException($"Could not load resource prefab: {path}");
            }

            try
            {
                Health health = root.GetComponent<Health>();
                if (health == null)
                {
                    throw new InvalidOperationException($"{root.name} must already contain the universal Health component.");
                }

                ResourceNode node = root.GetComponent<ResourceNode>();
                if (node == null)
                {
                    node = root.AddComponent<ResourceNode>();
                }

                node.Configure(type, health, rewardPerHit, healPercent);
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
            Transform runtime = game != null ? game.transform.Find("Runtime") : null;
            WorldGridService grid = UnityEngine.Object.FindFirstObjectByType<WorldGridService>();
            if (systems == null || runtime == null || grid == null)
            {
                throw new InvalidOperationException("Game scene requires Game/Systems, Game/Runtime, and WorldGridService before configuring resources.");
            }

            GameObject occupancyObject = FindOrCreateChild(systems, "GridOccupancy");
            GridOccupancyService occupancy = GetOrAdd<GridOccupancyService>(occupancyObject);
            occupancy.Configure(grid);

            GameObject resourcesObject = FindOrCreateChild(runtime, "Resources");
            Transform trees = FindOrCreateChild(resourcesObject.transform, "Trees").transform;
            Transform rocks = FindOrCreateChild(resourcesObject.transform, "Rocks").transform;
            Transform bushes = FindOrCreateChild(resourcesObject.transform, "Bushes").transform;

            GameObject managerObject = FindOrCreateChild(systems, "ResourcePopulationManager");
            ResourcePopulationManager manager = GetOrAdd<ResourcePopulationManager>(managerObject);
            ResourceNode treePrefab = LoadNodePrefab(TreePrefabPath);
            ResourceNode rockPrefab = LoadNodePrefab(RockPrefabPath);
            ResourceNode bushPrefab = LoadNodePrefab(BushPrefabPath);
            LayerMask blockers =
                (1 << GameLayers.PlayerIndex) |
                (1 << GameLayers.DinosaurIndex) |
                (1 << GameLayers.BuildingIndex) |
                (1 << GameLayers.ResourceIndex) |
                (1 << GameLayers.DoorIndex);
            manager.Configure(grid, occupancy, treePrefab, trees, rockPrefab, rocks, bushPrefab, bushes, 10, 5, 64, blockers);

            EditorUtility.SetDirty(occupancy);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PhaseOneSetupTool.ScenePath);
        }

        private static ResourceNode LoadNodePrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ResourceNode node = prefab != null ? prefab.GetComponent<ResourceNode>() : null;
            if (node == null)
            {
                throw new InvalidOperationException($"Configured resource prefab is missing ResourceNode: {path}");
            }

            return node;
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
