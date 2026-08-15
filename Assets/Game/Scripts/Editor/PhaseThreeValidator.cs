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
    public static class PhaseThreeValidator
    {
        [MenuItem("Tools/MicroJam/Phase 3/Validate Player Gameplay")]
        public static void ValidateFromMenu() => Validate(true);

        public static void ValidateFromBatch() => Validate(false);

        private static void Validate(bool showDialog)
        {
            List<string> failures = new();
            ValidateInput(failures);
            ValidatePrefab(failures);
            ValidateScene(failures);

            if (failures.Count > 0)
            {
                string message = "Phase 3 validation failed:\n - " + string.Join("\n - ", failures);
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Player gameplay validation failed", message, "OK");
                }

                throw new InvalidOperationException(message);
            }

            const string success = "Phase 3 validation passed: Player prefab/scene bindings, Input System actions, movement, mouse-facing hierarchy, melee settings, target filtering, and resource wallet are valid.";
            Debug.Log(success);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Player gameplay validation", success, "OK");
            }
        }

        private static void ValidateInput(List<string> failures)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PhaseThreeSetupTool.InputActionsPath);
            Require(asset != null, "Input System asset is missing.", failures);
            InputActionMap player = asset != null ? asset.FindActionMap("Player", false) : null;
            InputAction move = player?.FindAction("Move", false);
            InputAction attack = player?.FindAction("Attack", false);
            Require(move != null, "Player/Move action is missing.", failures);
            Require(attack != null, "Player/Attack action is missing.", failures);

            if (move != null)
            {
                string[] required = { "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d" };
                foreach (string path in required)
                {
                    Require(move.bindings.Any(binding => binding.path == path), $"Player/Move is missing {path}.", failures);
                }
            }

            if (attack != null)
            {
                Require(attack.bindings.Any(binding => binding.path == "<Keyboard>/e"), "Player/Attack must include E.", failures);
                Require(!attack.bindings.Any(binding => binding.path == "<Mouse>/leftButton"), "Left mouse must remain reserved for pointing/facing.", failures);
                Require(!attack.bindings.Any(binding => binding.path == "<Keyboard>/enter"), "Player/Attack must not retain the old Enter binding.", failures);
            }
        }

        private static void ValidatePrefab(List<string> failures)
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PhaseThreeSetupTool.PlayerPrefabPath);
            Require(player != null, "Player prefab is missing.", failures);
            if (player == null)
            {
                return;
            }

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Health health = player.GetComponent<Health>();
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            PlayerFacing facing = player.GetComponent<PlayerFacing>();
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            PlayerResourceWallet wallet = player.GetComponent<PlayerResourceWallet>();
            PlayerInputController input = player.GetComponent<PlayerInputController>();
            Transform facingRoot = player.transform.Find("FacingRoot");
            Transform indicator = facingRoot?.Find("FacingIndicator");
            Transform combatRoot = facingRoot?.Find("Combat");
            Transform attackOrigin = combatRoot?.Find("AttackOrigin");
            SpriteRenderer feedback = combatRoot?.Find("AttackVisual")?.GetComponent<SpriteRenderer>();

            Require(body != null && body.bodyType == RigidbodyType2D.Dynamic && Mathf.Approximately(body.gravityScale, 0f), "Player Rigidbody2D configuration changed.", failures);
            Require(player.GetComponent<CircleCollider2D>() != null, "Player collider is missing.", failures);
            Require(health != null && Mathf.Approximately(health.MaxHealth, 100f), "Player Health configuration changed.", failures);
            Require(player.GetComponentInChildren<HealthBar>(true) != null, "Player HealthBar hierarchy is missing.", failures);
            Require(movement != null && movement.Body == body && movement.Health == health, "PlayerMovement references are invalid.", failures);
            Require(movement != null && Mathf.Approximately(movement.MoveSpeed, 5f), "Player movement speed must be 5.", failures);
            Require(facing != null && facing.Health == health && facing.FacingVisualRoot == facingRoot, "PlayerFacing references are invalid.", failures);
            Require(facing != null && facing.GameplayViewport == null, "Prefab PlayerFacing viewport must remain scene-bound rather than referencing a scene object.", failures);
            Require(facingRoot != null && indicator != null && combatRoot != null && attackOrigin != null && feedback != null, "Player prefab-facing/combat child hierarchy is incomplete.", failures);
            Require(facingRoot != null && facingRoot.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast"), "FacingRoot must not intercept pointing.", failures);
            Require(indicator != null && indicator.GetComponent<SpriteRenderer>() != null && indicator.GetComponent<SpriteRenderer>().enabled, "Facing indicator must be a visible prefab-bound SpriteRenderer.", failures);
            Require(combat != null && combat.Health == health && combat.Facing == facing, "PlayerCombat references are invalid.", failures);
            Require(combat != null && combat.AttackOrigin == attackOrigin && combat.AttackFeedback == feedback, "PlayerCombat child references are invalid.", failures);
            Require(combat != null && Mathf.Approximately(combat.AttackDamage, 5f), "Melee damage must be 5.", failures);
            Require(combat != null && Mathf.Approximately(combat.AttackRange, 1.5f), "Melee range must be 1.5.", failures);
            Require(combat != null && Mathf.Approximately(combat.AttackArcDegrees, 90f), "Melee arc must be 90 degrees.", failures);
            Require(combat != null && Mathf.Approximately(combat.AttackCooldown, 0.4f), "Melee cooldown must be 0.4 seconds.", failures);
            int expectedMask = (1 << GameLayers.DinosaurIndex) | (1 << GameLayers.ResourceIndex);
            Require(combat != null && combat.TargetLayers.value == expectedMask, "Melee target mask must contain only Dinosaur and Resource.", failures);
            Require(feedback != null && !feedback.enabled, "Attack placeholder feedback must start hidden.", failures);
            Require(wallet != null && wallet.StartingWood == 20 && wallet.StartingStone == 20 && wallet.Wood == 20 && wallet.Stone == 20, "Resource wallet must start with 20 Wood and 20 Stone.", failures);
            Require(input != null && input.InputActions != null && input.Movement == movement && input.Facing == facing && input.Combat == combat, "PlayerInputController references are invalid.", failures);
            Require(input != null && input.HasValidActions, "PlayerInputController could not resolve Player/Move and Player/Attack.", failures);
            Require(player.GetComponents<PlayerMovement>().Length == 1 && player.GetComponents<PlayerFacing>().Length == 1 &&
                    player.GetComponents<PlayerCombat>().Length == 1 && player.GetComponents<PlayerResourceWallet>().Length == 1 &&
                    player.GetComponents<PlayerInputController>().Length == 1,
                "Player gameplay components must each exist exactly once on the prefab root.", failures);
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
            Transform player = game != null ? game.transform.Find("Actors/Player") : null;
            SquareGameplayViewport viewport = UnityEngine.Object.FindFirstObjectByType<SquareGameplayViewport>();
            Require(player != null, "Scene-bound Player is missing.", failures);
            Require(viewport != null, "Scene SquareGameplayViewport is missing.", failures);
            if (player == null)
            {
                return;
            }

            Require(PrefabUtility.IsPartOfPrefabInstance(player), "Scene Player must remain a connected prefab instance.", failures);
            Require(PrefabUtility.GetCorrespondingObjectFromSource(player.gameObject) != null, "Scene Player lost its prefab source.", failures);
            PlayerFacing facing = player.GetComponent<PlayerFacing>();
            Require(facing != null && facing.GameplayViewport == viewport, "Scene PlayerFacing must reference the scene SquareGameplayViewport.", failures);
            Require(player.GetComponent<PlayerMovement>() != null && player.GetComponent<PlayerCombat>() != null &&
                    player.GetComponent<PlayerResourceWallet>() != null && player.GetComponent<PlayerInputController>() != null,
                "Scene Player did not inherit all prefab gameplay components.", failures);
            Require(UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Scene must contain exactly one persistent Player movement stack.", failures);
            Require(game.transform.Find("Runtime") != null && game.transform.Find("UI") != null,
                "Existing Runtime/UI hierarchy was changed.", failures);
        }

        private static void Require(bool condition, string failure, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(failure);
            }
        }
    }
}
