using System;
using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Health), typeof(BuildingInstance))]
    public sealed class TowerCombat : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Transform turretPivot;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private TowerProjectile projectilePrefab;
        [SerializeField, Min(0.01f)] private float attackRange = 30f;
        [SerializeField, Min(0.01f)] private float attackDamage = 5f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 0.5f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 20f;
        [SerializeField] private bool showRangeGizmo;
        [SerializeField] private bool showTargetGizmo;

        private WorldGridService worldGrid;
        private BuildingInstance building;
        private DinosaurAgent currentTarget;
        private float nextAttackTime;

        public Health Health => health;
        public Transform TurretPivot => turretPivot;
        public Transform ProjectileSpawnPoint => projectileSpawnPoint;
        public TowerProjectile ProjectilePrefab => projectilePrefab;
        public float AttackRange => attackRange;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public float ProjectileSpeed => projectileSpeed;
        public DinosaurAgent CurrentTarget => currentTarget;
        public event Action<TowerProjectile> ProjectileFired;

        public void Configure(
            Health configuredHealth,
            Transform configuredTurretPivot,
            Transform configuredSpawnPoint,
            TowerProjectile configuredProjectilePrefab,
            float configuredRange,
            float configuredDamage,
            float configuredCooldown,
            float configuredProjectileSpeed)
        {
            health = configuredHealth;
            turretPivot = configuredTurretPivot;
            projectileSpawnPoint = configuredSpawnPoint;
            projectilePrefab = configuredProjectilePrefab;
            attackRange = Mathf.Max(0.01f, configuredRange);
            attackDamage = Mathf.Max(0.01f, configuredDamage);
            attackCooldown = Mathf.Max(0.01f, configuredCooldown);
            projectileSpeed = Mathf.Max(0.01f, configuredProjectileSpeed);
        }

        private void Awake()
        {
            health ??= GetComponent<Health>();
            building = GetComponent<BuildingInstance>();
            worldGrid = FindFirstObjectByType<WorldGridService>();
        }

        private void Update()
        {
            if (health == null || health.IsDead || (building != null && building.RemovalStarted))
            {
                currentTarget = null;
                return;
            }

            if (!IsValidTarget(currentTarget)) AcquireNearestTarget();
            if (currentTarget == null) return;

            Vector2 targetPosition = GetTargetPosition(currentTarget);
            FaceTarget(targetPosition);
            if (Time.time >= nextAttackTime) FireAtCurrentTarget();
        }

        public bool AcquireNearestTarget()
        {
            currentTarget = null;
            float bestDistanceSquared = float.PositiveInfinity;
            foreach (DinosaurAgent candidate in DinosaurRegistry.ActiveDinosaurs)
            {
                if (!IsValidTarget(candidate)) continue;
                float distanceSquared = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    currentTarget = candidate;
                }
            }

            return currentTarget != null;
        }

        public bool FireAtCurrentTarget()
        {
            if (!IsValidTarget(currentTarget) || projectilePrefab == null || projectileSpawnPoint == null ||
                Time.time < nextAttackTime)
            {
                return false;
            }

            TowerProjectile projectile = Instantiate(
                projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
            projectile.Initialize(currentTarget, attackDamage, projectileSpeed, gameObject);
            nextAttackTime = Time.time + attackCooldown;
            ProjectileFired?.Invoke(projectile);
            return true;
        }

        public bool IsValidTarget(DinosaurAgent candidate)
        {
            if (candidate == null || candidate.Health == null || candidate.Health.IsDead || worldGrid == null || worldGrid.Config == null)
            {
                return false;
            }

            Vector2 position = candidate.transform.position;
            if (!worldGrid.Config.IsCellInsidePlayableArea(worldGrid.WorldToCell(position))) return false;
            return (position - (Vector2)transform.position).sqrMagnitude <= attackRange * attackRange;
        }

        private void FaceTarget(Vector2 targetPosition)
        {
            if (turretPivot == null) return;
            Vector2 direction = targetPosition - (Vector2)turretPivot.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon) return;
            turretPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private static Vector2 GetTargetPosition(DinosaurAgent target)
        {
            Collider2D collider = target != null ? target.GetComponent<Collider2D>() : null;
            return collider != null ? (Vector2)collider.bounds.center : (Vector2)target.transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            if (showRangeGizmo)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.45f);
                Gizmos.DrawWireSphere(transform.position, attackRange);
            }

            if (showTargetGizmo && currentTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position,
                    currentTarget.transform.position);
            }
        }

        private void OnValidate()
        {
            attackRange = Mathf.Max(0.01f, attackRange);
            attackDamage = Mathf.Max(0.01f, attackDamage);
            attackCooldown = Mathf.Max(0.01f, attackCooldown);
            projectileSpeed = Mathf.Max(0.01f, projectileSpeed);
            health ??= GetComponent<Health>();
        }
    }
}
