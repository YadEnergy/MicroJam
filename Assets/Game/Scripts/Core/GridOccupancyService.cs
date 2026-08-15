using System;
using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    public readonly struct GridOccupancyChangedEvent
    {
        public GridOccupancyChangedEvent(UnityEngine.Object occupant, Vector2Int[] cells, bool occupied, int revision)
        {
            Occupant = occupant;
            Cells = cells;
            Occupied = occupied;
            Revision = revision;
        }

        public UnityEngine.Object Occupant { get; }
        public Vector2Int[] Cells { get; }
        public bool Occupied { get; }
        public int Revision { get; }
    }

    public sealed class GridOccupancyService : MonoBehaviour
    {
        [SerializeField] private WorldGridService worldGrid;

        private readonly Dictionary<Vector2Int, UnityEngine.Object> occupantsByCell = new();
        private readonly Dictionary<UnityEngine.Object, HashSet<Vector2Int>> cellsByOccupant = new();

        public WorldGridService WorldGrid => worldGrid;
        public int OccupiedCellCount => occupantsByCell.Count;
        public int Revision { get; private set; }

        public event Action<GridOccupancyChangedEvent> OccupancyChanged;

        public void Configure(WorldGridService configuredWorldGrid) => worldGrid = configuredWorldGrid;

        public bool IsCellOccupied(Vector2Int cell)
        {
            if (!occupantsByCell.TryGetValue(cell, out UnityEngine.Object occupant))
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

        public bool TryGetOccupant(Vector2Int cell, out UnityEngine.Object occupant)
        {
            if (IsCellOccupied(cell))
            {
                occupant = occupantsByCell[cell];
                return true;
            }

            occupant = null;
            return false;
        }

        public bool TryRegister(UnityEngine.Object occupant, Vector2Int cell)
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
            RaiseChanged(occupant, new[] { cell }, true);
            return true;
        }

        public bool TryRegister(UnityEngine.Object occupant, IEnumerable<Vector2Int> cells)
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
            RaiseChanged(occupant, requested.ToArray(), true);
            return true;
        }

        public bool Unregister(UnityEngine.Object occupant)
        {
            if (occupant == null || !cellsByOccupant.TryGetValue(occupant, out HashSet<Vector2Int> cells))
            {
                return false;
            }

            Vector2Int[] releasedCells = new Vector2Int[cells.Count];
            cells.CopyTo(releasedCells);
            foreach (Vector2Int cell in cells)
            {
                if (occupantsByCell.TryGetValue(cell, out UnityEngine.Object registered) && registered == occupant)
                {
                    occupantsByCell.Remove(cell);
                }
            }

            cellsByOccupant.Remove(occupant);
            RaiseChanged(occupant, releasedCells, false);
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

        private void RaiseChanged(UnityEngine.Object occupant, Vector2Int[] cells, bool occupied)
        {
            Revision++;
            OccupancyChanged?.Invoke(new GridOccupancyChangedEvent(occupant, cells, occupied, Revision));
        }
    }
}
