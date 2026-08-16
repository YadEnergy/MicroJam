using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Health), typeof(DinosaurMovement), typeof(DinosaurAttack))]
    [RequireComponent(typeof(DinosaurTargeting))]
    public sealed class DinosaurAgent : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private DinosaurMovement movement;
        [SerializeField] private DinosaurAttack attack;
        [SerializeField] private DinosaurTargeting targeting;
        [SerializeField, Min(1)] private int spawnCost = 3;

        public int SpawnCost => spawnCost;
        public Health Health => health;
        public DinosaurMovement Movement => movement;
        public DinosaurAttack Attack => attack;
        public DinosaurTargeting Targeting => targeting;

        public void Configure(Health configuredHealth, DinosaurMovement configuredMovement, DinosaurAttack configuredAttack, DinosaurTargeting configuredTargeting)
        {
            health = configuredHealth;
            movement = configuredMovement;
            attack = configuredAttack;
            targeting = configuredTargeting;
        }

        public void Initialize() => targeting?.Initialize();

        private void Awake()
        {
            health ??= GetComponent<Health>();
            movement ??= GetComponent<DinosaurMovement>();
            attack ??= GetComponent<DinosaurAttack>();
            targeting ??= GetComponent<DinosaurTargeting>();
            ConfigureNonBlockingActorCollisions();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnDied;
            DinosaurRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
            DinosaurRegistry.Unregister(this);
        }

        private void FixedUpdate()
        {
            if (health == null || health.IsDead) return;
            targeting?.Tick();
        }

        private static void ConfigureNonBlockingActorCollisions()
        {
            int dinosaurLayer = GameLayers.DinosaurIndex;
            int boundaryLayer = GameLayers.WorldBoundaryIndex;
            int playerLayer = GameLayers.PlayerIndex;
            if (dinosaurLayer >= 0 && boundaryLayer >= 0) Physics2D.IgnoreLayerCollision(dinosaurLayer, boundaryLayer, true);
            if (dinosaurLayer >= 0 && playerLayer >= 0) Physics2D.IgnoreLayerCollision(dinosaurLayer, playerLayer, true);
        }

        private void OnDied(DeathEvent death)
        {
            bool wasKilledByPlayer = death.Source != null && death.Source.GetComponentInParent<PlayerCombat>() != null;
            if (wasKilledByPlayer) PlayerPoints.Add(spawnCost * 5);
            Destroy(gameObject);
        }

        private void OnValidate() => spawnCost = Mathf.Max(1, spawnCost);
    }
}
