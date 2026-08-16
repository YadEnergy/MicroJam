using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Health))]
    public sealed class CampfireInteraction : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField, Min(0)] private int repairWoodCost = 20;
        [SerializeField, Range(0f, 1f)] private float repairHealthPercent = 0.1f;

        [Header("Burning ambience")]
        [SerializeField, Min(0f)] private float fullVolumeDistance = 1.5f;
        [SerializeField, Min(0.01f)] private float maxAudibleDistance = 12f;
        [SerializeField, Min(0f)] private float volumeChangeSpeed = 4f;

        private AudioSource burningSource;
        private PlayerMovement player;
        private Camera gameplayCamera;
        private Collider2D campfireCollider;

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

            campfireCollider = GetComponent<Collider2D>();
            burningSource = GetComponent<AudioSource>();
            if (burningSource == null) burningSource = gameObject.AddComponent<AudioSource>();
            burningSource.volume = 0f;
        }

        private void OnEnable()
        {
            if (health != null) health.DamageReceived += HandleDamageReceived;
            GameAudio.ConfigureLoopingSource(burningSource, GameSound.CampfireBurning);
        }

        private void OnDisable()
        {
            if (health != null) health.DamageReceived -= HandleDamageReceived;
            if (burningSource != null) burningSource.Stop();
        }

        private void Update()
        {
            if (burningSource == null) return;
            if (burningSource.clip == null && !GameAudio.ConfigureLoopingSource(burningSource, GameSound.CampfireBurning))
            {
                burningSource.volume = 0f;
                return;
            }

            player ??= FindFirstObjectByType<PlayerMovement>();
            gameplayCamera ??= Camera.main;
            float localVolume = CalculateBurningVolume();
            float targetVolume = GameAudio.GetSoundVolume(GameSound.CampfireBurning, localVolume);
            burningSource.volume = volumeChangeSpeed <= 0f
                ? targetVolume
                : Mathf.MoveTowards(burningSource.volume, targetVolume, volumeChangeSpeed * Time.deltaTime);
        }

        private float CalculateBurningVolume()
        {
            if (player == null || player.Health == null || player.Health.IsDead || !IsVisibleInGameplayCamera()) return 0f;

            Vector2 playerPosition = player.transform.position;
            Vector2 nearestCampfirePoint = campfireCollider != null
                ? campfireCollider.ClosestPoint(playerPosition)
                : (Vector2)transform.position;
            float distance = Vector2.Distance(playerPosition, nearestCampfirePoint);
            return 1f - Mathf.InverseLerp(fullVolumeDistance, maxAudibleDistance, distance);
        }

        private bool IsVisibleInGameplayCamera()
        {
            if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled) return false;
            Bounds bounds = campfireCollider != null
                ? campfireCollider.bounds
                : new Bounds(transform.position, Vector3.one * 0.1f);
            Vector3 min = gameplayCamera.WorldToViewportPoint(bounds.min);
            Vector3 max = gameplayCamera.WorldToViewportPoint(bounds.max);
            if (min.z <= 0f && max.z <= 0f) return false;
            return max.x >= 0f && min.x <= 1f && max.y >= 0f && min.y <= 1f;
        }

        private static void HandleDamageReceived(DamageReceivedEvent damage)
        {
            if (damage.AppliedAmount > 0f) GameAudio.Play(GameSound.CampfireHitting);
        }

        private void OnValidate()
        {
            repairWoodCost = Mathf.Max(0, repairWoodCost);
            repairHealthPercent = Mathf.Clamp01(repairHealthPercent);
            fullVolumeDistance = Mathf.Max(0f, fullVolumeDistance);
            maxAudibleDistance = Mathf.Max(fullVolumeDistance + 0.01f, maxAudibleDistance);
            volumeChangeSpeed = Mathf.Max(0f, volumeChangeSpeed);
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }
    }
}
