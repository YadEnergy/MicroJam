using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static class PhaseSixValidator
    {
        [MenuItem("Tools/MicroJam/Phase 6/Validate Interaction, Removal, Regeneration, and Repair")]
        public static void ValidateFromMenu() => Validate(true);

        public static void ValidateFromBatch() => Validate(false);

        private static void Validate(bool showDialog)
        {
            List<string> failures = new();
            ValidateInput(failures);
            ValidateBuilding(PhaseFiveSetupTool.WallDefinitionPath, PhaseFiveSetupTool.WallPrefabPath, 3, failures);
            ValidateBuilding(PhaseFiveSetupTool.DoorDefinitionPath, PhaseFiveSetupTool.DoorPrefabPath, 5, failures);
            ValidateCampfirePrefab(failures);
            ValidateNoRegenerationOnOtherPrefabs(failures);
            ValidateScene(failures);

            if (failures.Count > 0)
            {
                string message = "Phase 6 validation failed:\n - " + string.Join("\n - ", failures);
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Interaction and repair validation failed", message, "OK");
                }

                throw new InvalidOperationException(message);
            }

            const string success = "Phase 6 validation passed: generalized removal/refunds/death, building regeneration, Campfire repair, live popup UI, EventSystem input, and scene-bound interaction architecture are valid.";
            Debug.Log(success);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Interaction and repair validation", success, "OK");
            }
        }

        private static void ValidateInput(List<string> failures)
        {
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PhaseFiveSetupTool.InputActionsPath);
            InputActionMap map = input?.FindActionMap("WorldInteraction", false);
            Require(map != null, "Input asset must contain WorldInteraction map.", failures);
            if (map == null)
            {
                return;
            }

            RequireSingleBinding(map, "Interact", "<Mouse>/leftButton", failures);
            RequireSingleBinding(map, "Point", "<Mouse>/position", failures);
            RequireSingleBinding(map, "Cancel", "<Keyboard>/escape", failures);
        }

        private static void RequireSingleBinding(InputActionMap map, string actionName, string path, List<string> failures)
        {
            InputAction action = map.FindAction(actionName, false);
            Require(action != null && action.bindings.Count == 1 && action.bindings[0].path == path,
                $"WorldInteraction/{actionName} must be bound only to {path}.", failures);
        }

        private static void ValidateBuilding(string definitionPath, string prefabPath, int expectedRefund, List<string> failures)
        {
            BuildingDefinition definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(definitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(definition != null && Mathf.Approximately(definition.RemovalRefundPercent, PhaseSixSetupTool.DefaultRefundPercent),
                $"{definitionPath} refund percent must be 50%.", failures);
            Require(definition != null && definition.RemovalRefundWood == expectedRefund,
                $"{definitionPath} calculated refund must be {expectedRefund} Wood.", failures);
            BuildingRegeneration regeneration = prefab != null ? prefab.GetComponent<BuildingRegeneration>() : null;
            BuildingInstance instance = prefab != null ? prefab.GetComponent<BuildingInstance>() : null;
            Health health = prefab != null ? prefab.GetComponent<Health>() : null;
            Require(regeneration != null && regeneration.Health == health &&
                    Mathf.Approximately(regeneration.RegenerationDelay, PhaseSixSetupTool.DefaultRegenerationDelay) &&
                    Mathf.Approximately(regeneration.RegenerationPerSecond, PhaseSixSetupTool.DefaultRegenerationPerSecond),
                $"{prefabPath} regeneration must be prefab-owned and configured to 10 seconds / 10 HP per second.", failures);
            Require(prefab != null && prefab.GetComponents<BuildingRegeneration>().Length == 1,
                $"{prefabPath} must contain exactly one BuildingRegeneration.", failures);
            Require(instance != null && instance.Definition == definition && instance.RemovalRefundWood == expectedRefund,
                $"{prefabPath} generalized BuildingInstance/refund linkage is invalid.", failures);
        }

        private static void ValidateCampfirePrefab(List<string> failures)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhaseSixSetupTool.CampfirePrefabPath);
            CampfireInteraction interaction = prefab != null ? prefab.GetComponent<CampfireInteraction>() : null;
            Health health = prefab != null ? prefab.GetComponent<Health>() : null;
            Require(interaction != null && interaction.Health == health && interaction.RepairWoodCost == PhaseSixSetupTool.DefaultCampfireRepairCost &&
                    Mathf.Approximately(interaction.RepairHealthPercent, PhaseSixSetupTool.DefaultCampfireRepairPercent),
                "Campfire prefab repair must cost 20 Wood and restore 10% Max Health.", failures);
            Require(prefab != null && prefab.GetComponents<CampfireInteraction>().Length == 1,
                "Campfire prefab must contain exactly one CampfireInteraction.", failures);
            Require(prefab != null && prefab.GetComponent<BuildingRegeneration>() == null,
                "Campfire must not have automatic building regeneration.", failures);
        }

        private static void ValidateNoRegenerationOnOtherPrefabs(List<string> failures)
        {
            string[] paths =
            {
                "Assets/Game/Prefabs/Player/Player.prefab",
                "Assets/Game/Prefabs/Enemies/Dinosaur.prefab",
                PhaseFourSetupTool.TreePrefabPath,
                PhaseFourSetupTool.RockPrefabPath,
                PhaseFourSetupTool.BushPrefabPath
            };
            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null && prefab.GetComponent<BuildingRegeneration>() == null,
                    $"Non-defensive prefab must not regenerate: {path}", failures);
            }
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
            Transform worldInteraction = game != null ? game.transform.Find("UI/WorldInteraction") : null;
            Transform buildingPanel = worldInteraction != null ? worldInteraction.Find("BuildingPopup") : null;
            Transform campfirePanel = worldInteraction != null ? worldInteraction.Find("CampfirePopup") : null;
            Transform eventSystemObject = game != null ? game.transform.Find("UI/EventSystem") : null;
            WorldInteractionController controller = worldInteraction != null ? worldInteraction.GetComponent<WorldInteractionController>() : null;
            BuildingInteractionPopup buildingPopup = buildingPanel != null ? buildingPanel.GetComponent<BuildingInteractionPopup>() : null;
            CampfireInteractionPopup campfirePopup = campfirePanel != null ? campfirePanel.GetComponent<CampfireInteractionPopup>() : null;
            CampfireInteraction campfire = UnityEngine.Object.FindFirstObjectByType<CampfireInteraction>();
            InputSystemUIInputModule inputModule = eventSystemObject != null ? eventSystemObject.GetComponent<InputSystemUIInputModule>() : null;
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PhaseFiveSetupTool.InputActionsPath);

            Require(worldInteraction != null && worldInteraction.GetComponent<Canvas>() != null &&
                    worldInteraction.GetComponent<CanvasScaler>() != null && worldInteraction.GetComponent<GraphicRaycaster>() != null,
                "Scene-bound UI/WorldInteraction Canvas hierarchy is incomplete.", failures);
            Require(controller != null && !PrefabUtility.IsPartOfPrefabInstance(controller) && controller.HasValidInputActions,
                "WorldInteractionController must be scene-bound with valid actions.", failures);
            Require(controller != null && controller.InputActions == input && controller.BuildingSystem == UnityEngine.Object.FindFirstObjectByType<BuildingSystem>() &&
                    controller.GameplayViewport == UnityEngine.Object.FindFirstObjectByType<SquareGameplayViewport>() &&
                    controller.PlayerWallet == UnityEngine.Object.FindFirstObjectByType<PlayerResourceWallet>(),
                "WorldInteractionController service references are invalid.", failures);
            int expectedLayers = (1 << GameLayers.BuildingIndex) | (1 << GameLayers.DoorIndex);
            Require(controller != null && controller.InteractableLayers.value == expectedLayers,
                "World interactions must query only Building and Door layers.", failures);
            Require(buildingPopup != null && controller?.BuildingPopup == buildingPopup && !buildingPanel.gameObject.activeSelf,
                "BuildingPopup must exist scene-bound and inactive by default.", failures);
            Require(buildingPopup != null && buildingPopup.TitleText != null && buildingPopup.HealthText != null &&
                    buildingPopup.RemovalPromptText != null && buildingPopup.RemoveButton != null && buildingPopup.CloseButton != null,
                "BuildingPopup UI references are incomplete.", failures);
            Require(campfirePopup != null && controller?.CampfirePopup == campfirePopup && !campfirePanel.gameObject.activeSelf,
                "CampfirePopup must exist scene-bound and inactive by default.", failures);
            Require(campfirePopup != null && campfirePopup.TitleText != null && campfirePopup.HealthText != null &&
                    campfirePopup.RepairDescriptionText != null && campfirePopup.RepairButton != null && campfirePopup.CloseButton != null,
                "CampfirePopup UI references are incomplete.", failures);
            Require(eventSystemObject != null && eventSystemObject.GetComponent<EventSystem>() != null && inputModule != null,
                "Scene-bound UI/EventSystem must use InputSystemUIInputModule.", failures);
            Require(inputModule != null && inputModule.actionsAsset == input && inputModule.point?.action == input.FindAction("UI/Point") &&
                    inputModule.leftClick?.action == input.FindAction("UI/Click") && inputModule.cancel?.action == input.FindAction("UI/Cancel"),
                "EventSystem UI action references are invalid.", failures);
            Require(campfire != null && campfire.transform.name == "Campfire" && campfire.GetComponent<BuildingRegeneration>() == null,
                "Scene Campfire interaction reference or no-regeneration rule is invalid.", failures);
            Require(UnityEngine.Object.FindObjectsByType<WorldInteractionController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Scene must contain exactly one WorldInteractionController.", failures);
            Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Scene must contain exactly one EventSystem.", failures);
            Require(game != null && game.transform.Find("Runtime/Buildings") != null &&
                    UnityEngine.Object.FindObjectsByType<BuildingInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Runtime/Buildings must remain scene-bound and empty before Play Mode.", failures);
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
