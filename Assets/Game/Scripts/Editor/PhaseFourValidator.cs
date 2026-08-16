using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroJam.Game.Editor
{
    public static class PhaseFourValidator
    {
        private readonly struct ExpectedNode
        {
            public ExpectedNode(string path, ResourceNodeType type)
            {
                Path = path;
                Type = type;
            }

            public string Path { get; }
            public ResourceNodeType Type { get; }
        }

        private static readonly ExpectedNode[] ExpectedNodes =
        {
            new(PhaseFourSetupTool.TreePrefabPath, ResourceNodeType.Tree),
            new(PhaseFourSetupTool.RockPrefabPath, ResourceNodeType.Rock),
            new(PhaseFourSetupTool.BushPrefabPath, ResourceNodeType.Bush)
        };

        [MenuItem("Tools/MicroJam/Phase 4/Validate Resource System")]
        public static void ValidateFromMenu() => Validate(true);

        public static void ValidateFromBatch() => Validate(false);

        private static void Validate(bool showDialog)
        {
            List<string> failures = new();
            foreach (ExpectedNode expected in ExpectedNodes)
            {
                ValidatePrefab(expected, failures);
            }

            ValidateScene(failures);
            if (failures.Count > 0)
            {
                string message = "Phase 4 validation failed:\n - " + string.Join("\n - ", failures);
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Resource system validation failed", message, "OK");
                }

                throw new InvalidOperationException(message);
            }

            const string success = "Phase 4 validation passed: resource prefabs, successful-hit effects, scene-bound occupancy/population systems, runtime containers, and spawn configuration are valid.";
            Debug.Log(success);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Resource system validation", success, "OK");
            }
        }

        private static void ValidatePrefab(ExpectedNode expected, List<string> failures)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(expected.Path);
            Require(prefab != null, $"Missing resource prefab: {expected.Path}", failures);
            if (prefab == null)
            {
                return;
            }

            Health health = prefab.GetComponent<Health>();
            ResourceNode node = prefab.GetComponent<ResourceNode>();
            HealthBar bar = prefab.GetComponentInChildren<HealthBar>(true);
            Collider2D collider = prefab.GetComponent<Collider2D>();
            Require(prefab.layer == GameLayers.ResourceIndex, $"{prefab.name} must remain on Resource layer.", failures);
            Require(collider != null && !collider.isTrigger, $"{prefab.name} must physically block the Player.", failures);
            Require(health != null && Mathf.Approximately(health.MaxHealth, 50f) && Mathf.Approximately(health.CurrentHealth, 50f), $"{prefab.name} must serialize full 50 HP.", failures);
            Require(node != null && node.NodeType == expected.Type && node.Health == health, $"{prefab.name} ResourceNode configuration is invalid.", failures);
            Require(node != null && node.PopulationManager == null && !node.IsRegistered, $"{prefab.name} must not serialize runtime manager state.", failures);
            Require(node != null && node.ResourcePerSuccessfulHit == 1, $"{prefab.name} reward per successful hit must default to 1.", failures);
            Require(node != null && Mathf.Approximately(node.HealPercentPerSuccessfulHit, 0.1f), $"{prefab.name} heal percentage default must be 10%.", failures);
            Require(bar != null && bar.VisibilityMode == HealthBarVisibilityMode.ShowAfterDamage && bar.ColorRole == HealthBarColorRole.Friendly && !bar.IsVisible,
                $"{prefab.name} HealthBar must be green and hidden until damaged.", failures);
            Require(prefab.GetComponents<ResourceNode>().Length == 1, $"{prefab.name} must contain exactly one ResourceNode.", failures);
        }

        private static void ValidateScene(List<string> failures)
        {
            Require(File.Exists(PhaseOneSetupTool.ScenePath), "Game scene is missing.", failures);
            if (!File.Exists(PhaseOneSetupTool.ScenePath))
            {
                return;
            }

            EditorSceneManager.OpenScene(PhaseOneSetupTool.ScenePath, OpenSceneMode.Single);
            GameObject game = GameObject.Find("Game");
            Transform systems = game != null ? game.transform.Find("Systems") : null;
            Transform runtimeResources = game != null ? game.transform.Find("Runtime/Resources") : null;
            ResourcePopulationManager manager = UnityEngine.Object.FindFirstObjectByType<ResourcePopulationManager>();
            GridOccupancyService occupancy = UnityEngine.Object.FindFirstObjectByType<GridOccupancyService>();
            WorldGridService grid = UnityEngine.Object.FindFirstObjectByType<WorldGridService>();

            Require(systems?.Find("ResourcePopulationManager")?.GetComponent<ResourcePopulationManager>() == manager,
                "ResourcePopulationManager must be a scene-bound object directly under Systems.", failures);
            Require(systems?.Find("GridOccupancy")?.GetComponent<GridOccupancyService>() == occupancy,
                "GridOccupancy must be a scene-bound object directly under Systems.", failures);
            Require(manager != null && !PrefabUtility.IsPartOfPrefabInstance(manager), "ResourcePopulationManager must not be a prefab/runtime-created manager.", failures);
            Require(occupancy != null && occupancy.WorldGrid == grid, "GridOccupancy has the wrong WorldGridService reference.", failures);
            Require(manager != null && manager.WorldGrid == grid && manager.Occupancy == occupancy, "ResourcePopulationManager scene references are invalid.", failures);
            Require(runtimeResources != null && runtimeResources.Find("Trees") != null && runtimeResources.Find("Rocks") != null && runtimeResources.Find("Bushes") != null,
                "Scene-bound Runtime/Resources type containers are incomplete.", failures);
            Require(manager != null && manager.Tree.Prefab != null && manager.Tree.Prefab.NodeType == ResourceNodeType.Tree &&
                    manager.Tree.InitialCount == 10 && manager.Tree.MinimumCount == 5 && manager.Tree.RuntimeParent == runtimeResources?.Find("Trees"),
                "Tree population configuration is invalid.", failures);
            Require(manager != null && manager.Rock.Prefab != null && manager.Rock.Prefab.NodeType == ResourceNodeType.Rock &&
                    manager.Rock.InitialCount == 10 && manager.Rock.MinimumCount == 5 && manager.Rock.RuntimeParent == runtimeResources?.Find("Rocks"),
                "Rock population configuration is invalid.", failures);
            Require(manager != null && manager.Bush.Prefab != null && manager.Bush.Prefab.NodeType == ResourceNodeType.Bush &&
                    manager.Bush.InitialCount == 10 && manager.Bush.MinimumCount == 5 && manager.Bush.RuntimeParent == runtimeResources?.Find("Bushes"),
                "Bush population configuration is invalid.", failures);
            int blockers = (1 << GameLayers.PlayerIndex) | (1 << GameLayers.DinosaurIndex) |
                           (1 << GameLayers.BuildingIndex) | (1 << GameLayers.ResourceIndex) | (1 << GameLayers.DoorIndex);
            Require(manager != null && manager.RandomAttemptsPerSpawn == 64 && manager.SpawnBlockingLayers.value == blockers,
                "Resource spawn retry or blocker configuration is invalid.", failures);
            Require(UnityEngine.Object.FindObjectsByType<ResourcePopulationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Scene must contain exactly one persistent ResourcePopulationManager.", failures);
            Require(UnityEngine.Object.FindObjectsByType<GridOccupancyService>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Scene must contain exactly one persistent GridOccupancyService.", failures);
            Require(UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Resource nodes must be runtime-spawned prefab instances, not manually placed in the scene.", failures);
        }

        private static void Require(bool condition, string message, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }
    }
}
