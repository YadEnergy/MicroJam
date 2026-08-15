using UnityEngine;

namespace MicroJam.Game
{
    public readonly struct DamageContext
    {
        public DamageContext(float amount, GameObject source = null)
        {
            Amount = amount;
            Source = source;
        }

        public float Amount { get; }
        public GameObject Source { get; }
    }

    public readonly struct HealthChangedEvent
    {
        public HealthChangedEvent(float previousHealth, float currentHealth, float maxHealth)
        {
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
        }

        public float PreviousHealth { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
    }

    public readonly struct DamageReceivedEvent
    {
        public DamageReceivedEvent(DamageContext context, float appliedAmount, float previousHealth, float currentHealth)
        {
            Context = context;
            AppliedAmount = appliedAmount;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
        }

        public DamageContext Context { get; }
        public float AppliedAmount { get; }
        public float PreviousHealth { get; }
        public float CurrentHealth { get; }
        public GameObject Source => Context.Source;
    }

    public readonly struct HealingReceivedEvent
    {
        public HealingReceivedEvent(float requestedAmount, float appliedAmount, float previousHealth, float currentHealth)
        {
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
        }

        public float RequestedAmount { get; }
        public float AppliedAmount { get; }
        public float PreviousHealth { get; }
        public float CurrentHealth { get; }
    }

    public readonly struct DeathEvent
    {
        public DeathEvent(DamageReceivedEvent killingDamage)
        {
            KillingDamage = killingDamage;
        }

        public DamageReceivedEvent KillingDamage { get; }
        public GameObject Source => KillingDamage.Source;
    }
}
