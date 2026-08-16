using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class DinosaurNavigationGrid : MonoBehaviour
    {
        [SerializeField] private WorldGridService worldGrid;
        [SerializeField] private GridOccupancyService occupancy;
        [SerializeField, Min(1)] private int buildingTraversalCost = 25;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private readonly HashSet<Vector2Int> cachedBlockedTurns = new();
        private int blockedTurnCacheRevision = -1;

        public WorldGridService WorldGrid => worldGrid;
        public GridOccupancyService Occupancy => occupancy;
        public int Revision { get; private set; }

        public void Configure(WorldGridService configuredGrid, GridOccupancyService configuredOccupancy)
        {
            Unsubscribe();
            worldGrid = configuredGrid;
            occupancy = configuredOccupancy;
            Subscribe();
        }

        private void Awake()
        {
            worldGrid ??= FindFirstObjectByType<WorldGridService>();
            occupancy ??= FindFirstObjectByType<GridOccupancyService>();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public bool TryFindPathToTarget(
            Vector2 startWorld,
            Health target,
            float attackRange,
            bool allowBuildingTraversal,
            out List<Vector2> worldPath,
            out BuildingInstance firstBlockingBuilding)
        {
            return TryFindPathToTarget(startWorld, target, null, attackRange, 0f, allowBuildingTraversal,
                out worldPath, out firstBlockingBuilding);
        }

        public bool TryFindPathToTarget(
            Vector2 startWorld,
            Health target,
            DinosaurAttack attacker,
            float reservedStoppingDistance,
            bool allowBuildingTraversal,
            out List<Vector2> worldPath,
            out BuildingInstance firstBlockingBuilding)
        {
            float attackRange = attacker != null ? attacker.AttackRange : 0f;
            return TryFindPathToTarget(startWorld, target, attacker, attackRange, reservedStoppingDistance,
                allowBuildingTraversal, out worldPath, out firstBlockingBuilding);
        }

        private bool TryFindPathToTarget(
            Vector2 startWorld,
            Health target,
            DinosaurAttack attacker,
            float attackRange,
            float reservedStoppingDistance,
            bool allowBuildingTraversal,
            out List<Vector2> worldPath,
            out BuildingInstance firstBlockingBuilding)
        {
            worldPath = new List<Vector2>();
            firstBlockingBuilding = null;
            if (worldGrid == null || worldGrid.Config == null || occupancy == null || target == null || target.IsDead)
            {
                return false;
            }

            List<Vector2Int> goals = CollectAttackCells(target, attacker, attackRange, reservedStoppingDistance, allowBuildingTraversal);
            if (goals.Count == 0)
            {
                return false;
            }

            Vector2Int start = ClampToPlayable(worldGrid.WorldToCell(startWorld));
            if (blockedTurnCacheRevision != Revision)
            {
                cachedBlockedTurns.Clear();
                blockedTurnCacheRevision = Revision;
            }

            HashSet<Vector2Int> excludedTurns = new(cachedBlockedTurns);
            float turnClearance = GetTurnClearance(attacker);
            List<Vector2Int> cells = null;
            int attempts = 0;
            while (attempts++ < 6 && TryFindCellPath(start, goals, allowBuildingTraversal, excludedTurns, out cells))
            {
                if (allowBuildingTraversal || turnClearance <= 0f ||
                    !TryFindBlockedTurn(cells, turnClearance, out Vector2Int blockedTurn))
                {
                    break;
                }

                excludedTurns.Add(blockedTurn);
                cachedBlockedTurns.Add(blockedTurn);
                cells = null;
            }

            if (cells == null)
            {
                return false;
            }

            for (int i = 1; i < cells.Count; i++)
            {
                if (firstBlockingBuilding == null)
                {
                    firstBlockingBuilding = GetBlockingBuilding(cells[i]);
                }
            }

            worldPath = BuildSmoothedWorldPath(startWorld, cells, allowBuildingTraversal, GetTravelClearance(attacker));

            return true;
        }

        private List<Vector2> BuildSmoothedWorldPath(
            Vector2 startWorld,
            List<Vector2Int> cells,
            bool allowBuildingTraversal,
            float travelClearance)
        {
            List<Vector2> result = new();
            if (cells == null || cells.Count == 0) return result;

            // Routes through buildings are only queried to select the first obstacle. Keeping
            // their complete cell path makes that diagnostic route unambiguous.
            if (allowBuildingTraversal)
            {
                for (int i = 0; i < cells.Count; i++) result.Add(worldGrid.CellToWorldCenter(cells[i]));
                return result;
            }

            Vector2 anchor = startWorld;
            int next = cells.Count > 1 ? 1 : 0;
            while (next < cells.Count)
            {
                int furthest = next;
                for (int candidate = next + 1; candidate < cells.Count; candidate++)
                {
                    Vector2 candidateWorld = worldGrid.CellToWorldCenter(cells[candidate]);
                    if (!IsClearSegment(anchor, candidateWorld, travelClearance)) break;
                    furthest = candidate;
                }

                Vector2 waypoint = worldGrid.CellToWorldCenter(cells[furthest]);
                result.Add(waypoint);
                anchor = waypoint;
                next = furthest + 1;
            }

            return result;
        }

        private bool IsClearSegment(Vector2 from, Vector2 to, float clearance)
        {
            float sampleSpacing = Mathf.Max(0.05f, worldGrid.Config.TileSize * 0.2f);
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / sampleSpacing));
            Vector2 direction = (to - from).normalized;
            Vector2 perpendicular = new(-direction.y, direction.x);
            for (int i = 1; i <= samples; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)samples);
                if (!IsClearPoint(point) ||
                    (clearance > 0f && (!IsClearPoint(point + perpendicular * clearance) ||
                                        !IsClearPoint(point - perpendicular * clearance))))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsClearPoint(Vector2 point)
        {
            Vector2Int cell = worldGrid.WorldToCell(point);
            return worldGrid.Config.IsCellInsidePlayableArea(cell) && GetBlockingBuilding(cell) == null;
        }

        public bool HasFreePath(Vector2 startWorld, Health target, float attackRange)
        {
            return TryFindPathToTarget(startWorld, target, attackRange, false, out _, out _);
        }

        private List<Vector2Int> CollectAttackCells(
            Health target,
            DinosaurAttack attacker,
            float attackRange,
            float reservedStoppingDistance,
            bool allowBuildingTraversal)
        {
            List<Vector2Int> result = new();
            Collider2D targetCollider = target.GetComponent<Collider2D>();
            float range = Mathf.Max(0.01f, attackRange);
            RectInt playable = worldGrid.Config.PlayableCellRect;
            for (int y = playable.yMin; y < playable.yMax; y++)
            {
                for (int x = playable.xMin; x < playable.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    BuildingInstance candidateBlocker = GetBlockingBuilding(cell);
                    if (!allowBuildingTraversal && candidateBlocker != null)
                    {
                        continue;
                    }

                    Vector2 center = worldGrid.CellToWorldCenter(cell);
                    if (targetCollider != null && targetCollider.bounds.Contains(center))
                    {
                        continue;
                    }

                    Vector2 closest = targetCollider != null ? targetCollider.ClosestPoint(center) : (Vector2)target.transform.position;
                    bool canAttack = attacker != null
                        ? attacker.CanAttackFrom(center, target, reservedStoppingDistance)
                        : (closest - center).sqrMagnitude <= range * range;
                    if (canAttack &&
                        ((allowBuildingTraversal && candidateBlocker != null) || !HasBlockingBuildingBetween(center, closest)))
                    {
                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        private bool HasBlockingBuildingBetween(Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (worldGrid.Config.TileSize * 0.25f)));
            for (int i = 1; i < steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
                Vector2Int cell = worldGrid.WorldToCell(point);
                if (worldGrid.Config.IsCellInsidePlayableArea(cell) && GetBlockingBuilding(cell) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindCellPath(
            Vector2Int start,
            List<Vector2Int> goals,
            bool allowBuildingTraversal,
            HashSet<Vector2Int> excludedCells,
            out List<Vector2Int> path)
        {
            path = null;
            HashSet<Vector2Int> goalSet = new(goals);
            List<PathNode> open = new() { new PathNode(start, Heuristic(start, goals)) };
            HashSet<Vector2Int> closed = new();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new();
            Dictionary<Vector2Int, int> gScore = new() { [start] = 0 };

            while (open.Count > 0)
            {
                PathNode node = DequeueLowestPriority(open);
                Vector2Int current = node.Cell;
                if (closed.Contains(current))
                {
                    continue;
                }

                int expectedPriority = gScore[current] + Heuristic(current, goals);
                if (node.Priority != expectedPriority)
                {
                    continue; // An improved route to this cell was added to the heap.
                }

                if (goalSet.Contains(current))
                {
                    path = Reconstruct(cameFrom, current);
                    return true;
                }

                closed.Add(current);
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = current + direction;
                    if (!worldGrid.Config.IsCellInsidePlayableArea(next) || closed.Contains(next) ||
                        (excludedCells != null && excludedCells.Contains(next)))
                    {
                        continue;
                    }

                    BuildingInstance blocker = GetBlockingBuilding(next);
                    if (blocker != null && !allowBuildingTraversal)
                    {
                        continue;
                    }

                    int stepCost = blocker != null ? buildingTraversalCost : 1;
                    int tentative = gScore[current] + stepCost;
                    if (gScore.TryGetValue(next, out int known) && tentative >= known)
                    {
                        continue;
                    }

                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    Enqueue(open, new PathNode(next, tentative + Heuristic(next, goals)));
                }
            }

            return false;
        }

        private float GetTurnClearance(DinosaurAttack attacker)
        {
            if (attacker == null) return 0f;
            CapsuleCollider2D capsule = attacker.GetComponent<CapsuleCollider2D>();
            if (capsule == null) return 0f;

            Vector3 scale = capsule.transform.lossyScale;
            float width = capsule.size.x * Mathf.Abs(scale.x);
            float height = capsule.size.y * Mathf.Abs(scale.y);
            return Mathf.Max(width, height) * 0.5f + 0.05f;
        }

        private float GetTravelClearance(DinosaurAttack attacker)
        {
            if (attacker == null) return 0f;
            CapsuleCollider2D capsule = attacker.GetComponent<CapsuleCollider2D>();
            if (capsule == null) return 0f;

            Vector3 scale = capsule.transform.lossyScale;
            float width = capsule.size.x * Mathf.Abs(scale.x);
            float height = capsule.size.y * Mathf.Abs(scale.y);
            return Mathf.Min(width, height) * 0.5f + 0.03f;
        }

        private bool TryFindBlockedTurn(List<Vector2Int> cells, float clearance, out Vector2Int blockedTurn)
        {
            blockedTurn = default;
            if (cells == null || cells.Count < 3) return false;

            for (int i = 1; i < cells.Count - 1; i++)
            {
                Vector2Int incoming = cells[i] - cells[i - 1];
                Vector2Int outgoing = cells[i + 1] - cells[i];
                if (incoming == outgoing) continue;
                if (HasTurnClearance(cells[i], clearance)) continue;

                blockedTurn = cells[i];
                return true;
            }

            return false;
        }

        private bool HasTurnClearance(Vector2Int turnCell, float clearance)
        {
            Vector2 turnCenter = worldGrid.CellToWorldCenter(turnCell);
            float tileSize = worldGrid.Config.TileSize;
            int cellRadius = Mathf.CeilToInt(clearance / tileSize) + 1;
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    Vector2Int candidate = turnCell + new Vector2Int(x, y);
                    if (GetBlockingBuilding(candidate) == null) continue;

                    Vector2 buildingCenter = worldGrid.CellToWorldCenter(candidate);
                    Vector2 delta = turnCenter - buildingCenter;
                    float nearestX = Mathf.Max(Mathf.Abs(delta.x) - tileSize * 0.5f, 0f);
                    float nearestY = Mathf.Max(Mathf.Abs(delta.y) - tileSize * 0.5f, 0f);
                    if (nearestX * nearestX + nearestY * nearestY < clearance * clearance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void Enqueue(List<PathNode> heap, PathNode node)
        {
            heap.Add(node);
            int index = heap.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Compare(heap[parent], node) <= 0) break;

                heap[index] = heap[parent];
                index = parent;
            }

            heap[index] = node;
        }

        private static PathNode DequeueLowestPriority(List<PathNode> heap)
        {
            PathNode result = heap[0];
            PathNode last = heap[^1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return result;

            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= heap.Count) break;

                int right = left + 1;
                int child = right < heap.Count && Compare(heap[right], heap[left]) < 0 ? right : left;
                if (Compare(last, heap[child]) <= 0) break;

                heap[index] = heap[child];
                index = child;
            }

            heap[index] = last;
            return result;
        }

        private static int Compare(PathNode left, PathNode right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0) return priority;
            int x = left.Cell.x.CompareTo(right.Cell.x);
            return x != 0 ? x : left.Cell.y.CompareTo(right.Cell.y);
        }

        private readonly struct PathNode
        {
            public PathNode(Vector2Int cell, int priority)
            {
                Cell = cell;
                Priority = priority;
            }

            public Vector2Int Cell { get; }
            public int Priority { get; }
        }

        private BuildingInstance GetBlockingBuilding(Vector2Int cell)
        {
            if (!occupancy.TryGetOccupant(cell, out Object occupant))
            {
                return null;
            }

            BuildingInstance building = occupant as BuildingInstance;
            return building != null && building.BlocksDinosaur && !building.RemovalStarted ? building : null;
        }

        private Vector2Int ClampToPlayable(Vector2Int cell)
        {
            RectInt rect = worldGrid.Config.PlayableCellRect;
            return new Vector2Int(
                Mathf.Clamp(cell.x, rect.xMin, rect.xMax - 1),
                Mathf.Clamp(cell.y, rect.yMin, rect.yMax - 1));
        }

        private static int Heuristic(Vector2Int cell, List<Vector2Int> goals)
        {
            int best = int.MaxValue;
            foreach (Vector2Int goal in goals)
            {
                int distance = Mathf.Abs(goal.x - cell.x) + Mathf.Abs(goal.y - cell.y);
                if (distance < best) best = distance;
            }

            return best;
        }

        private static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            List<Vector2Int> result = new() { current };
            while (cameFrom.TryGetValue(current, out Vector2Int previous))
            {
                current = previous;
                result.Add(current);
            }

            result.Reverse();
            return result;
        }

        private void Subscribe()
        {
            if (occupancy != null)
            {
                occupancy.OccupancyChanged -= OnOccupancyChanged;
                occupancy.OccupancyChanged += OnOccupancyChanged;
            }
        }

        private void Unsubscribe()
        {
            if (occupancy != null) occupancy.OccupancyChanged -= OnOccupancyChanged;
        }

        private void OnOccupancyChanged(GridOccupancyChangedEvent change)
        {
            if (change.Occupant is BuildingInstance)
            {
                Revision++;
                cachedBlockedTurns.Clear();
                blockedTurnCacheRevision = Revision;
            }
        }

        private void OnValidate() => buildingTraversalCost = Mathf.Max(1, buildingTraversalCost);
    }
}
