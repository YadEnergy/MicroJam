using UnityEngine;

namespace MicroJam.Game
{
    public enum ResourceNodeType
    {
        Tree,
        Rock,
        Bush
    }

    [RequireComponent(typeof(Health))]
    public sealed class ResourceNode : MonoBehaviour
    {
        [SerializeField] private ResourceNodeType nodeType;
        [SerializeField] private Health health;

        [Header("Tree / Rock Reward")]
        [SerializeField, Min(1)] private int resourcePerSuccessfulHit = 1;

        [Header("Bush Healing")]
        [SerializeField, Range(0f, 1f)] private float healPercentPerSuccessfulHit = 0.1f;

        private ResourcePopulationManager populationManager;
        private Vector2Int occupiedCell;
        private bool registered;
        private bool deathProcessed;
        private bool replacementSpawn;

        public ResourceNodeType NodeType => nodeType;
        public Health Health => health;
        public int ResourcePerSuccessfulHit => resourcePerSuccessfulHit;
        public float HealPercentPerSuccessfulHit => healPercentPerSuccessfulHit;
        public Vector2Int OccupiedCell => occupiedCell;
        public bool IsRegistered => registered;
        public bool IsReplacementSpawn => replacementSpawn;
        public ResourcePopulationManager PopulationManager => populationManager;

        public void Configure(
            ResourceNodeType configuredType,
            Health configuredHealth,
            int configuredResourcePerHit,
            float configuredHealPercent)
        {
            nodeType = configuredType;
            health = configuredHealth;
            resourcePerSuccessfulHit = Mathf.Max(1, configuredResourcePerHit);
            healPercentPerSuccessfulHit = Mathf.Clamp01(configuredHealPercent);
        }

        public bool InitializeSpawn(ResourcePopulationManager manager, Vector2Int cell, bool isReplacement)
        {
            if (manager == null || registered)
            {
                return false;
            }

            populationManager = manager;
            occupiedCell = cell;
            replacementSpawn = isReplacement;
            deathProcessed = false;

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            health?.ResetHealth();
            HealthBar bar = GetComponentInChildren<HealthBar>(true);
            bar?.ResetForSpawn();
            registered = populationManager.RegisterSpawnedNode(this);
            return registered;
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }

        private void OnEnable()
        {
            deathProcessed = false;
            if (health != null)
            {
                health.DamageReceived += HandleDamageReceived;
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DamageReceived -= HandleDamageReceived;
                health.Died -= HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (registered && !deathProcessed && populationManager != null && populationManager.isActiveAndEnabled)
            {
                populationManager.NotifyNodeRemoved(this);
            }
        }

        private void HandleDamageReceived(DamageReceivedEvent damage)
        {
            if (damage.AppliedAmount <= 0f || damage.Source == null ||
                !damage.Source.TryGetComponent(out PlayerResourceWallet wallet))
            {
                return;
            }

            switch (nodeType)
            {
                case ResourceNodeType.Tree:
                    wallet.AddWood(resourcePerSuccessfulHit);
                    break;
                case ResourceNodeType.Rock:
                    wallet.AddStone(resourcePerSuccessfulHit);
                    break;
                case ResourceNodeType.Bush:
                    Health playerHealth = damage.Source.GetComponent<Health>();
                    if (playerHealth != null && !playerHealth.IsDead)
                    {
                        playerHealth.TryHeal(playerHealth.MaxHealth * healPercentPerSuccessfulHit);
                    }
                    break;
            }
        }

        private void HandleDied(DeathEvent death)
        {
            if (deathProcessed)
            {
                return;
            }

            deathProcessed = true;
            if (registered && populationManager != null)
            {
                populationManager.NotifyNodeRemoved(this);
            }

            registered = false;
            Destroy(gameObject);
        }

        private void OnValidate()
        {
            resourcePerSuccessfulHit = Mathf.Max(1, resourcePerSuccessfulHit);
            healPercentPerSuccessfulHit = Mathf.Clamp01(healPercentPerSuccessfulHit);
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }
    }
}
