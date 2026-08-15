using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class DinosaurSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpawnPerimeterProvider perimeter;
        [SerializeField] private Transform runtimeParent;

        [Header("Waves")]
        [SerializeField] private DinosaurWave[] waves;
        [SerializeField, Min(1)] private int maximumAlive = 20;

        private readonly List<DinosaurAgent> aliveDinosaurs = new();
        private int currentWaveIndex = -1;
        private int currentWaveCoins;

        public int CurrentWaveNumber => currentWaveIndex + 1;
        public int CurrentWaveCoins => currentWaveCoins;
        public int MaximumAlive => maximumAlive;
        public int AliveCount => aliveDinosaurs.Count;
        public bool HasConfiguredWaves => perimeter != null && waves != null && waves.Length > 0;

        private void Awake()
        {
            perimeter ??= FindFirstObjectByType<SpawnPerimeterProvider>();
        }

        public IEnumerator RunNextWaveAndWaitUntilCleared()
        {
            if (!HasConfiguredWaves)
            {
                Debug.LogError("DinosaurSpawner needs a perimeter and at least one configured wave.", this);
                yield break;
            }

            currentWaveIndex++;
            if (currentWaveIndex < waves.Length)
            {
                DinosaurWave configuredWave = waves[currentWaveIndex];
                if (configuredWave == null)
                {
                    Debug.LogWarning($"Night skipped: wave {CurrentWaveNumber} is not assigned.", this);
                    yield break;
                }

                yield return RunWave(configuredWave.CoinBank, configuredWave.AllowedDinosaurs, configuredWave.SpawnInterval);
                yield break;
            }

            int endlessWaveNumber = currentWaveIndex - waves.Length + 1;
            int coinBank = GetLastConfiguredCoinBank() + endlessWaveNumber * 10;
            DinosaurAgent[] allDinosaurs = GetAllConfiguredDinosaurs();
            Debug.Log($"Endless wave {CurrentWaveNumber} started with {coinBank} coins and all dinosaur types.", this);
            yield return RunWave(coinBank, allDinosaurs, GetLastConfiguredSpawnInterval());
        }

        private IEnumerator RunWave(int coinBank, DinosaurAgent[] allowedDinosaurs, float spawnInterval)
        {
            currentWaveCoins = coinBank;
            while (enabled)
            {
                DinosaurAgent prefab = GetRandomAffordablePrefab(allowedDinosaurs, currentWaveCoins);
                if (prefab == null)
                {
                    break; // The remaining coins cannot buy any allowed enemy.
                }

                RemoveDestroyedDinosaurs();
                while (aliveDinosaurs.Count >= maximumAlive)
                {
                    yield return null;
                    RemoveDestroyedDinosaurs();
                }

                Spawn(prefab);
                currentWaveCoins -= prefab.SpawnCost;
                yield return new WaitForSeconds(spawnInterval);
            }

            while (aliveDinosaurs.Count > 0)
            {
                yield return null;
                RemoveDestroyedDinosaurs();
            }

        }

        private int GetLastConfiguredCoinBank()
        {
            for (int i = waves.Length - 1; i >= 0; i--)
            {
                if (waves[i] != null)
                {
                    return waves[i].CoinBank;
                }
            }

            return 0;
        }

        private float GetLastConfiguredSpawnInterval()
        {
            for (int i = waves.Length - 1; i >= 0; i--)
            {
                if (waves[i] != null)
                {
                    return waves[i].SpawnInterval;
                }
            }

            return 0.75f;
        }

        private DinosaurAgent[] GetAllConfiguredDinosaurs()
        {
            HashSet<DinosaurAgent> uniqueDinosaurs = new();
            foreach (DinosaurWave wave in waves)
            {
                if (wave?.AllowedDinosaurs == null)
                {
                    continue;
                }

                foreach (DinosaurAgent dinosaur in wave.AllowedDinosaurs)
                {
                    if (dinosaur != null)
                    {
                        uniqueDinosaurs.Add(dinosaur);
                    }
                }
            }

            DinosaurAgent[] result = new DinosaurAgent[uniqueDinosaurs.Count];
            uniqueDinosaurs.CopyTo(result);
            return result;
        }

        private void Spawn(DinosaurAgent prefab)
        {
            Vector2 position = perimeter.GetRandomPosition(perimeter.GetRandomSide());
            DinosaurAgent dinosaur = Instantiate(prefab, position, Quaternion.identity, runtimeParent);
            dinosaur.Initialize();
            aliveDinosaurs.Add(dinosaur);
        }

        private static DinosaurAgent GetRandomAffordablePrefab(DinosaurAgent[] prefabs, int coins)
        {
            if (prefabs == null)
            {
                return null;
            }

            List<DinosaurAgent> affordable = null;
            foreach (DinosaurAgent prefab in prefabs)
            {
                if (prefab != null && prefab.SpawnCost <= coins)
                {
                    affordable ??= new List<DinosaurAgent>();
                    affordable.Add(prefab);
                }
            }

            return affordable == null ? null : affordable[UnityEngine.Random.Range(0, affordable.Count)];
        }

        private void RemoveDestroyedDinosaurs() => aliveDinosaurs.RemoveAll(dinosaur => dinosaur == null);

        private void OnValidate()
        {
            maximumAlive = Mathf.Max(1, maximumAlive);
            if (waves == null)
            {
                return;
            }

        }
    }
}
