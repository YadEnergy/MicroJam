using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MicroJam.Game.Editor
{
    public static class PhaseOneValidator
    {
        private static readonly string[] RequiredAssets =
        {
            "Assets/Game/Scenes/Game.unity",
            "Assets/Game/Settings/WorldGridConfig.asset",
            "Assets/Game/Prefabs/World/Campfire.prefab",
            "Assets/Game/Prefabs/Player/Player.prefab",
            "Assets/Game/Prefabs/Enemies/Dinosaur.prefab",
            "Assets/Game/Prefabs/Buildings/Wall.prefab",
            "Assets/Game/Prefabs/Buildings/Door.prefab",
            "Assets/Game/Prefabs/Resources/Tree.prefab",
            "Assets/Game/Prefabs/Resources/Rock.prefab",
            "Assets/Game/Prefabs/Resources/Bush.prefab"
        };

        [MenuItem("Tools/MicroJam/Phase 0 + 1/Validate Foundation")]
        public static void ValidateFromMenu() => Validate(true);

        public static void ValidateFromBatch() => Validate(false);

        private static void Validate(bool showDialog)
        {
            List<string> failures = new();

            foreach (string path in RequiredAssets)
            {
                Require(File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null, $"Missing asset: {path}", failures);
            }

            WorldGridConfig config = AssetDatabase.LoadAssetAtPath<WorldGridConfig>(PhaseOneSetupTool.ConfigPath);
            Require(config != null, "WorldGridConfig could not be loaded.", failures);
            if (config != null)
            {
                ValidateConfig(config, failures);
            }

            ValidateLayers(failures);
            ValidatePrefabs(config, failures);
            ValidateScene(config, failures);
            ValidateBuildSettings(failures);
            ValidateViewportMath(failures);

            if (failures.Count > 0)
            {
                string message = "Phase 0 + 1 validation failed:\n - " + string.Join("\n - ", failures);
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Foundation validation failed", message, "OK");
                }

                throw new InvalidOperationException(message);
            }

            const string success = "Phase 0 + 1 validation passed: configuration, scene, prefabs, layers, collision matrix, viewport math, and spawn perimeter are valid.";
            Debug.Log(success);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Foundation validation", success, "OK");
            }
        }

        private static void ValidateConfig(WorldGridConfig config, List<string> failures)
        {
            Require(Mathf.Approximately(config.TileSize, 1f), "Tile size must be exactly 1 world unit.", failures);
            Require(config.PlayableSize == new Vector2Int(50, 50), "Playable grid must be 50 x 50.", failures);
            Require(config.BuildZoneSize == new Vector2Int(30, 30), "Build zone must be 30 x 30.", failures);
            Require(config.CampfireFootprint == new Vector2Int(3, 3), "Campfire footprint must be 3 x 3.", failures);
            Require(config.ProtectedCampfireCellRect.size == new Vector2Int(5, 5), "Campfire protected area must be the 3 x 3 footprint plus one full tile on each side.", failures);
            Require(config.BuildZoneCellRect.width * config.BuildZoneCellRect.height == 900, "Build zone query must contain exactly 900 cells.", failures);

            int buildCellCount = 0;
            int protectedCellCount = 0;
            for (int y = 0; y < config.PlayableSize.y; y++)
            {
                for (int x = 0; x < config.PlayableSize.x; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (config.IsCellInsideBuildZone(cell))
                    {
                        buildCellCount++;
                    }

                    if (config.IsCellProtectedFromBuilding(cell))
                    {
                        protectedCellCount++;
                    }
                }
            }

            Require(buildCellCount == 900, $"Build zone query returned {buildCellCount} cells instead of 900.", failures);
            Require(protectedCellCount == 25, $"Protected no-build query returned {protectedCellCount} cells instead of 25.", failures);

            Vector2Int sampleCell = new(12, 34);
            Require(config.WorldToCell(config.CellToWorldCenter(sampleCell)) == sampleCell, "Grid/world conversion does not round-trip.", failures);
        }

        private static void ValidateLayers(List<string> failures)
        {
            Require(GameLayers.PlayerIndex >= 0, "Player layer is missing.", failures);
            Require(GameLayers.DinosaurIndex >= 0, "Dinosaur layer is missing.", failures);
            Require(GameLayers.BuildingIndex >= 0, "Building layer is missing.", failures);
            Require(GameLayers.ResourceIndex >= 0, "Resource layer is missing.", failures);
            Require(GameLayers.WorldBoundaryIndex >= 0, "WorldBoundary layer is missing.", failures);
            Require(GameLayers.DoorIndex >= 0, "Door layer is missing.", failures);

            Require(Ignored(GameLayers.PlayerIndex, GameLayers.DinosaurIndex), "Player and Dinosaur must not physically push each other.", failures);
            Require(Ignored(GameLayers.DinosaurIndex, GameLayers.DinosaurIndex), "Dinosaurs must not physically collide with each other.", failures);
            Require(!Ignored(GameLayers.PlayerIndex, GameLayers.BuildingIndex), "Player must collide with Wall/Campfire.", failures);
            Require(!Ignored(GameLayers.DinosaurIndex, GameLayers.BuildingIndex), "Dinosaur must collide with Wall/Campfire.", failures);
            Require(Ignored(GameLayers.PlayerIndex, GameLayers.DoorIndex), "Player must pass through Door.", failures);
            Require(!Ignored(GameLayers.DinosaurIndex, GameLayers.DoorIndex), "Dinosaur must collide with Door.", failures);
            Require(Ignored(GameLayers.PlayerIndex, GameLayers.ResourceIndex), "Player must pass through Resource.", failures);
            Require(Ignored(GameLayers.DinosaurIndex, GameLayers.ResourceIndex), "Dinosaur must pass through Resource.", failures);
            Require(!Ignored(GameLayers.PlayerIndex, GameLayers.WorldBoundaryIndex), "Player must collide with WorldBoundary.", failures);
            Require(!Ignored(GameLayers.DinosaurIndex, GameLayers.WorldBoundaryIndex), "Dinosaur must collide with WorldBoundary.", failures);
        }

        private static void ValidatePrefabs(WorldGridConfig config, List<string> failures)
        {
            GameObject player = LoadPrefab("Assets/Game/Prefabs/Player/Player.prefab", failures);
            ValidateActor(player, GameLayers.PlayerIndex, "Player", failures);

            GameObject dinosaur = LoadPrefab("Assets/Game/Prefabs/Enemies/Dinosaur.prefab", failures);
            ValidateActor(dinosaur, GameLayers.DinosaurIndex, "Dinosaur", failures);

            GameObject campfire = LoadPrefab("Assets/Game/Prefabs/World/Campfire.prefab", failures);
            if (campfire != null && config != null)
            {
                BoxCollider2D collider = campfire.GetComponent<BoxCollider2D>();
                GridFootprint footprint = campfire.GetComponent<GridFootprint>();
                Require(campfire.layer == GameLayers.BuildingIndex, "Campfire must use the Building layer.", failures);
                Require(collider != null && collider.size == new Vector2(3f, 3f), "Campfire collider must be exactly 3 x 3 units.", failures);
                Require(footprint != null && footprint.SizeInCells == new Vector2Int(3, 3), "Campfire GridFootprint must be exactly 3 x 3 cells.", failures);
                Require(campfire.transform.Find("Visual") != null, "Campfire requires a replaceable Visual child.", failures);
            }

            ValidateBuilding("Assets/Game/Prefabs/Buildings/Wall.prefab", GameLayers.BuildingIndex, "Wall", failures);
            ValidateBuilding("Assets/Game/Prefabs/Buildings/Door.prefab", GameLayers.DoorIndex, "Door", failures);
            ValidateResource("Assets/Game/Prefabs/Resources/Tree.prefab", "Tree", failures);
            ValidateResource("Assets/Game/Prefabs/Resources/Rock.prefab", "Rock", failures);
            ValidateResource("Assets/Game/Prefabs/Resources/Bush.prefab", "Bush", failures);
        }

        private static void ValidateScene(WorldGridConfig config, List<string> failures)
        {
            if (!File.Exists(PhaseOneSetupTool.ScenePath))
            {
                return;
            }

            EditorSceneManager.OpenScene(PhaseOneSetupTool.ScenePath, OpenSceneMode.Single);
            WorldGridService grid = UnityEngine.Object.FindFirstObjectByType<WorldGridService>();
            SpawnPerimeterProvider spawn = UnityEngine.Object.FindFirstObjectByType<SpawnPerimeterProvider>();
            SquareGameplayViewport viewport = UnityEngine.Object.FindFirstObjectByType<SquareGameplayViewport>();
            Camera camera = Camera.main;

            Require(grid != null && grid.Config == config, "Scene WorldGridService is missing or references the wrong config.", failures);
            Require(spawn != null, "Scene SpawnPerimeterProvider is missing.", failures);
            Require(viewport != null, "Scene SquareGameplayViewport is missing.", failures);
            Require(camera != null && camera.orthographic && Mathf.Approximately(camera.orthographicSize, 25f), "Main Camera must be orthographic and show 50 units vertically.", failures);

            GameObject game = GameObject.Find("Game");
            Require(game != null, "Game hierarchy root is missing.", failures);
            if (game != null)
            {
                Require(game.transform.Find("Systems") != null, "Systems hierarchy group is missing.", failures);
                Require(game.transform.Find("World/Ground") != null, "World/Ground hierarchy is missing.", failures);
                Transform boundaries = game.transform.Find("World/Boundaries");
                Require(boundaries != null && boundaries.GetComponentsInChildren<BoxCollider2D>().Length == 4, "World must have four physical boundary colliders.", failures);
                Require(game.transform.Find("World/Campfire") != null, "Campfire scene instance is missing.", failures);
                Require(game.transform.Find("World/BuildZone") != null, "BuildZone hierarchy object is missing.", failures);
                Require(game.transform.Find("Actors/Player") != null, "Player scene instance is missing.", failures);
                Require(game.transform.Find("Runtime") != null, "Runtime hierarchy group is missing.", failures);
                Require(game.transform.Find("UI") != null, "UI hierarchy group is missing.", failures);
            }

            if (spawn != null && config != null)
            {
                foreach (SpawnSide side in Enum.GetValues(typeof(SpawnSide)))
                {
                    Vector2 position = spawn.GetPosition(side, 0.5f);
                    Require(!config.PlayableWorldBounds.Contains(position), $"{side} spawn position is inside the visible world.", failures);
                    Require(Mathf.Abs(position.x) > 25f || Mathf.Abs(position.y) > 25f, $"{side} spawn position may be visible by the square camera.", failures);
                }
            }
        }

        private static void ValidateBuildSettings(List<string> failures)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Require(scenes.Length > 0 && scenes[0].enabled && scenes[0].path == PhaseOneSetupTool.ScenePath,
                "Game scene must be the first enabled scene in Build Settings.", failures);
        }

        private static void ValidateViewportMath(List<string> failures)
        {
            Rect wide = SquareGameplayViewport.CalculateSquareViewport(1920, 1080);
            Rect tall = SquareGameplayViewport.CalculateSquareViewport(1080, 1920);
            Rect square = SquareGameplayViewport.CalculateSquareViewport(1000, 1000);
            Require(Mathf.Approximately(wide.width * 1920f, wide.height * 1080f), "Wide viewport is not square in pixels.", failures);
            Require(Mathf.Approximately(tall.width * 1080f, tall.height * 1920f), "Tall viewport is not square in pixels.", failures);
            Require(square == new Rect(0f, 0f, 1f, 1f), "Square screen should use the full viewport.", failures);
        }

        private static void ValidateActor(GameObject prefab, int expectedLayer, string label, List<string> failures)
        {
            if (prefab == null)
            {
                return;
            }

            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
            Require(prefab.layer == expectedLayer, $"{label} uses the wrong layer.", failures);
            Require(body != null && body.bodyType == RigidbodyType2D.Dynamic && Mathf.Approximately(body.gravityScale, 0f), $"{label} Rigidbody2D is not configured for top-down movement.", failures);
            Require(collider != null && Mathf.Approximately(collider.radius * 2f, 0.8f), $"{label} collider diameter must be 0.8 units.", failures);
            Require(prefab.transform.Find("Visual") != null, $"{label} requires a replaceable Visual child.", failures);
        }

        private static void ValidateBuilding(string path, int expectedLayer, string label, List<string> failures)
        {
            GameObject prefab = LoadPrefab(path, failures);
            if (prefab == null)
            {
                return;
            }

            BoxCollider2D collider = prefab.GetComponent<BoxCollider2D>();
            GridFootprint footprint = prefab.GetComponent<GridFootprint>();
            Require(prefab.layer == expectedLayer, $"{label} uses the wrong layer.", failures);
            Require(collider != null && collider.size == Vector2.one && !collider.isTrigger, $"{label} must have a solid 1 x 1 collider.", failures);
            Require(footprint != null && footprint.SizeInCells == Vector2Int.one, $"{label} must have a 1 x 1 GridFootprint.", failures);
            Require(prefab.transform.Find("Visual") != null, $"{label} requires a replaceable Visual child.", failures);
        }

        private static void ValidateResource(string path, string label, List<string> failures)
        {
            GameObject prefab = LoadPrefab(path, failures);
            if (prefab == null)
            {
                return;
            }

            Collider2D collider = prefab.GetComponent<Collider2D>();
            Require(prefab.layer == GameLayers.ResourceIndex, $"{label} must use the Resource layer.", failures);
            Require(collider != null && collider.isTrigger, $"{label} must use a non-blocking trigger collider.", failures);
            Require(prefab.transform.Find("Visual") != null, $"{label} requires a replaceable Visual child.", failures);
        }

        private static GameObject LoadPrefab(string path, List<string> failures)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, $"Could not load prefab: {path}", failures);
            return prefab;
        }

        private static bool Ignored(int first, int second)
        {
            return first >= 0 && second >= 0 && Physics2D.GetIgnoreLayerCollision(first, second);
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
