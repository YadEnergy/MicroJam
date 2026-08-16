using UnityEngine;

namespace MicroJam.Game
{
    public sealed class TowerProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float hitDistance = 0.15f;
        [SerializeField, Min(0.1f)] private float maxLifetime = 10f;

        private DinosaurAgent target;
        private Health targetHealth;
        private GameObject damageSource;
        private float damage;
        private float speed;
        private float expiresAt;
        private Vector2 lastKnownTargetPosition;
        private bool initialized;

        public DinosaurAgent Target => target;
        public Health TargetHealth => targetHealth;
        public GameObject DamageSource => damageSource;
        public float Damage => damage;
        public float Speed => speed;
        public float HitDistance => hitDistance;
        public float MaxLifetime => maxLifetime;
        public Vector2 LastKnownTargetPosition => lastKnownTargetPosition;
        public bool HasLivingTarget => target != null && targetHealth != null && !targetHealth.IsDead;

        public void Configure(float configuredHitDistance, float configuredMaxLifetime)
        {
            hitDistance = Mathf.Max(0.01f, configuredHitDistance);
            maxLifetime = Mathf.Max(0.1f, configuredMaxLifetime);
        }

        public void Initialize(DinosaurAgent configuredTarget, float configuredDamage, float configuredSpeed, GameObject configuredSource)
        {
            target = configuredTarget;
            targetHealth = configuredTarget != null ? configuredTarget.Health : null;
            damageSource = configuredSource;
            damage = Mathf.Max(0.01f, configuredDamage);
            speed = Mathf.Max(0.01f, configuredSpeed);
            lastKnownTargetPosition = configuredTarget != null ? GetTargetPosition(configuredTarget) : (Vector2)transform.position;
            expiresAt = Time.time + maxLifetime;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            bool targetAlive = target != null && targetHealth != null && !targetHealth.IsDead;
            if (targetAlive) lastKnownTargetPosition = GetTargetPosition(target);

            Vector2 position = transform.position;
            Vector2 offset = lastKnownTargetPosition - position;
            float step = speed * Time.deltaTime;
            if (offset.sqrMagnitude > Mathf.Epsilon)
            {
                float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (offset.magnitude <= Mathf.Max(hitDistance, step))
            {
                transform.position = lastKnownTargetPosition;
                if (targetAlive) targetHealth.TryTakeDamage(new DamageContext(damage, damageSource));
                Destroy(gameObject);
                return;
            }

            transform.position = Vector2.MoveTowards(position, lastKnownTargetPosition, step);
        }

        private static Vector2 GetTargetPosition(DinosaurAgent dinosaur)
        {
            Collider2D collider = dinosaur != null ? dinosaur.GetComponent<Collider2D>() : null;
            return collider != null ? (Vector2)collider.bounds.center : (Vector2)dinosaur.transform.position;
        }

        private void OnValidate()
        {
            hitDistance = Mathf.Max(0.01f, hitDistance);
            maxLifetime = Mathf.Max(0.1f, maxLifetime);
        }
    }
}
