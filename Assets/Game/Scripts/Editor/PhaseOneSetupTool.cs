using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroJam.Game.Editor
{
    public static class PhaseOneSetupTool
    {
        public const string GameRoot = "Assets/Game";
        public const string ScenePath = GameRoot + "/Scenes/Game.unity";
        public const string ConfigPath = GameRoot + "/Settings/WorldGridConfig.asset";

        private const string SquareSpritePath = GameRoot + "/Art/Placeholders/Square.png";
        private const string CircleSpritePath = GameRoot + "/Art/Placeholders/Circle.png";
        private const string PlayerPrefabPath = GameRoot + "/Prefabs/Player/Player.prefab";
        private const string DinosaurPrefabPath = GameRoot + "/Prefabs/Enemies/Dinosaur.prefab";
        private const string CampfirePrefabPath = GameRoot + "/Prefabs/World/Campfire.prefab";
        private const string WallPrefabPath = GameRoot + "/Prefabs/Buildings/Wall.prefab";
        private const string DoorPrefabPath = GameRoot + "/Prefabs/Buildings/Door.prefab";
        private const string TreePrefabPath = GameRoot + "/Prefabs/Resources/Tree.prefab";
        private const string RockPrefabPath = GameRoot + "/Prefabs/Resources/Rock.prefab";
        private const string BushPrefabPath = GameRoot + "/Prefabs/Resources/Bush.prefab";

        private static readonly string[] RequiredFolders =
        {
            GameRoot + "/Art",
            GameRoot + "/Art/Placeholders",
            GameRoot + "/Audio",
            GameRoot + "/Materials",
            GameRoot + "/Prefabs",
            GameRoot + "/Prefabs/Player",
            GameRoot + "/Prefabs/Enemies",
            GameRoot + "/Prefabs/Resources",
            GameRoot + "/Prefabs/Buildings",
            GameRoot + "/Prefabs/World",
            GameRoot + "/UI",
            GameRoot + "/Scenes",
            GameRoot + "/Scripts",
            GameRoot + "/Scripts/Core",
            GameRoot + "/Scripts/Editor",
            GameRoot + "/ScriptableObjects",
            GameRoot + "/ScriptableObjects/Dinosaurs",
            GameRoot + "/ScriptableObjects/Waves",
            GameRoot + "/Settings"
        };

        [MenuItem("Tools/MicroJam/Phase 0 + 1/Build Foundation")]
        public static void BuildFoundation()
        {
            EnsureFolders();
            ConfigureLayers();
            Sprite square = EnsurePlaceholderSprite(SquareSpritePath, false);
            Sprite circle = EnsurePlaceholderSprite(CircleSpritePath, true);
            WorldGridConfig config = EnsureWorldConfig();

            CreateCampfirePrefab(config, square, circle);
            CreateActorPrefab(PlayerPrefabPath, "Player", GameLayers.PlayerIndex, new Color(0.15f, 0.55f, 1f), circle);
            CreateActorPrefab(DinosaurPrefabPath, "Dinosaur", GameLayers.DinosaurIndex, new Color(0.65f, 0.2f, 0.75f), circle);
            CreateBuildingPrefab(WallPrefabPath, "Wall", GameLayers.BuildingIndex, new Color(0.35f, 0.38f, 0.42f), square);
            CreateBuildingPrefab(DoorPrefabPath, "Door", GameLayers.DoorIndex, new Color(0.55f, 0.28f, 0.08f), square);
            CreateResourcePrefab(TreePrefabPath, "Tree", new Color(0.12f, 0.55f, 0.16f), circle);
            CreateResourcePrefab(RockPrefabPath, "Rock", new Color(0.48f, 0.52f, 0.58f), circle);
            CreateResourcePrefab(BushPrefabPath, "Bush", new Color(0.25f, 0.75f, 0.28f), circle);
            CreateGameScene(config, square);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Phase 0 + 1 foundation generated successfully.");
        }

        public static void RunFromBatch()
        {
            BuildFoundation();
            PhaseOneValidator.ValidateFromBatch();
        }

        private static void EnsureFolders()
        {
            foreach (string folder in RequiredFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }

            AssetDatabase.Refresh();
        }

        private static WorldGridConfig EnsureWorldConfig()
        {
            WorldGridConfig config = AssetDatabase.LoadAssetAtPath<WorldGridConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<WorldGridConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static Sprite EnsurePlaceholderSprite(string path, bool circle)
        {
            const int resolution = 32;
            Texture2D texture = new(resolution, resolution, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[resolution * resolution];
            Vector2 center = new((resolution - 1) * 0.5f, (resolution - 1) * 0.5f);
            float radius = resolution * 0.47f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    bool visible = !circle || Vector2.Distance(new Vector2(x, y), center) <= radius;
                    pixels[y * resolution + x] = visible ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = resolution;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreateActorPrefab(string path, string name, int layer, Color color, Sprite circle)
        {
            GameObject root = NewRoot(name, layer);
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.4f;
            CreateVisual(root.transform, "Visual", circle, color, new Vector2(0.8f, 0.8f), 10, layer);
            SavePrefabAndDestroy(root, path);
        }

        private static void CreateCampfirePrefab(WorldGridConfig config, Sprite square, Sprite circle)
        {
            GameObject root = NewRoot("Campfire", GameLayers.BuildingIndex);
            GridFootprint footprint = root.AddComponent<GridFootprint>();
            footprint.Configure(config.CampfireFootprint);
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = (Vector2)config.CampfireFootprint * config.TileSize;

            GameObject visual = NewChild(root.transform, "Visual", GameLayers.BuildingIndex);
            CreateVisual(visual.transform, "Footprint", square, new Color(0.75f, 0.22f, 0.05f, 0.82f), new Vector2(3f, 3f), 5, GameLayers.BuildingIndex);
            CreateVisual(visual.transform, "Flame", circle, new Color(1f, 0.82f, 0.12f), new Vector2(0.9f, 0.9f), 6, GameLayers.BuildingIndex);
            SavePrefabAndDestroy(root, CampfirePrefabPath);
        }

        private static void CreateBuildingPrefab(string path, string name, int layer, Color color, Sprite square)
        {
            GameObject root = NewRoot(name, layer);
            root.AddComponent<GridFootprint>().Configure(Vector2Int.one);
            root.AddComponent<BoxCollider2D>().size = Vector2.one;
            CreateVisual(root.transform, "Visual", square, color, new Vector2(0.92f, 0.92f), 8, layer);
            SavePrefabAndDestroy(root, path);
        }

        private static void CreateResourcePrefab(string path, string name, Color color, Sprite circle)
        {
            GameObject root = NewRoot(name, GameLayers.ResourceIndex);
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.45f;
            collider.isTrigger = true;
            CreateVisual(root.transform, "Visual", circle, color, new Vector2(0.9f, 0.9f), 7, GameLayers.ResourceIndex);
            SavePrefabAndDestroy(root, path);
        }

        private static void CreateGameScene(WorldGridConfig config, Sprite square)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject game = new("Game");
            GameObject systems = NewChild(game.transform, "Systems", 0);
            GameObject world = NewChild(game.transform, "World", 0);
            NewChild(game.transform, "Runtime", 0);
            NewChild(game.transform, "UI", 5);

            WorldGridService gridService = systems.AddComponent<WorldGridService>();
            gridService.Configure(config);
            SpawnPerimeterProvider spawnProvider = systems.AddComponent<SpawnPerimeterProvider>();
            spawnProvider.Configure(config);

            GameObject cameraObject = NewChild(systems.transform, "Main Camera", 0);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = config.WorldSize.y * 0.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.AddComponent<AudioListener>();
            SquareGameplayViewport viewport = cameraObject.AddComponent<SquareGameplayViewport>();
            viewport.Configure(config, Color.black);

            GameObject ground = NewChild(world.transform, "Ground", 0);
            CreateVisual(ground.transform, "Visual", square, new Color(0.13f, 0.25f, 0.15f), config.WorldSize, -10, 0);

            GameObject boundaries = NewChild(world.transform, "Boundaries", GameLayers.WorldBoundaryIndex);
            CreateBoundary(boundaries.transform, "Left", new Vector2(-config.WorldSize.x * 0.5f - 0.5f, 0f), new Vector2(1f, config.WorldSize.y + 2f));
            CreateBoundary(boundaries.transform, "Right", new Vector2(config.WorldSize.x * 0.5f + 0.5f, 0f), new Vector2(1f, config.WorldSize.y + 2f));
            CreateBoundary(boundaries.transform, "Top", new Vector2(0f, config.WorldSize.y * 0.5f + 0.5f), new Vector2(config.WorldSize.x + 2f, 1f));
            CreateBoundary(boundaries.transform, "Bottom", new Vector2(0f, -config.WorldSize.y * 0.5f - 0.5f), new Vector2(config.WorldSize.x + 2f, 1f));

            GameObject buildZone = NewChild(world.transform, "BuildZone", 0);
            buildZone.AddComponent<WorldDebugVisualization>().Configure(config, true);

            GameObject campfirePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CampfirePrefabPath);
            GameObject campfire = (GameObject)PrefabUtility.InstantiatePrefab(campfirePrefab, world.transform);
            campfire.name = "Campfire";
            campfire.transform.position = config.CampfireWorldCenter;

            GameObject actors = NewChild(game.transform, "Actors", 0);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, actors.transform);
            player.name = "Player";
            player.transform.position = new Vector3(3f, 0f, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateBoundary(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject boundary = NewChild(parent, name, GameLayers.WorldBoundaryIndex);
            boundary.transform.position = position;
            boundary.AddComponent<BoxCollider2D>().size = size;
        }

        private static GameObject NewRoot(string name, int layer)
        {
            GameObject gameObject = new(name) { layer = layer };
            return gameObject;
        }

        private static GameObject NewChild(Transform parent, string name, int layer)
        {
            GameObject child = NewRoot(name, layer);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static SpriteRenderer CreateVisual(Transform parent, string name, Sprite sprite, Color color, Vector2 size, int sortingOrder, int layer)
        {
            GameObject visual = NewChild(parent, name, layer);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            return renderer;
        }

        private static void SavePrefabAndDestroy(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void ConfigureLayers()
        {
            Dictionary<string, int> requested = new()
            {
                { GameLayers.Player, 6 },
                { GameLayers.Dinosaur, 7 },
                { GameLayers.Building, 8 },
                { GameLayers.Resource, 9 },
                { GameLayers.WorldBoundary, 10 },
                { GameLayers.Door, 11 }
            };

            UnityEngine.Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject serialized = new(tagManager);
            SerializedProperty layers = serialized.FindProperty("layers");
            foreach ((string layerName, int preferredIndex) in requested)
            {
                int existing = LayerMask.NameToLayer(layerName);
                if (existing >= 0)
                {
                    continue;
                }

                int index = string.IsNullOrEmpty(layers.GetArrayElementAtIndex(preferredIndex).stringValue)
                    ? preferredIndex
                    : FindFirstEmptyLayer(layers);
                if (index < 0)
                {
                    throw new InvalidOperationException($"No free Unity layer is available for {layerName}.");
                }

                layers.GetArrayElementAtIndex(index).stringValue = layerName;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            SetIgnored(GameLayers.DinosaurIndex, GameLayers.DinosaurIndex, true);
            SetIgnored(GameLayers.PlayerIndex, GameLayers.DoorIndex, true);
            SetIgnored(GameLayers.PlayerIndex, GameLayers.ResourceIndex, true);
            SetIgnored(GameLayers.DinosaurIndex, GameLayers.ResourceIndex, true);
            SetIgnored(GameLayers.PlayerIndex, GameLayers.DinosaurIndex, false);
            SetIgnored(GameLayers.PlayerIndex, GameLayers.BuildingIndex, false);
            SetIgnored(GameLayers.DinosaurIndex, GameLayers.BuildingIndex, false);
            SetIgnored(GameLayers.DinosaurIndex, GameLayers.DoorIndex, false);
            SetIgnored(GameLayers.PlayerIndex, GameLayers.WorldBoundaryIndex, false);
            SetIgnored(GameLayers.DinosaurIndex, GameLayers.WorldBoundaryIndex, false);
        }

        private static int FindFirstEmptyLayer(SerializedProperty layers)
        {
            for (int i = 6; i < 32; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SetIgnored(int first, int second, bool ignored)
        {
            if (first < 0 || second < 0)
            {
                throw new InvalidOperationException("Required gameplay layers were not created.");
            }

            Physics2D.IgnoreLayerCollision(first, second, ignored);
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != ScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
