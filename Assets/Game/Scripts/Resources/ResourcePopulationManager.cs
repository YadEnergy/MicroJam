using System;
using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    [Serializable]
    public sealed class ResourcePopulationDefinition
    {
        [SerializeField] private ResourceNode prefab;
        [SerializeField, Min(0)] private int initialCount = 10;
        [SerializeField, Min(0)] private int minimumCount = 5;
        [SerializeField] private Transform runtimeParent;

        public ResourceNode Prefab => prefab;
        public int InitialCount => initialCount;
        public int MinimumCount => minimumCount;
        public Transform RuntimeParent => runtimeParent;

        public void Configure(ResourceNode configuredPrefab, int configuredInitial, int configuredMinimum, Transform configuredParent)
        {
            prefab = configuredPrefab;
            initialCount = Mathf.Max(0, configuredInitial);
            minimumCount = Mathf.Max(0, configuredMinimum);
            runtimeParent = configuredParent;
        }
    }

    public sealed class ResourcePopulationManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private WorldGridService worldGrid;
        [SerializeField] private GridOccupancyService occupancy;

        [Header("Tree Population")]
        [SerializeField] private ResourcePopulationDefinition tree = new();

        [Header("Rock Population")]
        [SerializeField] private ResourcePopulationDefinition rock = new();

        [Header("Bush Population")]
        [SerializeField] private ResourcePopulationDefinition bush = new();

        [Header("Placement")]
        [SerializeField, Min(1)] private int randomAttemptsPerSpawn = 64;
        [SerializeField] private LayerMask spawnBlockingLayers;
        [SerializeField, Range(0.1f, 1f)] private float cellOverlapSize = 0.8f;
        [SerializeField] private bool spawnInitialPopulationOnStart = true;

        private readonly Dictionary<ResourceNodeType, HashSet<ResourceNode>> activeNodes = new();
        private bool runStarted;
        private bool shuttingDown;

        public WorldGridService WorldGrid => worldGrid;
        public GridOccupancyService Occupancy => occupancy;
        public ResourcePopulationDefinition Tree => tree;
        public ResourcePopulationDefinition Rock => rock;
        public ResourcePopulationDefinition Bush => bush;
        public int RandomAttemptsPerSpawn => randomAttemptsPerSpawn;
        public LayerMask SpawnBlockingLayers => spawnBlockingLayers;
        public bool RunStarted => runStarted;
        public int ActiveTreeCount => GetActiveCount(ResourceNodeType.Tree);
        public int ActiveRockCount => GetActiveCount(ResourceNodeType.Rock);
        public int ActiveBushCount => GetActiveCount(ResourceNodeType.Bush);
        public int ActiveNodeCount => ActiveTreeCount + ActiveRockCount + ActiveBushCount;

        public event Action<ResourceNode> NodeSpawned;
        public event Action<ResourceNodeType, Vector2Int> NodeRemoved;

        public void Configure(
            WorldGridService configuredGrid,
            GridOccupancyService configuredOccupancy,
            ResourceNode treePrefab,
            Transform treeParent,
            ResourceNode rockPrefab,
            Transform rockParent,
            ResourceNode bushPrefab,
            Transform bushParent,
            int initialCount = 10,
            int minimumCount = 5,
            int randomAttempts = 64,
            LayerMask blockingLayers = default)
        {
            worldGrid = configuredGrid;
            occupancy = configuredOccupancy;
            tree.Configure(treePrefab, initialCount, minimumCount, treeParent);
            rock.Configure(rockPrefab, initialCount, minimumCount, rockParent);
            bush.Configure(bushPrefab, initialCount, minimumCount, bushParent);
            randomAttemptsPerSpawn = Mathf.Max(1, randomAttempts);
            spawnBlockingLayers = blockingLayers;
            cellOverlapSize = 0.8f;
            spawnInitialPopulationOnStart = true;
        }

        public void BeginNewRun()
        {
            if (runStarted)
            {
                return;
            }

            runStarted = true;
            SpawnInitial(ResourceNodeType.Tree);
            SpawnInitial(ResourceNodeType.Rock);
            SpawnInitial(ResourceNodeType.Bush);
        }

        public int GetActiveCount(ResourceNodeType type)
        {
            EnsureCollections();
            activeNodes[type].RemoveWhere(node => node == null);
            return activeNodes[type].Count;
        }

        public ResourceNode[] GetActiveNodesSnapshot(ResourceNodeType type)
        {
            EnsureCollections();
            activeNodes[type].RemoveWhere(node => node == null);
            ResourceNode[] result = new ResourceNode[activeNodes[type].Count];
            activeNodes[type].CopyTo(result);
            return result;
        }

        public bool IsValidSpawnCell(Vector2Int cell, bool replacement)
        {
            if (worldGrid == null || worldGrid.Config == null || occupancy == null)
            {
                return false;
            }

            WorldGridConfig config = worldGrid.Config;
            if (!config.IsCellInsidePlayableArea(cell) || config.ProtectedCampfireCellRect.Contains(cell) ||
                (replacement && config.IsCellInsideBuildZone(cell)) || occupancy.IsCellOccupied(cell))
            {
                return false;
            }

            Vector2 center = config.CellToWorldCenter(cell);
            return Physics2D.OverlapBox(center, Vector2.one * cellOverlapSize, 0f, spawnBlockingLayers) == null;
        }

        public bool TrySpawnAtCell(ResourceNodeType type, Vector2Int cell, bool replacement, out ResourceNode spawned)
        {
            spawned = null;
            ResourcePopulationDefinition definition = GetDefinition(type);
            if (definition?.Prefab == null || definition.RuntimeParent == null || !IsValidSpawnCell(cell, replacement))
            {
                return false;
            }

            Vector2 position = worldGrid.Config.CellToWorldCenter(cell);
            spawned = Instantiate(definition.Prefab, position, Quaternion.identity, definition.RuntimeParent);
            spawned.name = $"{type} [{cell.x}, {cell.y}]";
            if (!spawned.InitializeSpawn(this, cell, replacement))
            {
                Destroy(spawned.gameObject);
                spawned = null;
                return false;
            }

            NodeSpawned?.Invoke(spawned);
            return true;
        }

        public bool RegisterSpawnedNode(ResourceNode node)
        {
            EnsureCollections();
            if (node == null || occupancy == null || activeNodes[node.NodeType].Contains(node) ||
                !occupancy.TryRegister(node, node.OccupiedCell))
            {
                return false;
            }

            activeNodes[node.NodeType].Add(node);
            return true;
        }

        public void NotifyNodeRemoved(ResourceNode node)
        {
            if (node == null)
            {
                return;
            }

            EnsureCollections();
            bool removed = activeNodes[node.NodeType].Remove(node);
            occupancy?.Unregister(node);
            if (!removed)
            {
                return;
            }

            NodeRemoved?.Invoke(node.NodeType, node.OccupiedCell);
            if (!shuttingDown && runStarted)
            {
                RestoreMinimum(node.NodeType);
            }
        }

        private void Awake()
        {
            EnsureCollections();
            if (worldGrid == null || occupancy == null)
            {
                Debug.LogError("ResourcePopulationManager requires scene-bound grid and occupancy references.", this);
            }
        }

        private void Start()
        {
            if (spawnInitialPopulationOnStart)
            {
                BeginNewRun();
            }
        }

        private void OnEnable() => shuttingDown = false;
        private void OnDisable() => shuttingDown = true;

        private void SpawnInitial(ResourceNodeType type)
        {
            ResourcePopulationDefinition definition = GetDefinition(type);
            int target = Mathf.Max(definition.InitialCount, definition.MinimumCount);
            while (GetActiveCount(type) < target)
            {
                if (!TrySpawnRandom(type, false))
                {
                    Debug.LogWarning($"Could not spawn the configured initial {type} population. Active: {GetActiveCount(type)}, target: {target}.", this);
                    break;
                }
            }
        }

        private void RestoreMinimum(ResourceNodeType type)
        {
            int minimum = GetDefinition(type).MinimumCount;
            while (GetActiveCount(type) < minimum)
            {
                if (!TrySpawnRandom(type, true))
                {
                    Debug.LogWarning($"Could not restore {type} population to minimum {minimum}; no valid outside-build-zone cell was available.", this);
                    break;
                }
            }
        }

        private bool TrySpawnRandom(ResourceNodeType type, bool replacement)
        {
            RectInt playable = worldGrid.Config.PlayableCellRect;
            for (int attempt = 0; attempt < randomAttemptsPerSpawn; attempt++)
            {
                Vector2Int cell = new(
                    UnityEngine.Random.Range(playable.xMin, playable.xMax),
                    UnityEngine.Random.Range(playable.yMin, playable.yMax));
                if (TrySpawnAtCell(type, cell, replacement, out _))
                {
                    return true;
                }
            }

            List<Vector2Int> fallback = new(playable.width * playable.height);
            foreach (Vector2Int cell in playable.allPositionsWithin)
            {
                if (IsValidSpawnCell(cell, replacement))
                {
                    fallback.Add(cell);
                }
            }

            for (int i = fallback.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (fallback[i], fallback[swapIndex]) = (fallback[swapIndex], fallback[i]);
            }

            return fallback.Count > 0 && TrySpawnAtCell(type, fallback[0], replacement, out _);
        }

        private ResourcePopulationDefinition GetDefinition(ResourceNodeType type)
        {
            return type switch
            {
                ResourceNodeType.Tree => tree,
                ResourceNodeType.Rock => rock,
                ResourceNodeType.Bush => bush,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private void EnsureCollections()
        {
            foreach (ResourceNodeType type in Enum.GetValues(typeof(ResourceNodeType)))
            {
                if (!activeNodes.ContainsKey(type))
                {
                    activeNodes.Add(type, new HashSet<ResourceNode>());
                }
            }
        }

        private void OnValidate()
        {
            randomAttemptsPerSpawn = Mathf.Max(1, randomAttemptsPerSpawn);
            cellOverlapSize = Mathf.Clamp(cellOverlapSize, 0.1f, 1f);
        }
    }
}
