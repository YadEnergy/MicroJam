using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MicroJam.Game.Editor
{
    public static class PhaseTwoSetupTool
    {
        public const string HealthBarSettingsPath = "Assets/Game/ScriptableObjects/Settings/HealthBarSettings.asset";

        private const string SquareSpritePath = "Assets/Game/Art/Placeholders/Square.png";

        private readonly struct PrefabHealthDefinition
        {
            public PrefabHealthDefinition(
                string path,
                float maxHealth,
                HealthBarVisibilityMode visibilityMode,
                HealthBarColorRole colorRole,
                Vector2 anchorPosition,
                Vector2 barSize)
            {
                Path = path;
                MaxHealth = maxHealth;
                VisibilityMode = visibilityMode;
                ColorRole = colorRole;
                AnchorPosition = anchorPosition;
                BarSize = barSize;
            }

            public string Path { get; }
            public float MaxHealth { get; }
            public HealthBarVisibilityMode VisibilityMode { get; }
            public HealthBarColorRole ColorRole { get; }
            public Vector2 AnchorPosition { get; }
            public Vector2 BarSize { get; }
        }

        private static readonly PrefabHealthDefinition[] Definitions =
        {
            new("Assets/Game/Prefabs/Player/Player.prefab", 100f, HealthBarVisibilityMode.AlwaysVisible, HealthBarColorRole.Friendly, new Vector2(0f, 0.72f), new Vector2(1f, 0.12f)),
            new("Assets/Game/Prefabs/World/Campfire.prefab", 500f, HealthBarVisibilityMode.AlwaysVisible, HealthBarColorRole.Friendly, new Vector2(0f, 1.9f), new Vector2(2.4f, 0.18f)),
            new("Assets/Game/Prefabs/Enemies/Dinosaur.prefab", 75f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Enemy, new Vector2(0f, 0.72f), new Vector2(1f, 0.12f)),
            new("Assets/Game/Prefabs/Buildings/Wall.prefab", 150f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly, new Vector2(0f, 0.68f), new Vector2(0.9f, 0.11f)),
            new("Assets/Game/Prefabs/Buildings/Door.prefab", 100f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly, new Vector2(0f, 0.68f), new Vector2(0.9f, 0.11f)),
            new("Assets/Game/Prefabs/Resources/Tree.prefab", 50f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly, new Vector2(0f, 0.72f), new Vector2(0.95f, 0.11f)),
            new("Assets/Game/Prefabs/Resources/Rock.prefab", 50f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly, new Vector2(0f, 0.72f), new Vector2(0.95f, 0.11f)),
            new("Assets/Game/Prefabs/Resources/Bush.prefab", 50f, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly, new Vector2(0f, 0.72f), new Vector2(0.95f, 0.11f))
        };

        [MenuItem("Tools/MicroJam/Phase 2/Apply Health Foundation")]
        public static void ApplyHealthFoundation() => ApplyHealthFoundation(true);

        public static void ApplyHealthFoundation(bool saveAndRefresh)
        {
            EnsureSettingsFolder();
            HealthBarSettings settings = EnsureSettingsAsset();
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (square == null)
            {
                throw new InvalidOperationException($"Required placeholder sprite is missing: {SquareSpritePath}");
            }

            foreach (PrefabHealthDefinition definition in Definitions)
            {
                ConfigurePrefab(definition, settings, square);
            }

            if (saveAndRefresh)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("Phase 2 Health and prefab-owned HealthBars configured successfully.");
        }

        public static void RunFromBatch()
        {
            ApplyHealthFoundation();
            PhaseOneValidator.ValidateFromBatch();
            PhaseTwoValidator.ValidateFromBatch();
        }

        private static void EnsureSettingsFolder()
        {
            const string path = "Assets/Game/ScriptableObjects/Settings";
            if (!AssetDatabase.IsValidFolder(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static HealthBarSettings EnsureSettingsAsset()
        {
            HealthBarSettings settings = AssetDatabase.LoadAssetAtPath<HealthBarSettings>(HealthBarSettingsPath);
            if (settings != null)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<HealthBarSettings>();
            AssetDatabase.CreateAsset(settings, HealthBarSettingsPath);
            return settings;
        }

        private static void ConfigurePrefab(PrefabHealthDefinition definition, HealthBarSettings settings, Sprite square)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(definition.Path);
            if (root == null)
            {
                throw new InvalidOperationException($"Could not load prefab: {definition.Path}");
            }

            try
            {
                Health health = root.GetComponent<Health>();
                if (health == null)
                {
                    health = root.AddComponent<Health>();
                }

                health.Configure(definition.MaxHealth);

                Transform oldAnchor = root.transform.Find("HealthBarAnchor");
                if (oldAnchor != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldAnchor.gameObject);
                }

                int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                GameObject anchor = NewChild(root.transform, "HealthBarAnchor", ignoreRaycastLayer);
                anchor.transform.localPosition = definition.AnchorPosition;

                GameObject healthBarObject = NewChild(anchor.transform, "HealthBar", ignoreRaycastLayer);
                GameObject backgroundObject = NewChild(healthBarObject.transform, "Background", ignoreRaycastLayer);
                GameObject fillObject = NewChild(healthBarObject.transform, "Fill", ignoreRaycastLayer);
                fillObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);

                SpriteRenderer background = backgroundObject.AddComponent<SpriteRenderer>();
                background.sprite = square;
                background.sortingOrder = 30;

                SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
                fill.sprite = square;
                fill.sortingOrder = 31;

                HealthBar healthBar = healthBarObject.AddComponent<HealthBar>();
                healthBar.Configure(
                    health,
                    settings,
                    definition.VisibilityMode,
                    definition.ColorRole,
                    background,
                    fill,
                    definition.BarSize);

                PrefabUtility.SaveAsPrefabAsset(root, definition.Path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject NewChild(Transform parent, string name, int layer)
        {
            GameObject child = new(name) { layer = layer };
            child.transform.SetParent(parent, false);
            return child;
        }
    }
}
