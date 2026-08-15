using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroJam.Game.Editor
{
    public static class DinosaurNavigationValidator
    {
        [MenuItem("Tools/MicroJam/Dinosaurs/Validate Recovery and Navigation")]
        public static void ValidateFromMenu() => Validate(true);
        public static void ValidateFromBatch() => Validate(false);

        private static void Validate(bool showDialog)
        {
            List<string> failures = new();
            ValidatePrefabs(failures);
            ValidateWaveBank(failures);
            ValidateScene(failures);
            if (failures.Count > 0)
            {
                string message = "Dinosaur navigation validation failed:\n - " + string.Join("\n - ", failures);
                Debug.LogError(message);
                if (showDialog) EditorUtility.DisplayDialog("Dinosaur validation failed", message, "OK");
                throw new InvalidOperationException(message);
            }

            const string success = "Dinosaur navigation validation passed: Building/Campfire recovery, prefab-owned AI, scene grid service, and the original seven-wave bank configuration are valid.";
            Debug.Log(success);
            if (showDialog) EditorUtility.DisplayDialog("Dinosaur validation", success, "OK");
        }

        private static void ValidatePrefabs(List<string> failures)
        {
            int[] expectedCosts = { 1, 2, 3 };
            for (int i = 0; i < DinosaurNavigationSetupTool.DinosaurPrefabPaths.Length; i++)
            {
                string path = DinosaurNavigationSetupTool.DinosaurPrefabPaths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null, $"Missing {path}.", failures);
                if (prefab == null) continue;
                DinosaurAgent agent = prefab.GetComponent<DinosaurAgent>();
                DinosaurMovement movement = prefab.GetComponent<DinosaurMovement>();
                DinosaurAttack attack = prefab.GetComponent<DinosaurAttack>();
                DinosaurTargeting targeting = prefab.GetComponent<DinosaurTargeting>();
                Health health = prefab.GetComponent<Health>();
                Require(agent != null && movement != null && attack != null && targeting != null && health != null,
                    $"{path} must contain every prefab-authored Dinosaur AI component.", failures);
                Require(agent != null && agent.Health == health && agent.Movement == movement && agent.Attack == attack && agent.Targeting == targeting,
                    $"{path} agent references are incomplete.", failures);
                Require(agent != null && agent.SpawnCost == expectedCosts[i], $"{path} spawn cost changed.", failures);
                Require(targeting != null && Mathf.Approximately(targeting.PlayerChaseFailureTimeout, 10f),
                    $"{path} Player chase failure timeout must be 10 seconds.", failures);
                Require(prefab.GetComponents<DinosaurAgent>().Length == 1 && prefab.GetComponents<DinosaurMovement>().Length == 1 &&
                        prefab.GetComponents<DinosaurAttack>().Length == 1 && prefab.GetComponents<DinosaurTargeting>().Length == 1,
                    $"{path} must have exactly one of each AI component.", failures);
            }
        }

        private static void ValidateWaveBank(List<string> failures)
        {
            int[] banks = { 10, 15, 20, 25, 30, 35, 40 };
            int[] allowedCounts = { 1, 1, 2, 1, 2, 2, 3 };
            for (int i = 0; i < banks.Length; i++)
            {
                string path = $"Assets/Game/Prefabs/Waves/DinosaurWave {i + 1}.asset";
                DinosaurWave wave = AssetDatabase.LoadAssetAtPath<DinosaurWave>(path);
                Require(wave != null && wave.CoinBank == banks[i] && Mathf.Approximately(wave.SpawnInterval, 0.75f) &&
                        wave.AllowedDinosaurs != null && wave.AllowedDinosaurs.Length == allowedCounts[i],
                    $"Original spawn-bank data changed in {path}.", failures);
            }
        }

        private static void ValidateScene(List<string> failures)
        {
            Require(File.Exists(PhaseOneSetupTool.ScenePath), "Game scene is missing.", failures);
            if (!File.Exists(PhaseOneSetupTool.ScenePath)) return;
            EditorSceneManager.OpenScene(PhaseOneSetupTool.ScenePath, OpenSceneMode.Single);
            GameObject game = GameObject.Find("Game");
            DinosaurNavigationGrid navigation = UnityEngine.Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            DinosaurSpawner spawner = UnityEngine.Object.FindFirstObjectByType<DinosaurSpawner>();
            Require(game?.transform.Find("Systems/BuildingSystem")?.GetComponent<BuildingSystem>() != null,
                "Merge-damaged BuildingSystem was not restored.", failures);
            Require(game?.transform.Find("Runtime/Buildings") != null, "Runtime/Buildings was not restored.", failures);
            Require(game?.transform.Find("UI/WorldInteraction/BuildingPopup") != null &&
                    game.transform.Find("UI/WorldInteraction/CampfirePopup") != null,
                "Building/Campfire popup hierarchy was not restored.", failures);
            Require(navigation != null && navigation.WorldGrid == UnityEngine.Object.FindFirstObjectByType<WorldGridService>() &&
                    navigation.Occupancy == UnityEngine.Object.FindFirstObjectByType<GridOccupancyService>(),
                "DinosaurNavigation must be scene-bound to the shared grid and occupancy services.", failures);
            Require(spawner != null && spawner.HasConfiguredWaves && spawner.MaximumAlive == 10,
                "Teammate DinosaurSpawner scene configuration changed.", failures);
        }

        private static void Require(bool condition, string message, List<string> failures)
        {
            if (!condition) failures.Add(message);
        }
    }
}
