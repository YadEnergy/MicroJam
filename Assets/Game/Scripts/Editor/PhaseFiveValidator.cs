using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MicroJam.Game.Editor
{
    public static class PhaseFiveValidator
    {
        [MenuItem("Tools/MicroJam/Phase 5/Validate Grid Building System")]
        public static void ValidateFromMenu() => Validate(true);

        public static void ValidateFromBatch() => Validate(false);

        private static void Validate(bool showDialog)
        {
            List<string> failures = new();
            ValidateInput(failures);
            BuildingDefinition wall = ValidateDefinition(
                PhaseFiveSetupTool.WallDefinitionPath, PhaseFiveSetupTool.WallPrefabPath,
                BuildingType.Wall, 5, true, true, failures);
            BuildingDefinition door = ValidateDefinition(
                PhaseFiveSetupTool.DoorDefinitionPath, PhaseFiveSetupTool.DoorPrefabPath,
                BuildingType.Door, 10, false, true, failures);
            ValidatePrefab(PhaseFiveSetupTool.WallPrefabPath, wall, 150f, GameLayers.BuildingIndex, failures);
            ValidatePrefab(PhaseFiveSetupTool.DoorPrefabPath, door, 100f, GameLayers.DoorIndex, failures);
            ValidateScene(wall, door, failures);
            ValidateCollisionRules(failures);

            if (failures.Count > 0)
            {
                string message = "Phase 5 validation failed:\n - " + string.Join("\n - ", failures);
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Building system validation failed", message, "OK");
                }

                throw new InvalidOperationException(message);
            }

            const string success = "Phase 5 validation passed: build controls, data assets, prefab-owned buildings, scene-bound preview/system, occupancy, costs, and Wall/Door collision rules are valid.";
            Debug.Log(success);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Building system validation", success, "OK");
            }
        }

        private static void ValidateInput(List<string> failures)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PhaseFiveSetupTool.InputActionsPath);
            Require(asset != null, "Existing Input System asset is missing.", failures);
            InputActionMap map = asset?.FindActionMap("Building", false);
            Require(map != null, "Input asset must contain a dedicated Building action map.", failures);
            if (map == null)
            {
                return;
            }

            RequireBinding(map, "SelectWall", "<Keyboard>/1", 1, failures);
            RequireBinding(map, "SelectDoor", "<Keyboard>/2", 1, failures);
            RequireBinding(map, "Place", "<Mouse>/leftButton", 1, failures);
            RequireBinding(map, "Point", "<Mouse>/position", 1, failures);
            InputAction cancel = map.FindAction("Cancel", false);
            Require(cancel != null, "Building/Cancel action is missing.", failures);
            if (cancel != null)
            {
                string[] cancelPaths = cancel.bindings.Select(binding => binding.path).ToArray();
                Require(cancelPaths.Length == 2 && cancelPaths.Contains("<Keyboard>/escape") && cancelPaths.Contains("<Mouse>/rightButton"),
                    "Building/Cancel must be bound only to Escape and RMB.", failures);
            }
        }

        private static void RequireBinding(InputActionMap map, string actionName, string path, int expectedCount, List<string> failures)
        {
            InputAction action = map.FindAction(actionName, false);
            Require(action != null, $"Building/{actionName} action is missing.", failures);
            if (action != null)
            {
                Require(action.bindings.Count == expectedCount && action.bindings.Count(binding => binding.path == path) == expectedCount,
                    $"Building/{actionName} must be bound only to {path}.", failures);
            }
        }

        private static BuildingDefinition ValidateDefinition(
            string assetPath,
            string prefabPath,
            BuildingType type,
            int woodCost,
            bool blocksPlayer,
            bool blocksDinosaur,
            List<string> failures)
        {
            BuildingDefinition definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(assetPath);
            GameObject expectedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(definition != null, $"Building definition is missing: {assetPath}", failures);
            if (definition == null)
            {
                return null;
            }

            Require(definition.BuildingType == type && definition.DisplayName == type.ToString(), $"{type} identity is invalid.", failures);
            Require(definition.Prefab == expectedPrefab && expectedPrefab != null, $"{type} definition prefab reference is invalid.", failures);
            Require(definition.WoodCost == woodCost, $"{type} must cost {woodCost} Wood.", failures);
            Require(definition.FootprintSize == Vector2Int.one, $"{type} must currently occupy one cell.", failures);
            Require(definition.BlocksPlayer == blocksPlayer && definition.BlocksDinosaur == blocksDinosaur,
                $"{type} blocking metadata is invalid.", failures);
            return definition;
        }

        private static void ValidatePrefab(string path, BuildingDefinition definition, float maxHealth, int layer, List<string> failures)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, $"Building prefab is missing: {path}", failures);
            if (prefab == null)
            {
                return;
            }

            BuildingInstance instance = prefab.GetComponent<BuildingInstance>();
            Health health = prefab.GetComponent<Health>();
            GridFootprint footprint = prefab.GetComponent<GridFootprint>();
            BoxCollider2D collider = prefab.GetComponent<BoxCollider2D>();
            HealthBar bar = prefab.GetComponentInChildren<HealthBar>(true);
            Require(prefab.layer == layer, $"{prefab.name} is on the wrong physics layer.", failures);
            Require(prefab.transform.Find("Visual") != null, $"{prefab.name} must retain its prefab-authored Visual.", failures);
            Require(collider != null && !collider.isTrigger && collider.size == Vector2.one, $"{prefab.name} collider must be a solid one-cell box.", failures);
            Require(health != null && Mathf.Approximately(health.MaxHealth, maxHealth) && Mathf.Approximately(health.CurrentHealth, maxHealth),
                $"{prefab.name} must serialize full {maxHealth} HP.", failures);
            Require(footprint != null && footprint.SizeInCells == Vector2Int.one, $"{prefab.name} GridFootprint is invalid.", failures);
            Require(instance != null && instance.Definition == definition && instance.Health == health && instance.Footprint == footprint && !instance.IsRegistered,
                $"{prefab.name} BuildingInstance references or prefab runtime state are invalid.", failures);
            Require(instance != null && instance.OccupiedCells.Length == 0, $"{prefab.name} must not serialize occupied runtime cells.", failures);
            Require(prefab.GetComponents<BuildingInstance>().Length == 1, $"{prefab.name} must contain exactly one BuildingInstance.", failures);
            Require(bar != null && bar.VisibilityMode == HealthBarVisibilityMode.ShowAfterDamage &&
                    bar.ColorRole == HealthBarColorRole.Friendly && !bar.IsVisible,
                $"{prefab.name} health bar must be green and hidden until damaged.", failures);
        }

        private static void ValidateScene(BuildingDefinition wall, BuildingDefinition door, List<string> failures)
        {
            Require(File.Exists(PhaseOneSetupTool.ScenePath), "Game scene is missing.", failures);
            if (!File.Exists(PhaseOneSetupTool.ScenePath))
            {
                return;
            }

            EditorSceneManager.OpenScene(PhaseOneSetupTool.ScenePath, OpenSceneMode.Single);
            GameObject game = GameObject.Find("Game");
            Transform systems = game != null ? game.transform.Find("Systems") : null;
            Transform runtimeBuildings = game != null ? game.transform.Find("Runtime/Buildings") : null;
            Transform expectedSystem = systems != null ? systems.Find("BuildingSystem") : null;
            Transform expectedPreview = expectedSystem != null ? expectedSystem.Find("BuildPreview") : null;
            BuildingSystem buildingSystem = UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
            BuildPlacementPreview preview = UnityEngine.Object.FindFirstObjectByType<BuildPlacementPreview>();
            WorldGridService grid = UnityEngine.Object.FindFirstObjectByType<WorldGridService>();
            GridOccupancyService occupancy = UnityEngine.Object.FindFirstObjectByType<GridOccupancyService>();
            SquareGameplayViewport viewport = UnityEngine.Object.FindFirstObjectByType<SquareGameplayViewport>();
            PlayerResourceWallet wallet = UnityEngine.Object.FindFirstObjectByType<PlayerResourceWallet>();

            Require(expectedSystem?.GetComponent<BuildingSystem>() == buildingSystem,
                "BuildingSystem must be a scene-bound object directly under Systems.", failures);
            Require(buildingSystem != null && !PrefabUtility.IsPartOfPrefabInstance(buildingSystem),
                "BuildingSystem must not be runtime-created or a prefab instance.", failures);
            Require(buildingSystem != null && buildingSystem.WorldGrid == grid && buildingSystem.Occupancy == occupancy &&
                    buildingSystem.GameplayViewport == viewport && buildingSystem.PlayerWallet == wallet,
                "BuildingSystem scene service references are invalid.", failures);
            Require(buildingSystem != null && buildingSystem.WallDefinition == wall && buildingSystem.DoorDefinition == door,
                "BuildingSystem definition references are invalid.", failures);
            Require(buildingSystem != null && buildingSystem.RuntimeBuildingParent == runtimeBuildings,
                "Placed buildings must be parented under the scene-bound Runtime/Buildings container.", failures);
            Require(buildingSystem != null && buildingSystem.InputActions == AssetDatabase.LoadAssetAtPath<InputActionAsset>(PhaseFiveSetupTool.InputActionsPath) && buildingSystem.HasValidInputActions,
                "BuildingSystem Input System reference or actions are invalid.", failures);
            int dynamicMask = (1 << GameLayers.PlayerIndex) | (1 << GameLayers.DinosaurIndex);
            Require(buildingSystem != null && buildingSystem.DynamicOccupantLayers.value == dynamicMask,
                "Dynamic build blocking must check only Player and Dinosaur layers.", failures);
            Require(expectedPreview?.GetComponent<BuildPlacementPreview>() == preview && buildingSystem?.PlacementPreview == preview,
                "BuildPreview must be a scene-bound child of BuildingSystem.", failures);
            SpriteRenderer renderer = preview != null ? preview.PreviewRenderer : null;
            Require(renderer != null && !renderer.enabled && renderer.sprite != null && renderer.sortingOrder == 20,
                "BuildPreview renderer must be configured and hidden at rest.", failures);
            Require(preview != null && preview.ValidColor.g > preview.ValidColor.r && preview.InvalidColor.r > preview.InvalidColor.g,
                "BuildPreview valid/invalid colors must be green/red.", failures);
            Require(expectedPreview != null && expectedPreview.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast") &&
                    expectedPreview.GetComponent<Collider2D>() == null && expectedPreview.GetComponent<Health>() == null,
                "BuildPreview must remain non-interactive and non-gameplay.", failures);
            Require(occupancy != null && occupancy.OccupiedCellCount == 0,
                "Scene must not serialize runtime grid occupancy.", failures);
            Require(runtimeBuildings != null && runtimeBuildings.childCount == 0,
                "Buildings must be runtime-spawned, not manually placed in the scene.", failures);
            Require(UnityEngine.Object.FindObjectsByType<BuildingSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Scene must contain exactly one BuildingSystem.", failures);
            Require(UnityEngine.Object.FindObjectsByType<BuildPlacementPreview>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Scene must contain exactly one BuildPlacementPreview.", failures);
            Require(UnityEngine.Object.FindObjectsByType<BuildingInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Scene must not contain manually placed BuildingInstance objects.", failures);
        }

        private static void ValidateCollisionRules(List<string> failures)
        {
            Require(!Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.BuildingIndex),
                "Walls must block Player.", failures);
            Require(!Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.BuildingIndex),
                "Walls must block Dinosaur.", failures);
            Require(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.DoorIndex),
                "Doors must allow Player through.", failures);
            Require(!Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.DoorIndex),
                "Doors must block Dinosaur.", failures);
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
