using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MicroJam.Game.Editor
{
    public static class TowerAssetSetupTool
    {
        public const string BowTowerPrefabPath = "Assets/Game/Prefabs/Buildings/Towers/BowTower.prefab";
        public const string StoneTowerPrefabPath = "Assets/Game/Prefabs/Buildings/Towers/StoneTower.prefab";
        public const string BowProjectilePrefabPath = "Assets/Game/Prefabs/Buildings/Projectiles/BowArrowProjectile.prefab";
        public const string StoneProjectilePrefabPath = "Assets/Game/Prefabs/Buildings/Projectiles/StoneProjectile.prefab";
        public const string BowDefinitionPath = "Assets/Game/ScriptableObjects/Buildings/BowTower.asset";
        public const string StoneDefinitionPath = "Assets/Game/ScriptableObjects/Buildings/StoneTower.asset";

        private const string SquareSpritePath = "Assets/Game/Art/Placeholders/Square.png";

        [MenuItem("Tools/MicroJam/Towers/Create Tower Assets Only")]
        public static void ApplyFromMenu() => ApplyAssetsOnly(true);

        public static void ApplyAssetsOnly(bool logSuccess = true)
        {
            EnsureFolder("Assets/Game/Prefabs/Buildings/Towers");
            EnsureFolder("Assets/Game/Prefabs/Buildings/Projectiles");
            EnsureFolder("Assets/Game/ScriptableObjects/Buildings");

            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            HealthBarSettings healthBarSettings = AssetDatabase.LoadAssetAtPath<HealthBarSettings>(PhaseTwoSetupTool.HealthBarSettingsPath);
            if (square == null || healthBarSettings == null)
            {
                throw new InvalidOperationException("Tower asset creation requires the existing Square sprite and HealthBarSettings.");
            }

            TowerProjectile bowProjectile = CreateProjectilePrefab(
                BowProjectilePrefabPath, "Bow Arrow Projectile", square, new Color(0.76f, 0.48f, 0.18f), new Vector2(0.5f, 0.12f));
            TowerProjectile stoneProjectile = CreateProjectilePrefab(
                StoneProjectilePrefabPath, "Stone Projectile", square, new Color(0.42f, 0.45f, 0.5f), new Vector2(0.3f, 0.3f));

            CreateTowerPrefab(BowTowerPrefabPath, "Bow Tower", square, healthBarSettings, bowProjectile,
                100f, 30f, 5f, 0.5f, 20f,
                new Color(0.26f, 0.42f, 0.2f), new Color(0.58f, 0.35f, 0.12f));
            CreateTowerPrefab(StoneTowerPrefabPath, "Stone Tower", square, healthBarSettings, stoneProjectile,
                150f, 15f, 25f, 2f, 12f,
                new Color(0.34f, 0.37f, 0.43f), new Color(0.22f, 0.24f, 0.28f));

            GameObject bowTower = AssetDatabase.LoadAssetAtPath<GameObject>(BowTowerPrefabPath);
            GameObject stoneTower = AssetDatabase.LoadAssetAtPath<GameObject>(StoneTowerPrefabPath);
            BuildingDefinition bowDefinition = EnsureDefinition(BowDefinitionPath, BuildingType.BowTower, "Bow Tower",
                bowTower, 20, 10, new Color(0.26f, 0.62f, 0.24f));
            BuildingDefinition stoneDefinition = EnsureDefinition(StoneDefinitionPath, BuildingType.StoneTower, "Stone Tower",
                stoneTower, 10, 20, new Color(0.45f, 0.48f, 0.54f));
            BindDefinition(BowTowerPrefabPath, bowDefinition);
            BindDefinition(StoneTowerPrefabPath, stoneDefinition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (logSuccess)
            {
                Debug.Log("Bow/Stone Tower prefabs, projectile prefabs, and BuildingDefinitions created without opening or saving the gameplay scene.");
            }
        }

        public static void RunFromBatch()
        {
            ApplyAssetsOnly();
            ValidateAssets();
        }

        private static TowerProjectile CreateProjectilePrefab(
            string path, string objectName, Sprite sprite, Color color, Vector2 visualScale)
        {
            GameObject root = new(objectName) { layer = LayerMask.NameToLayer("Ignore Raycast") };
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = color;
                renderer.sortingOrder = 25;
                root.transform.localScale = new Vector3(visualScale.x, visualScale.y, 1f);
                TowerProjectile projectile = root.AddComponent<TowerProjectile>();
                projectile.Configure(0.15f, 10f);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                return saved.GetComponent<TowerProjectile>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateTowerPrefab(
            string path,
            string objectName,
            Sprite sprite,
            HealthBarSettings healthBarSettings,
            TowerProjectile projectilePrefab,
            float maxHealth,
            float range,
            float damage,
            float cooldown,
            float projectileSpeed,
            Color baseColor,
            Color turretColor)
        {
            GameObject root = new(objectName) { layer = GameLayers.BuildingIndex };
            try
            {
                GridFootprint footprint = root.AddComponent<GridFootprint>();
                footprint.Configure(new Vector2Int(2, 2));
                BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(2f, 2f);
                Health health = root.AddComponent<Health>();
                health.Configure(maxHealth);
                BuildingInstance instance = root.AddComponent<BuildingInstance>();
                BuildingRegeneration regeneration = root.AddComponent<BuildingRegeneration>();
                regeneration.Configure(health, 10f, 10f);

                Transform visual = CreateChild(root.transform, "Visual", GameLayers.BuildingIndex);
                Transform baseTransform = CreateSpriteChild(visual, "Base", sprite, baseColor, new Vector2(1.9f, 1.9f), 5);
                baseTransform.localRotation = Quaternion.identity;
                Transform turretPivot = CreateChild(visual, "TurretPivot", GameLayers.BuildingIndex);
                Transform turretVisual = CreateSpriteChild(turretPivot, "TurretVisual", sprite, turretColor, new Vector2(1.15f, 0.48f), 7);
                turretVisual.localPosition = new Vector3(0.15f, 0f, 0f);
                Transform spawnPoint = CreateChild(turretPivot, "ProjectileSpawnPoint", LayerMask.NameToLayer("Ignore Raycast"));
                spawnPoint.localPosition = new Vector3(0.85f, 0f, 0f);

                TowerCombat combat = root.AddComponent<TowerCombat>();
                combat.Configure(health, turretPivot, spawnPoint, projectilePrefab, range, damage, cooldown, projectileSpeed);
                CreateHealthBar(root.transform, health, healthBarSettings, sprite);
                instance.Configure(null, health, footprint);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateHealthBar(Transform root, Health health, HealthBarSettings settings, Sprite sprite)
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            Transform anchor = CreateChild(root, "HealthBarAnchor", layer);
            anchor.localPosition = new Vector3(0f, 1.25f, 0f);
            Transform barOwner = CreateChild(anchor, "HealthBar", layer);
            HealthBar bar = barOwner.gameObject.AddComponent<HealthBar>();
            SpriteRenderer background = CreateSpriteChild(barOwner, "Background", sprite, settings.BackgroundColor, Vector2.one, 30)
                .GetComponent<SpriteRenderer>();
            SpriteRenderer fill = CreateSpriteChild(barOwner, "Fill", sprite, settings.FriendlyColor, Vector2.one, 31)
                .GetComponent<SpriteRenderer>();
            bar.Configure(health, settings, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly,
                background, fill, new Vector2(1.8f, 0.14f));
        }

        private static BuildingDefinition EnsureDefinition(
            string path, BuildingType type, string name, GameObject prefab, int wood, int stone, Color previewColor)
        {
            BuildingDefinition definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.Configure(type, name, prefab, wood, stone, new Vector2Int(2, 2), true, true, previewColor, 0.5f);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void BindDefinition(string prefabPath, BuildingDefinition definition)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.GetComponent<GridFootprint>().Configure(definition.FootprintSize);
                root.GetComponent<BuildingInstance>().Configure(definition, root.GetComponent<Health>(), root.GetComponent<GridFootprint>());
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform CreateChild(Transform parent, string name, int layer)
        {
            GameObject child = new(name) { layer = layer };
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform CreateSpriteChild(
            Transform parent, string name, Sprite sprite, Color color, Vector2 scale, int sortingOrder)
        {
            Transform child = CreateChild(parent, name, parent.gameObject.layer);
            child.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return child;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        private static void ValidateAssets()
        {
            ValidateTower(BowTowerPrefabPath, BowDefinitionPath, 100f, 30f, 5f, 0.5f, 20f);
            ValidateTower(StoneTowerPrefabPath, StoneDefinitionPath, 150f, 15f, 25f, 2f, 12f);
            GameObject bowProjectile = AssetDatabase.LoadAssetAtPath<GameObject>(BowProjectilePrefabPath);
            GameObject stoneProjectile = AssetDatabase.LoadAssetAtPath<GameObject>(StoneProjectilePrefabPath);
            if (bowProjectile == null || bowProjectile.GetComponent<TowerProjectile>() == null ||
                stoneProjectile == null || stoneProjectile.GetComponent<TowerProjectile>() == null)
            {
                throw new InvalidOperationException("Tower projectile prefabs are incomplete.");
            }

            Debug.Log("Tower asset validation passed without opening the gameplay scene.");
        }

        private static void ValidateTower(
            string prefabPath, string definitionPath, float health, float range, float damage, float cooldown, float speed)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            BuildingDefinition definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(definitionPath);
            TowerCombat combat = prefab != null ? prefab.GetComponent<TowerCombat>() : null;
            if (prefab == null || definition == null || combat == null || prefab.GetComponent<BuildingInstance>()?.Definition != definition ||
                definition.FootprintSize != new Vector2Int(2, 2) || !Mathf.Approximately(prefab.GetComponent<Health>().MaxHealth, health) ||
                !Mathf.Approximately(combat.AttackRange, range) || !Mathf.Approximately(combat.AttackDamage, damage) ||
                !Mathf.Approximately(combat.AttackCooldown, cooldown) || !Mathf.Approximately(combat.ProjectileSpeed, speed))
            {
                throw new InvalidOperationException($"Tower validation failed for {prefabPath}.");
            }
        }
    }
}
