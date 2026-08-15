using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroJam.Game.Editor
{
    public static class PhaseTwoValidator
    {
        private readonly struct ExpectedPrefab
        {
            public ExpectedPrefab(string path, float maxHealth, HealthBarVisibilityMode mode, HealthBarColorRole role)
            {
                Path = path;
                MaxHealth = maxHealth;
                Mode = mode;
                Role = role;
            }

            public string Path { get; }
            public float MaxHealth { get; }
            public HealthBarVisibilityMode Mode { get; }
            public HealthBarColorRole Role { get; }
        }

        private static readonly ExpectedPrefab[] ExpectedPrefabs =
        {
            new("Assets/Game/Prefabs/Player/Player.prefab", 100f, HealthBarVisibilityMode.AlwaysVisible, HealthBarColorRole.Friendly),
            new("Assets/Game/Prefabs/World/Campfire.prefab", 500f, HealthBarVisibilityMode.AlwaysVisible, HealthBarColorRole.Friendly),
            new("Assets/Game/Prefabs/Enemies/Dinosaur.prefab", 75f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Enemy),
            new("Assets/Game/Prefabs/Buildings/Wall.prefab", 150f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly),
            new("Assets/Game/Prefabs/Buildings/Door.prefab", 100f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly),
            new("Assets/Game/Prefabs/Resources/Tree.prefab", 50f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly),
            new("Assets/Game/Prefabs/Resources/Rock.prefab", 50f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly),
            new("Assets/Game/Prefabs/Resources/Bush.prefab", 50f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly)
        };

        [MenuItem("Tools/MicroJam/Phase 2/Validate Health Foundation")]
        public static void ValidateFromMenu() => Validate(true);

        public static void ValidateFromBatch() => Validate(false);

        private static void Validate(bool showDialog)
        {
            List<string> failures = new();
            HealthBarSettings settings = AssetDatabase.LoadAssetAtPath<HealthBarSettings>(PhaseTwoSetupTool.HealthBarSettingsPath);
            Require(settings != null, "HealthBarSettings asset is missing.", failures);
            if (settings != null)
            {
                Require(Mathf.Approximately(settings.DamagedVisibleDuration, 3f), "Damaged health-bar duration must default to 3 seconds.", failures);
                Require(settings.FriendlyColor.g > settings.FriendlyColor.r, "Friendly health color must be green.", failures);
                Require(settings.EnemyColor.r > settings.EnemyColor.g, "Enemy health color must be red.", failures);
            }

            foreach (ExpectedPrefab expected in ExpectedPrefabs)
            {
                ValidatePrefab(expected, settings, failures);
            }

            ValidateSceneBindings(failures);

            if (failures.Count > 0)
            {
                string message = "Phase 2 validation failed:\n - " + string.Join("\n - ", failures);
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Health validation failed", message, "OK");
                }

                throw new InvalidOperationException(message);
            }

            const string success = "Phase 2 validation passed: every target prefab owns configured Health and serialized HealthBar visuals, settings and scene-bound instances are valid.";
            Debug.Log(success);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Health validation", success, "OK");
            }
        }

        private static void ValidatePrefab(ExpectedPrefab expected, HealthBarSettings settings, List<string> failures)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(expected.Path);
            Require(prefab != null, $"Missing prefab: {expected.Path}", failures);
            if (prefab == null)
            {
                return;
            }

            Health health = prefab.GetComponent<Health>();
            Transform anchor = prefab.transform.Find("HealthBarAnchor");
            HealthBar bar = anchor != null ? anchor.GetComponentInChildren<HealthBar>(true) : null;

            Require(health != null, $"{prefab.name} is missing Health.", failures);
            Require(health != null && Mathf.Approximately(health.MaxHealth, expected.MaxHealth), $"{prefab.name} Max Health is incorrect.", failures);
            Require(health != null && Mathf.Approximately(health.CurrentHealth, expected.MaxHealth), $"{prefab.name} prefab must serialize full current health.", failures);
            Require(anchor != null, $"{prefab.name} is missing its HealthBarAnchor child.", failures);
            Require(anchor != null && anchor.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast"), $"{prefab.name} HealthBarAnchor must use Ignore Raycast.", failures);
            Require(bar != null, $"{prefab.name} is missing its prefab-owned HealthBar component.", failures);

            if (bar != null)
            {
                Require(bar.ObservedHealth == health, $"{prefab.name} HealthBar has the wrong Health reference.", failures);
                Require(bar.Settings == settings, $"{prefab.name} HealthBar has the wrong settings reference.", failures);
                Require(bar.VisibilityMode == expected.Mode, $"{prefab.name} HealthBar visibility mode is incorrect.", failures);
                Require(bar.ColorRole == expected.Role, $"{prefab.name} HealthBar color role is incorrect.", failures);
                Require(bar.GetComponentsInChildren<SpriteRenderer>(true).Length == 2, $"{prefab.name} HealthBar must contain serialized Background and Fill SpriteRenderers.", failures);
                Require(bar.GetComponentsInChildren<Collider2D>(true).Length == 0, $"{prefab.name} HealthBar hierarchy must not contain colliders.", failures);
                Require(bar.GetComponentsInChildren<Canvas>(true).Length == 0, $"{prefab.name} HealthBar must not create input-blocking canvases.", failures);
                bool expectedVisible = expected.Mode == HealthBarVisibilityMode.AlwaysVisible;
                Require(bar.IsVisible == expectedVisible, $"{prefab.name} HealthBar edit-mode visibility is incorrect.", failures);
            }
        }

        private static void ValidateSceneBindings(List<string> failures)
        {
            const string scenePath = "Assets/Game/Scenes/Game.unity";
            Require(File.Exists(scenePath), "Game scene is missing.", failures);
            if (!File.Exists(scenePath))
            {
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject game = GameObject.Find("Game");
            Transform player = game != null ? game.transform.Find("Actors/Player") : null;
            Transform campfire = game != null ? game.transform.Find("World/Campfire") : null;

            Require(player != null, "Scene-bound Player is missing.", failures);
            Require(campfire != null, "Scene-bound Campfire is missing.", failures);
            ValidateScenePrefabInstance(player, "Player", failures);
            ValidateScenePrefabInstance(campfire, "Campfire", failures);
            Require(UnityEngine.Object.FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 2,
                "The scene should contain exactly the persistent Player and Campfire Health components before runtime spawning.", failures);
        }

        private static void ValidateScenePrefabInstance(Transform instance, string label, List<string> failures)
        {
            if (instance == null)
            {
                return;
            }

            Require(PrefabUtility.IsPartOfPrefabInstance(instance), $"Scene {label} must remain a connected prefab instance.", failures);
            Require(instance.GetComponent<Health>() != null, $"Scene {label} did not inherit Health from its prefab.", failures);
            Require(instance.GetComponentInChildren<HealthBar>(true) != null, $"Scene {label} did not inherit its HealthBar hierarchy.", failures);
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
