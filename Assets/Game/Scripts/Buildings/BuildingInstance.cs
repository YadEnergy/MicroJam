using System;
using UnityEngine;

namespace MicroJam.Game
{
    public enum BuildingRemovalReason
    {
        PlayerRemoval,
        DestroyedByDamage,
        ExternalDestruction
    }

    public readonly struct BuildingRemovalEvent
    {
        public BuildingRemovalEvent(BuildingInstance building, BuildingRemovalReason reason, int refundedWood)
        {
            Building = building;
            Reason = reason;
            RefundedWood = refundedWood;
        }

        public BuildingInstance Building { get; }
        public BuildingRemovalReason Reason { get; }
        public int RefundedWood { get; }
    }

    [RequireComponent(typeof(Health), typeof(GridFootprint))]
    public sealed class BuildingInstance : MonoBehaviour
    {
        [SerializeField] private BuildingDefinition definition;
        [SerializeField] private Health health;
        [SerializeField] private GridFootprint footprint;
        [SerializeField] private Vector2Int[] occupiedCells = System.Array.Empty<Vector2Int>();

        private GridOccupancyService occupancy;
        private bool registered;
        private bool removalStarted;
        private bool removalEventRaised;

        public BuildingDefinition Definition => definition;
        public Health Health => health;
        public GridFootprint Footprint => footprint;
        public Vector2Int[] OccupiedCells => (Vector2Int[])occupiedCells.Clone();
        public bool IsRegistered => registered;
        public bool BlocksPlayer => definition != null && definition.BlocksPlayer;
        public bool BlocksDinosaur => definition != null && definition.BlocksDinosaur;
        public bool RemovalStarted => removalStarted;
        public int RemovalRefundWood => definition != null ? definition.RemovalRefundWood : 0;

        public event Action<BuildingRemovalEvent> Removing;

        public void Configure(BuildingDefinition configuredDefinition, Health configuredHealth, GridFootprint configuredFootprint)
        {
            definition = configuredDefinition;
            health = configuredHealth;
            footprint = configuredFootprint;
            occupiedCells = System.Array.Empty<Vector2Int>();
            removalStarted = false;
            removalEventRaised = false;
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
            removalStarted = false;
            removalEventRaised = false;
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

        public bool TryRemoveByPlayer(PlayerResourceWallet wallet)
        {
            int refund = RemovalRefundWood;
            if (removalStarted || !registered || (refund > 0 && wallet == null))
            {
                return false;
            }

            if (!BeginRemoval(BuildingRemovalReason.PlayerRemoval, refund))
            {
                return false;
            }

            if (refund > 0 && !wallet.AddWood(refund))
            {
                Debug.LogWarning($"{name} was removed, but its {refund} Wood refund could not be added because the wallet is already full.", this);
            }

            return true;
        }

        public bool TryDestroyWithoutRefund()
        {
            return BeginRemoval(BuildingRemovalReason.DestroyedByDamage, 0);
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

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        private void OnDestroy()
        {
            ReleaseOccupancy();
            RaiseRemoving(BuildingRemovalReason.ExternalDestruction, 0);
        }

        private bool BeginRemoval(BuildingRemovalReason reason, int refund)
        {
            if (removalStarted)
            {
                return false;
            }

            removalStarted = true;
            ReleaseOccupancy();
            RaiseRemoving(reason, refund);
            Destroy(gameObject);
            return true;
        }

        private void HandleDied(DeathEvent _) => TryDestroyWithoutRefund();

        private void RaiseRemoving(BuildingRemovalReason reason, int refund)
        {
            if (removalEventRaised)
            {
                return;
            }

            removalEventRaised = true;
            Removing?.Invoke(new BuildingRemovalEvent(this, reason, refund));
        }

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
