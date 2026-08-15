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
        [SerializeField, Min(0f)] private float campfireStoppingDistance = 0.75f;
        [SerializeField, Min(1)] private int spawnCost = 3;

        public int SpawnCost => spawnCost;

        public void Initialize() => targeting?.GetTarget(false);

        private void Awake()
        {
            health ??= GetComponent<Health>();
            movement ??= GetComponent<DinosaurMovement>() ?? gameObject.AddComponent<DinosaurMovement>();
            attack ??= GetComponent<DinosaurAttack>() ?? gameObject.AddComponent<DinosaurAttack>();
            targeting ??= GetComponent<DinosaurTargeting>() ?? gameObject.AddComponent<DinosaurTargeting>();
            IgnoreWorldBoundaryCollision();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void FixedUpdate()
        {
            if (health == null || health.IsDead || movement == null || attack == null || targeting == null) return;

            Health retaliatingPlayer = targeting.RetaliatingPlayer;
            bool canCounterAttack = retaliatingPlayer != null && attack.IsWithinRange(retaliatingPlayer);
            Health target = targeting.GetTarget(canCounterAttack);
            if (target == null || target.IsDead) return;

            if (attack.IsWithinRange(target))
            {
                attack.TryAttack(target);
                return;
            }

            float stoppingDistance = target == targeting.CampfireHealth
                ? campfireStoppingDistance
                : attack.AttackRange;
            movement.MoveTowards(target.transform.position, stoppingDistance);
        }

        private static void IgnoreWorldBoundaryCollision()
        {
            int dinosaurLayer = GameLayers.DinosaurIndex;
            int boundaryLayer = GameLayers.WorldBoundaryIndex;
            if (dinosaurLayer >= 0 && boundaryLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(dinosaurLayer, boundaryLayer, true);
            }
        }

        private void OnDied(DeathEvent _) => Destroy(gameObject);

        private void OnValidate()
        {
            campfireStoppingDistance = Mathf.Max(0f, campfireStoppingDistance);
            spawnCost = Mathf.Max(1, spawnCost);
        }
    }
}
