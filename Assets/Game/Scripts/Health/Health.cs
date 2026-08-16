using System;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0.01f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float currentHealth = 100f;
        [SerializeField] private bool initializeAtMaxOnAwake = true;

        private bool isDead;
        private bool isInvulnerable;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        public bool IsDead => isDead;
        public bool IsInvulnerable => isInvulnerable;

        public event Action<HealthChangedEvent> HealthChanged;
        public event Action<DamageReceivedEvent> DamageReceived;
        public event Action<HealingReceivedEvent> HealingReceived;
        public event Action<DeathEvent> Died;

        public void Configure(float configuredMaxHealth, bool initializeFull = true)
        {
            maxHealth = IsPositiveFinite(configuredMaxHealth) ? configuredMaxHealth : 1f;
            initializeAtMaxOnAwake = initializeFull;
            currentHealth = initializeFull ? maxHealth : Mathf.Clamp(currentHealth, 0f, maxHealth);
            isDead = currentHealth <= 0f;
        }

        public bool TryTakeDamage(DamageContext context, out float appliedDamage)
        {
            appliedDamage = 0f;
            if (isDead || isInvulnerable || !IsPositiveFinite(context.Amount) || !HasValidState())
            {
                return false;
            }

            float previousHealth = currentHealth;
            appliedDamage = Mathf.Min(context.Amount, previousHealth);
            if (appliedDamage <= 0f)
            {
                return false;
            }

            currentHealth = Mathf.Clamp(previousHealth - appliedDamage, 0f, maxHealth);
            DamageReceivedEvent damageEvent = new(context, appliedDamage, previousHealth, currentHealth);
            HealthChanged?.Invoke(new HealthChangedEvent(previousHealth, currentHealth, maxHealth));
            DamageReceived?.Invoke(damageEvent);

            if (currentHealth <= 0f && !isDead)
            {
                isDead = true;
                Died?.Invoke(new DeathEvent(damageEvent));
            }

            return true;
        }

        public bool TryTakeDamage(DamageContext context) => TryTakeDamage(context, out _);

        public bool TryHeal(float requestedAmount, out float appliedHealing)
        {
            appliedHealing = 0f;
            if (isDead || !IsPositiveFinite(requestedAmount) || !HasValidState() || currentHealth >= maxHealth)
            {
                return false;
            }

            float previousHealth = currentHealth;
            appliedHealing = Mathf.Min(requestedAmount, maxHealth - previousHealth);
            if (appliedHealing <= 0f)
            {
                return false;
            }

            currentHealth = Mathf.Clamp(previousHealth + appliedHealing, 0f, maxHealth);
            HealthChanged?.Invoke(new HealthChangedEvent(previousHealth, currentHealth, maxHealth));
            HealingReceived?.Invoke(new HealingReceivedEvent(requestedAmount, appliedHealing, previousHealth, currentHealth));
            return true;
        }

        public bool TryHeal(float requestedAmount) => TryHeal(requestedAmount, out _);

        public void SetInvulnerable(bool value) => isInvulnerable = value;

        public void ResetHealth()
        {
            float previousHealth = currentHealth;
            bool wasDead = isDead;
            currentHealth = maxHealth;
            isDead = false;
            if (wasDead || !Mathf.Approximately(previousHealth, currentHealth))
            {
                HealthChanged?.Invoke(new HealthChangedEvent(previousHealth, currentHealth, maxHealth));
            }
        }

        public bool Revive(float restoredHealth)
        {
            if (!isDead || !IsPositiveFinite(restoredHealth) || !IsPositiveFinite(maxHealth))
            {
                return false;
            }

            float previousHealth = currentHealth;
            currentHealth = Mathf.Clamp(restoredHealth, 0.01f, maxHealth);
            isDead = false;
            HealthChanged?.Invoke(new HealthChangedEvent(previousHealth, currentHealth, maxHealth));
            return true;
        }

        private void Awake()
        {
            maxHealth = IsPositiveFinite(maxHealth) ? maxHealth : 1f;
            if (initializeAtMaxOnAwake || !IsFinite(currentHealth))
            {
                currentHealth = maxHealth;
            }
            else
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            }

            isDead = currentHealth <= 0f;
        }

        private bool HasValidState()
        {
            return IsPositiveFinite(maxHealth) && IsFinite(currentHealth) && currentHealth > 0f && currentHealth <= maxHealth;
        }

        private static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnValidate()
        {
            maxHealth = IsPositiveFinite(maxHealth) ? maxHealth : 1f;
            currentHealth = IsFinite(currentHealth) ? Mathf.Clamp(currentHealth, 0f, maxHealth) : maxHealth;
        }
    }
}
