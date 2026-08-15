using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Health))]
    public sealed class CampfireInteraction : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField, Min(0)] private int repairWoodCost = 20;
        [SerializeField, Range(0f, 1f)] private float repairHealthPercent = 0.1f;

        public Health Health => health;
        public int RepairWoodCost => repairWoodCost;
        public float RepairHealthPercent => repairHealthPercent;
        public float RequestedRepairHealth => health != null ? health.MaxHealth * repairHealthPercent : 0f;

        public void Configure(Health configuredHealth, int configuredCost, float configuredPercent)
        {
            health = configuredHealth;
            repairWoodCost = Mathf.Max(0, configuredCost);
            repairHealthPercent = Mathf.Clamp01(configuredPercent);
        }

        public bool CanRepair(PlayerResourceWallet wallet)
        {
            return health != null && !health.IsDead && health.CurrentHealth < health.MaxHealth && wallet != null &&
                   wallet.CanAffordWood(repairWoodCost);
        }

        public bool TryRepair(PlayerResourceWallet wallet)
        {
            if (!CanRepair(wallet))
            {
                return false;
            }

            bool spent = repairWoodCost == 0 || wallet.SpendWood(repairWoodCost);
            if (!spent)
            {
                return false;
            }

            if (health.TryHeal(RequestedRepairHealth))
            {
                return true;
            }

            if (repairWoodCost > 0)
            {
                wallet.AddWood(repairWoodCost);
            }

            return false;
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }

        private void OnValidate()
        {
            repairWoodCost = Mathf.Max(0, repairWoodCost);
            repairHealthPercent = Mathf.Clamp01(repairHealthPercent);
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }
    }
}
