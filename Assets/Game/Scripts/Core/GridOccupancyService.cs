using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class GridOccupancyService : MonoBehaviour
    {
        [SerializeField] private WorldGridService worldGrid;

        private readonly Dictionary<Vector2Int, Object> occupantsByCell = new();
        private readonly Dictionary<Object, HashSet<Vector2Int>> cellsByOccupant = new();

        public WorldGridService WorldGrid => worldGrid;
        public int OccupiedCellCount => occupantsByCell.Count;

        public void Configure(WorldGridService configuredWorldGrid) => worldGrid = configuredWorldGrid;

        public bool IsCellOccupied(Vector2Int cell)
        {
            if (!occupantsByCell.TryGetValue(cell, out Object occupant))
            {
                return false;
            }

            if (occupant != null)
            {
                return true;
            }

            occupantsByCell.Remove(cell);
            return false;
        }

        public bool TryGetOccupant(Vector2Int cell, out Object occupant)
        {
            if (IsCellOccupied(cell))
            {
                occupant = occupantsByCell[cell];
                return true;
            }

            occupant = null;
            return false;
        }

        public bool TryRegister(Object occupant, Vector2Int cell)
        {
            if (occupant == null || worldGrid == null || worldGrid.Config == null ||
                !worldGrid.Config.IsCellInsidePlayableArea(cell) || IsCellOccupied(cell))
            {
                return false;
            }

            occupantsByCell[cell] = occupant;
            if (!cellsByOccupant.TryGetValue(occupant, out HashSet<Vector2Int> cells))
            {
                cells = new HashSet<Vector2Int>();
                cellsByOccupant.Add(occupant, cells);
            }

            cells.Add(cell);
            return true;
        }

        public bool TryRegister(Object occupant, IEnumerable<Vector2Int> cells)
        {
            if (occupant == null || cells == null)
            {
                return false;
            }

            List<Vector2Int> requested = new();
            foreach (Vector2Int cell in cells)
            {
                if (requested.Contains(cell) || IsCellOccupied(cell) || worldGrid == null || worldGrid.Config == null ||
                    !worldGrid.Config.IsCellInsidePlayableArea(cell))
                {
                    return false;
                }

                requested.Add(cell);
            }

            if (requested.Count == 0)
            {
                return false;
            }

            foreach (Vector2Int cell in requested)
            {
                occupantsByCell[cell] = occupant;
            }

            cellsByOccupant[occupant] = new HashSet<Vector2Int>(requested);
            return true;
        }

        public bool Unregister(Object occupant)
        {
            if (occupant == null || !cellsByOccupant.TryGetValue(occupant, out HashSet<Vector2Int> cells))
            {
                return false;
            }

            foreach (Vector2Int cell in cells)
            {
                if (occupantsByCell.TryGetValue(cell, out Object registered) && registered == occupant)
                {
                    occupantsByCell.Remove(cell);
                }
            }

            cellsByOccupant.Remove(occupant);
            return true;
        }

        public Vector2Int[] GetOccupiedCellsSnapshot()
        {
            List<Vector2Int> cells = new(occupantsByCell.Count);
            Vector2Int[] candidates = new Vector2Int[occupantsByCell.Count];
            occupantsByCell.Keys.CopyTo(candidates, 0);
            foreach (Vector2Int cell in candidates)
            {
                if (IsCellOccupied(cell))
                {
                    cells.Add(cell);
                }
            }

            return cells.ToArray();
        }

        private void Awake()
        {
            if (worldGrid == null)
            {
                Debug.LogError("GridOccupancyService requires a scene-bound WorldGridService reference.", this);
            }
        }
    }
}
