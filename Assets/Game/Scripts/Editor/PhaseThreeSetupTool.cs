using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MicroJam.Game.Editor
{
    public static class PhaseThreeSetupTool
    {
        public const string PlayerPrefabPath = "Assets/Game/Prefabs/Player/Player.prefab";
        public const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private const string SquareSpritePath = "Assets/Game/Art/Placeholders/Square.png";

        [MenuItem("Tools/MicroJam/Phase 3/Build Player Gameplay")]
        public static void BuildFromMenu() => ApplyPlayerGameplayFoundation(true);

        public static void ApplyPlayerGameplayFoundation(bool logSuccess = true)
        {
            ConfigurePlayerPrefab();
            BindSceneViewport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logSuccess)
            {
                Debug.Log("Phase 3 player movement, facing, melee combat, and resource wallet generated successfully.");
            }
        }

        public static void RunFromBatch()
        {
            ApplyPlayerGameplayFoundation();
            PhaseOneValidator.ValidateFromBatch();
            PhaseTwoValidator.ValidateFromBatch();
            PhaseThreeValidator.ValidateFromBatch();
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException($"Could not load Player prefab at {PlayerPrefabPath}.");
            }

            try
            {
                Rigidbody2D body = RequireComponent<Rigidbody2D>(root);
                Health health = RequireComponent<Health>(root);
                PlayerMovement movement = GetOrAdd<PlayerMovement>(root);
                PlayerFacing facing = GetOrAdd<PlayerFacing>(root);
                PlayerCombat combat = GetOrAdd<PlayerCombat>(root);
                PlayerResourceWallet wallet = GetOrAdd<PlayerResourceWallet>(root);
                PlayerInputController input = GetOrAdd<PlayerInputController>(root);

                Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
                if (square == null)
                {
                    throw new InvalidOperationException($"Could not load placeholder sprite at {SquareSpritePath}.");
                }

                int visualLayer = LayerMask.NameToLayer("Ignore Raycast");
                GameObject facingRootObject = FindOrCreateChild(root.transform, "FacingRoot", visualLayer);
                Transform facingRoot = facingRootObject.transform;
                ResetLocalTransform(facingRoot);

                GameObject indicatorObject = FindOrCreateChild(facingRoot, "FacingIndicator", visualLayer);
                ResetLocalTransform(indicatorObject.transform);
                indicatorObject.transform.localPosition = new Vector3(0.58f, 0f, 0f);
                indicatorObject.transform.localScale = new Vector3(0.35f, 0.1f, 1f);
                SpriteRenderer indicator = GetOrAdd<SpriteRenderer>(indicatorObject);
                indicator.sprite = square;
                indicator.color = new Color(0.15f, 0.9f, 1f, 0.95f);
                indicator.sortingOrder = 12;
                indicator.enabled = true;

                GameObject combatObject = FindOrCreateChild(facingRoot, "Combat", visualLayer);
                ResetLocalTransform(combatObject.transform);
                GameObject originObject = FindOrCreateChild(combatObject.transform, "AttackOrigin", visualLayer);
                ResetLocalTransform(originObject.transform);
                originObject.transform.localPosition = new Vector3(0.35f, 0f, 0f);

                GameObject feedbackObject = FindOrCreateChild(combatObject.transform, "AttackVisual", visualLayer);
                ResetLocalTransform(feedbackObject.transform);
                feedbackObject.transform.localPosition = new Vector3(0.88f, 0f, 0f);
                feedbackObject.transform.localScale = new Vector3(1.5f, 0.16f, 1f);
                SpriteRenderer feedback = GetOrAdd<SpriteRenderer>(feedbackObject);
                feedback.sprite = square;
                feedback.color = new Color(1f, 0.72f, 0.1f, 0.62f);
                feedback.sortingOrder = 11;
                feedback.enabled = false;

                movement.Configure(body, health, 5f);
                facing.Configure(health, null, facingRoot, Vector2.right);
                LayerMask targetMask = (1 << GameLayers.DinosaurIndex) | (1 << GameLayers.ResourceIndex);
                combat.Configure(health, facing, originObject.transform, feedback, targetMask, 5f, 1.5f, 90f, 0.4f);
                wallet.Configure(20, 20);

                InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                if (inputActions == null)
                {
                    throw new InvalidOperationException($"Could not load Input System asset at {InputActionsPath}.");
                }

                input.Configure(inputActions, movement, facing, combat);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BindSceneViewport()
        {
            Scene scene = EditorSceneManager.OpenScene(PhaseOneSetupTool.ScenePath, OpenSceneMode.Single);
            GameObject game = GameObject.Find("Game");
            Transform playerTransform = game != null ? game.transform.Find("Actors/Player") : null;
            SquareGameplayViewport viewport = UnityEngine.Object.FindFirstObjectByType<SquareGameplayViewport>();
            if (playerTransform == null || viewport == null)
            {
                throw new InvalidOperationException("Game scene must contain Game/Actors/Player and a SquareGameplayViewport before Phase 3 can be bound.");
            }

            PlayerFacing facing = playerTransform.GetComponent<PlayerFacing>();
            if (facing == null)
            {
                throw new InvalidOperationException("Scene Player did not inherit PlayerFacing from the Player prefab.");
            }

            facing.SetGameplayViewport(viewport);
            EditorUtility.SetDirty(facing);
            PrefabUtility.RecordPrefabInstancePropertyModifications(facing);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PhaseOneSetupTool.ScenePath);
        }

        private static T RequireComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"Player prefab is missing required {typeof(T).Name}.");
            }

            return component;
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static GameObject FindOrCreateChild(Transform parent, string name, int layer)
        {
            Transform existing = parent.Find(name);
            GameObject child = existing != null ? existing.gameObject : new GameObject(name);
            child.layer = layer;
            if (existing == null)
            {
                child.transform.SetParent(parent, false);
            }

            return child;
        }

        private static void ResetLocalTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }
    }
}
