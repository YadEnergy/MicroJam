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
            if (!TryFindCellPath(start, goals, allowBuildingTraversal, out List<Vector2Int> cells))
            {
                return false;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                worldPath.Add(worldGrid.CellToWorldCenter(cells[i]));
                if (i > 0 && firstBlockingBuilding == null)
                {
                    firstBlockingBuilding = GetBlockingBuilding(cells[i]);
                }
            }

            return true;
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
            out List<Vector2Int> path)
        {
            path = null;
            HashSet<Vector2Int> goalSet = new(goals);
            List<Vector2Int> open = new() { start };
            HashSet<Vector2Int> closed = new();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new();
            Dictionary<Vector2Int, int> gScore = new() { [start] = 0 };

            while (open.Count > 0)
            {
                int bestIndex = 0;
                int bestScore = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int score = gScore[open[i]] + Heuristic(open[i], goals);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                Vector2Int current = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (goalSet.Contains(current))
                {
                    path = Reconstruct(cameFrom, current);
                    return true;
                }

                closed.Add(current);
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = current + direction;
                    if (!worldGrid.Config.IsCellInsidePlayableArea(next) || closed.Contains(next))
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
                    if (!open.Contains(next))
                    {
                        open.Add(next);
                    }
                }
            }

            return false;
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
            }
        }

        private void OnValidate() => buildingTraversalCost = Mathf.Max(1, buildingTraversalCost);
    }
}
