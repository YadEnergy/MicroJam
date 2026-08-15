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
        [SerializeField, Min(0.01f)] private float attackDamage = 8f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1f;
        [SerializeField, Min(0f)] private float feedbackDuration = 0.1f;

        private float nextAttackTime;
        private float hideFeedbackTime;

        public float AttackRange => attackRange;

        private void Awake()
        {
            body ??= GetComponent<Rigidbody2D>();
            visual ??= transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            attackOrigin ??= transform.Find("Combat/AttackOrigin");
            attackFeedback ??= transform.Find("Combat/AttackOrigin/AttackVisual")?.GetComponent<SpriteRenderer>();
            CreatePlaceholderFeedback();
        }

        private void Update()
        {
            if (attackFeedback != null && attackFeedback.enabled && Time.time >= hideFeedbackTime)
            {
                attackFeedback.enabled = false;
            }
        }

        public bool IsWithinRange(Health target) => GetAttackOffset(target).sqrMagnitude <= attackRange * attackRange;

        public void TryAttack(Health target)
        {
            if (target == null || Time.time < nextAttackTime || !IsWithinRange(target))
            {
                return;
            }

            nextAttackTime = Time.time + attackCooldown;
            target.TryTakeDamage(new DamageContext(attackDamage, gameObject));
            ShowFeedback(GetAttackOffset(target).normalized);
        }

        private Vector2 GetAttackOffset(Health target)
        {
            if (target == null || body == null) return Vector2.positiveInfinity;
            Collider2D targetCollider = target.GetComponent<Collider2D>();
            Vector2 point = targetCollider != null ? targetCollider.ClosestPoint(body.position) : target.transform.position;
            return point - body.position;
        }

        private void CreatePlaceholderFeedback()
        {
            if (attackFeedback != null || visual == null) return;
            GameObject feedback = new("AttackVisual (Placeholder)");
            feedback.transform.SetParent(transform, false);
            attackFeedback = feedback.AddComponent<SpriteRenderer>();
            attackFeedback.sprite = visual.sprite;
            attackFeedback.color = new Color(1f, 0.2f, 0.12f, 0.9f);
            attackFeedback.sortingLayerID = visual.sortingLayerID;
            attackFeedback.sortingOrder = visual.sortingOrder + 2;
            attackFeedback.enabled = false;
        }

        private void ShowFeedback(Vector2 direction)
        {
            if (attackFeedback == null || direction.sqrMagnitude <= Mathf.Epsilon) return;
            Transform feedback = attackFeedback.transform;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (attackOrigin != null)
            {
                attackOrigin.localRotation = Quaternion.Euler(0f, 0f, angle);
                feedback.localPosition = Vector3.right * attackRange * 0.5f;
                feedback.localRotation = Quaternion.identity;
            }
            else
            {
                feedback.localPosition = direction * attackRange * 0.5f;
                feedback.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            feedback.localScale = new Vector3(1.3f, 0.2f, 1f);
            attackFeedback.enabled = true;
            hideFeedbackTime = Time.time + feedbackDuration;
        }
    }
}
