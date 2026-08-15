using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Health), typeof(GridFootprint))]
    public sealed class BuildingInstance : MonoBehaviour
    {
        [SerializeField] private BuildingDefinition definition;
        [SerializeField] private Health health;
        [SerializeField] private GridFootprint footprint;
        [SerializeField] private Vector2Int[] occupiedCells = System.Array.Empty<Vector2Int>();

        private GridOccupancyService occupancy;
        private bool registered;

        public BuildingDefinition Definition => definition;
        public Health Health => health;
        public GridFootprint Footprint => footprint;
        public Vector2Int[] OccupiedCells => (Vector2Int[])occupiedCells.Clone();
        public bool IsRegistered => registered;
        public bool BlocksPlayer => definition != null && definition.BlocksPlayer;
        public bool BlocksDinosaur => definition != null && definition.BlocksDinosaur;

        public void Configure(BuildingDefinition configuredDefinition, Health configuredHealth, GridFootprint configuredFootprint)
        {
            definition = configuredDefinition;
            health = configuredHealth;
            footprint = configuredFootprint;
            occupiedCells = System.Array.Empty<Vector2Int>();
        }

        public bool InitializePlacement(
            BuildingDefinition placedDefinition,
            GridOccupancyService configuredOccupancy,
            Vector2Int[] cells)
        {
            if (registered || placedDefinition == null || configuredOccupancy == null || cells == null || cells.Length == 0)
            {
                return false;
            }

            definition = placedDefinition;
            occupancy = configuredOccupancy;
            occupiedCells = (Vector2Int[])cells.Clone();
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (footprint == null)
            {
                footprint = GetComponent<GridFootprint>();
            }

            health?.ResetHealth();
            GetComponentInChildren<HealthBar>(true)?.ResetForSpawn();
            registered = occupancy.TryRegister(this, occupiedCells);
            return registered;
        }

        public bool ReleaseOccupancy()
        {
            if (!registered)
            {
                return false;
            }

            bool released = occupancy != null && occupancy.Unregister(this);
            registered = false;
            return released;
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (footprint == null)
            {
                footprint = GetComponent<GridFootprint>();
            }
        }

        private void OnDestroy() => ReleaseOccupancy();

        private void OnValidate()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (footprint == null)
            {
                footprint = GetComponent<GridFootprint>();
            }
        }
    }
}
