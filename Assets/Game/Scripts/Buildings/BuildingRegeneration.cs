using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Health))]
    public sealed class BuildingRegeneration : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField, Min(0f)] private float regenerationDelay = 10f;
        [SerializeField, Min(0f)] private float regenerationPerSecond = 10f;

        private float lastSuccessfulDamageTime;

        public Health Health => health;
        public float RegenerationDelay => regenerationDelay;
        public float RegenerationPerSecond => regenerationPerSecond;
        public float LastSuccessfulDamageTime => lastSuccessfulDamageTime;
        public bool IsRegenerating { get; private set; }

        public void Configure(Health configuredHealth, float configuredDelay, float configuredPerSecond)
        {
            health = configuredHealth;
            regenerationDelay = Mathf.Max(0f, configuredDelay);
            regenerationPerSecond = Mathf.Max(0f, configuredPerSecond);
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            lastSuccessfulDamageTime = Time.time;
        }

        private void OnEnable()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (health != null)
            {
                health.DamageReceived += HandleDamageReceived;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DamageReceived -= HandleDamageReceived;
            }

            IsRegenerating = false;
        }

        private void Update()
        {
            IsRegenerating = false;
            if (health == null || health.IsDead || health.CurrentHealth >= health.MaxHealth || regenerationPerSecond <= 0f ||
                Time.time < lastSuccessfulDamageTime + regenerationDelay)
            {
                return;
            }

            IsRegenerating = health.TryHeal(regenerationPerSecond * Time.deltaTime);
        }

        private void HandleDamageReceived(DamageReceivedEvent damage)
        {
            if (damage.AppliedAmount > 0f)
            {
                lastSuccessfulDamageTime = Time.time;
                IsRegenerating = false;
            }
        }

        private void OnValidate()
        {
            regenerationDelay = Mathf.Max(0f, regenerationDelay);
            regenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }
    }
}
