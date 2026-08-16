using System;
using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class DinosaurAttack : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private SpriteRenderer attackFeedback;
        [SerializeField, Min(0.01f)] private float attackRange = 1.5f;
        [SerializeField, Min(0f)] private float rangeTolerance = 0.05f;
        [SerializeField, Min(0.01f)] private float attackDamage = 8f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1f;
        [SerializeField, Min(0f)] private float feedbackDuration = 0.1f;

        private float nextAttackTime;
        private float hideFeedbackTime;

        public float AttackRange => attackRange;
        public float RangeTolerance => rangeTolerance;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public event Action<Health> SuccessfulAttack;

        public void Configure(Rigidbody2D configuredBody, SpriteRenderer configuredVisual, Transform configuredOrigin, SpriteRenderer configuredFeedback)
        {
            body = configuredBody;
            visual = configuredVisual;
            attackOrigin = configuredOrigin;
            attackFeedback = configuredFeedback;
        }

        private void Awake()
        {
            body ??= GetComponent<Rigidbody2D>();
            visual ??= transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            attackOrigin ??= transform.Find("Combat/AttackOrigin");
            attackFeedback ??= transform.Find("Combat/AttackOrigin/AttackVisual")?.GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (attackFeedback != null && attackFeedback.enabled && Time.time >= hideFeedbackTime) attackFeedback.enabled = false;
        }

        public bool IsWithinRange(Health target) => GetSurfaceDistance(target) <= attackRange + rangeTolerance;

        public bool CanAttackFrom(Vector2 bodyPosition, Health target, float reservedStoppingDistance = 0f)
        {
            float permittedRange = Mathf.Max(0f, attackRange + rangeTolerance - reservedStoppingDistance);
            return GetSurfaceDistanceFrom(bodyPosition, target) <= permittedRange;
        }

        public bool TryAttack(Health target)
        {
            if (target == null || target.IsDead || Time.time < nextAttackTime || !IsWithinRange(target)) return false;

            nextAttackTime = Time.time + attackCooldown;
            if (!target.TryTakeDamage(new DamageContext(attackDamage, gameObject))) return false;

            ShowFeedback(GetAttackOffset(target).normalized);
            SuccessfulAttack?.Invoke(target);
            return true;
        }

        private Vector2 GetAttackOffset(Health target)
        {
            if (target == null || body == null) return Vector2.positiveInfinity;
            Collider2D targetCollider = target.GetComponent<Collider2D>();
            Vector2 point = targetCollider != null ? targetCollider.ClosestPoint(body.position) : target.transform.position;
            return point - body.position;
        }

        public float GetSurfaceDistance(Health target)
        {
            if (target == null || target.IsDead || body == null) return float.PositiveInfinity;
            Collider2D sourceCollider = GetComponent<Collider2D>();
            Collider2D targetCollider = target.GetComponent<Collider2D>();
            if (sourceCollider != null && targetCollider != null)
            {
                return Mathf.Max(0f, sourceCollider.Distance(targetCollider).distance);
            }

            return GetAttackOffset(target).magnitude;
        }

        public float GetSurfaceDistanceFrom(Vector2 candidateBodyPosition, Health target)
        {
            if (target == null || target.IsDead || body == null) return float.PositiveInfinity;
            Collider2D sourceCollider = GetComponent<Collider2D>();
            Collider2D targetCollider = target.GetComponent<Collider2D>();
            if (sourceCollider == null || targetCollider == null)
            {
                return Vector2.Distance(candidateBodyPosition, target.transform.position);
            }

            // Evaluate the authored source collider as if the Rigidbody were at the candidate cell.
            // Translating the target query into the collider's current local frame avoids moving physics state.
            Vector2 translation = candidateBodyPosition - body.position;
            Vector2 targetSurface = targetCollider.ClosestPoint(candidateBodyPosition);
            Vector2 sourceSurface = sourceCollider.ClosestPoint(targetSurface - translation) + translation;
            return Vector2.Distance(sourceSurface, targetSurface);
        }

        private void ShowFeedback(Vector2 direction)
        {
            if (attackFeedback == null || direction.sqrMagnitude <= Mathf.Epsilon) return;
            Vector2 localDirection = transform.InverseTransformDirection(direction);
            float angle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            if (attackOrigin != null)
            {
                attackOrigin.localRotation = Quaternion.Euler(0f, 0f, angle);
                attackFeedback.transform.localPosition = Vector3.right * attackRange * 0.5f;
                attackFeedback.transform.localRotation = Quaternion.identity;
            }

            attackFeedback.enabled = true;
            hideFeedbackTime = Time.time + feedbackDuration;
        }

        private void OnValidate()
        {
            attackRange = Mathf.Max(0.01f, attackRange);
            rangeTolerance = Mathf.Max(0f, rangeTolerance);
            attackDamage = Mathf.Max(0.01f, attackDamage);
            attackCooldown = Mathf.Max(0.01f, attackCooldown);
            feedbackDuration = Mathf.Max(0f, feedbackDuration);
        }
    }
}
