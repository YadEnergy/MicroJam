using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Health))]
    public sealed class DinosaurTargeting : MonoBehaviour
    {
        [SerializeField] private Health health;
        private Health campfireHealth;
        private Health retaliatingPlayer;

        public Health CampfireHealth => campfireHealth;
        public Health RetaliatingPlayer => retaliatingPlayer;

        private void Awake()
        {
            health ??= GetComponent<Health>();
            FindCampfire();
        }

        private void OnEnable()
        {
            if (health != null) health.DamageReceived += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.DamageReceived -= OnDamaged;
        }

        public Health GetTarget(bool canCounterAttackPlayer)
        {
            FindCampfire();
            if (retaliatingPlayer != null && !retaliatingPlayer.IsDead && canCounterAttackPlayer)
            {
                return retaliatingPlayer;
            }

            retaliatingPlayer = null;
            return campfireHealth;
        }

        private void OnDamaged(DamageReceivedEvent damage)
        {
            if (damage.Source == null || damage.Source.layer != GameLayers.PlayerIndex) return;
            Health player = damage.Source.GetComponent<Health>();
            if (player != null && !player.IsDead) retaliatingPlayer = player;
        }

        private void FindCampfire()
        {
            if (campfireHealth != null) return;
            GameObject campfire = GameObject.Find("Campfire");
            campfireHealth = campfire != null ? campfire.GetComponent<Health>() : null;
        }
    }
}
