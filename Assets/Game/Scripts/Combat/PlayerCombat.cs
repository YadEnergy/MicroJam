using System;
using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    public readonly struct PlayerMeleeHitEvent
    {
        public PlayerMeleeHitEvent(Health targetHealth, float appliedDamage)
        {
            TargetHealth = targetHealth;
            AppliedDamage = appliedDamage;
        }

        public Health TargetHealth { get; }
        public GameObject Target => TargetHealth != null ? TargetHealth.gameObject : null;
        public float AppliedDamage { get; }
    }

    public sealed class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health health;
        [SerializeField] private PlayerFacing facing;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private SpriteRenderer attackFeedback;

        [Header("Melee")]
        [SerializeField, Min(0.01f)] private float attackDamage = 5f;
        [SerializeField, Min(0.01f)] private float attackRange = 1.5f;
        [SerializeField, Range(0.1f, 360f)] private float attackArcDegrees = 90f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 0.4f;
        [SerializeField] private LayerMask targetLayers;
        [SerializeField, Min(1)] private int overlapCapacity = 32;

        [Header("Placeholder Feedback / Debug")]
        [SerializeField, Min(0f)] private float feedbackDuration = 0.08f;
        [SerializeField] private bool showDebugGizmos;

        private Collider2D[] overlapResults;
        private readonly HashSet<Health> hitTargets = new();
        private bool attackHeld;
        private float nextAttackTime;
        private float hideFeedbackTime;
        private ContactFilter2D targetFilter;

        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackArcDegrees => attackArcDegrees;
        public float AttackCooldown => attackCooldown;
        public LayerMask TargetLayers => targetLayers;
        public Transform AttackOrigin => attackOrigin;
        public SpriteRenderer AttackFeedback => attackFeedback;
        public Health Health => health;
        public PlayerFacing Facing => facing;
        public bool AttackHeld => attackHeld;

        public event Action AttackPerformed;
        public event Action<PlayerMeleeHitEvent> SuccessfulHit;

        public void Configure(
            Health configuredHealth,
            PlayerFacing configuredFacing,
            Transform configuredAttackOrigin,
            SpriteRenderer configuredFeedback,
            LayerMask configuredTargetLayers,
            float damage,
            float range,
            float arcDegrees,
            float cooldown)
        {
            health = configuredHealth;
            facing = configuredFacing;
            attackOrigin = configuredAttackOrigin;
            attackFeedback = configuredFeedback;
            targetLayers = configuredTargetLayers;
            attackDamage = Mathf.Max(0.01f, damage);
            attackRange = Mathf.Max(0.01f, range);
            attackArcDegrees = Mathf.Clamp(arcDegrees, 0.1f, 360f);
            attackCooldown = Mathf.Max(0.01f, cooldown);
            PrepareQuery();
            SetFeedbackVisible(false);
        }

        public void SetAttackHeld(bool held)
        {
            if (health != null && health.IsDead)
            {
                attackHeld = false;
                return;
            }

            bool pressedThisFrame = held && !attackHeld;
            attackHeld = held;
            if (pressedThisFrame)
            {
                TryAttackNow(out _);
            }
        }

        public bool TryAttackNow(out int successfulHitCount)
        {
            successfulHitCount = 0;
            if ((health != null && health.IsDead) || Time.time < nextAttackTime)
            {
                return false;
            }

            nextAttackTime = Time.time + attackCooldown;
            successfulHitCount = ExecuteAttack();
            AttackPerformed?.Invoke();
            ShowFeedback();
            return true;
        }

        private void Awake()
        {
            PrepareQuery();
            SetFeedbackVisible(false);
        }

        private void Update()
        {
            if (health != null && health.IsDead)
            {
                attackHeld = false;
            }

            if (attackHeld && Time.time >= nextAttackTime)
            {
                TryAttackNow(out _);
            }

            if (attackFeedback != null && attackFeedback.enabled && Time.time >= hideFeedbackTime)
            {
                SetFeedbackVisible(false);
            }
        }

        private int ExecuteAttack()
        {
            if (attackOrigin == null || facing == null)
            {
                return 0;
            }

            if (overlapResults == null || overlapResults.Length != overlapCapacity)
            {
                overlapResults = new Collider2D[overlapCapacity];
            }

            hitTargets.Clear();
            Vector2 origin = attackOrigin.position;
            Vector2 forward = facing.FacingDirection;
            int resultCount = Physics2D.OverlapCircle(origin, attackRange, targetFilter, overlapResults);
            int successfulHits = 0;

            for (int i = 0; i < resultCount; i++)
            {
                Collider2D candidate = overlapResults[i];
                if (candidate == null)
                {
                    continue;
                }

                Vector2 hitPoint = candidate.ClosestPoint(origin);
                Vector2 toTarget = hitPoint - origin;
                if (toTarget.sqrMagnitude <= Mathf.Epsilon)
                {
                    toTarget = (Vector2)candidate.bounds.center - origin;
                }

                if (toTarget.sqrMagnitude > attackRange * attackRange ||
                    Vector2.Angle(forward, toTarget) > attackArcDegrees * 0.5f)
                {
                    continue;
                }

                Health targetHealth = candidate.GetComponentInParent<Health>();
                if (targetHealth == null || !hitTargets.Add(targetHealth))
                {
                    continue;
                }

                IDamageable damageable = targetHealth;
                if (!damageable.TryTakeDamage(new DamageContext(attackDamage, gameObject), out float appliedDamage))
                {
                    continue;
                }

                successfulHits++;
                SuccessfulHit?.Invoke(new PlayerMeleeHitEvent(targetHealth, appliedDamage));
            }

            Array.Clear(overlapResults, 0, resultCount);
            return successfulHits;
        }

        private void PrepareQuery()
        {
            overlapCapacity = Mathf.Max(1, overlapCapacity);
            overlapResults = new Collider2D[overlapCapacity];
            targetFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true
            };
        }

        private void ShowFeedback()
        {
            hideFeedbackTime = Time.time + feedbackDuration;
            SetFeedbackVisible(true);
        }

        private void SetFeedbackVisible(bool visible)
        {
            if (attackFeedback != null)
            {
                attackFeedback.enabled = visible;
            }
        }

        private void OnValidate()
        {
            attackDamage = Mathf.Max(0.01f, attackDamage);
            attackRange = Mathf.Max(0.01f, attackRange);
            attackArcDegrees = Mathf.Clamp(attackArcDegrees, 0.1f, 360f);
            attackCooldown = Mathf.Max(0.01f, attackCooldown);
            overlapCapacity = Mathf.Max(1, overlapCapacity);
            feedbackDuration = Mathf.Max(0f, feedbackDuration);
            PrepareQuery();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos || attackOrigin == null)
            {
                return;
            }

            Vector2 origin = attackOrigin.position;
            Vector2 forward = facing != null ? facing.FacingDirection : (Vector2)transform.right;
            float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            float halfArc = attackArcDegrees * 0.5f;
            const int segments = 20;

            Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.9f);
            Vector3 previous = origin + Rotate(Vector2.right, baseAngle - halfArc) * attackRange;
            Gizmos.DrawLine(origin, previous);
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(baseAngle - halfArc, baseAngle + halfArc, i / (float)segments);
                Vector3 next = origin + Rotate(Vector2.right, angle) * attackRange;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.DrawLine(origin, previous);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + forward * attackRange);
            Gizmos.DrawSphere(origin, 0.06f);
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }
    }
}
