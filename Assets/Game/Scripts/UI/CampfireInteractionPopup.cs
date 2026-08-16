using UnityEngine;
using UnityEngine.UI;

namespace MicroJam.Game
{
    public sealed class CampfireInteractionPopup : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text repairDescriptionText;
        [SerializeField] private Button repairButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private UIPanelTween panelTween;

        private CampfireInteraction target;
        private PlayerResourceWallet wallet;

        public CampfireInteraction Target => target;
        public Text TitleText => titleText;
        public Text HealthText => healthText;
        public Text RepairDescriptionText => repairDescriptionText;
        public Button RepairButton => repairButton;
        public Button CloseButton => closeButton;
        public UIPanelTween PanelTween => panelTween;
        public bool IsOpen => gameObject.activeSelf;

        public void Configure(Text configuredTitle, Text configuredHealth, Text configuredDescription, Button configuredRepair, Button configuredClose)
        {
            titleText = configuredTitle;
            healthText = configuredHealth;
            repairDescriptionText = configuredDescription;
            repairButton = configuredRepair;
            closeButton = configuredClose;
        }

        public void ConfigureTween(UIPanelTween tween) => panelTween = tween;

        public void Open(CampfireInteraction campfire, PlayerResourceWallet playerWallet)
        {
            Unsubscribe();
            target = campfire;
            wallet = playerWallet;
            Subscribe();
            if (panelTween != null) panelTween.Show();
            else gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            Unsubscribe();
            target = null;
            wallet = null;
            if (panelTween != null) panelTween.Hide();
            else if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public bool RepairCampfire()
        {
            if (target == null || wallet == null || !target.TryRepair(wallet))
            {
                Refresh();
                return false;
            }

            Refresh();
            return true;
        }

        public void Refresh()
        {
            bool exists = target != null && target.Health != null;
            if (titleText != null)
            {
                titleText.text = "Campfire";
            }

            if (healthText != null)
            {
                healthText.text = exists
                    ? $"HP: {FormatHealth(target.Health.CurrentHealth)} / {FormatHealth(target.Health.MaxHealth)}"
                    : "HP: --";
            }

            if (repairDescriptionText != null)
            {
                repairDescriptionText.text = exists
                    ? $"Repair: {target.RepairWoodCost} Wood\n+{Mathf.RoundToInt(target.RepairHealthPercent * 100f)}% Max HP"
                    : "Campfire unavailable";
            }

            if (repairButton != null)
            {
                repairButton.interactable = exists && target.CanRepair(wallet);
            }
        }

        private void Awake()
        {
            repairButton?.onClick.AddListener(HandleRepairClicked);
            closeButton?.onClick.AddListener(Close);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            repairButton?.onClick.RemoveListener(HandleRepairClicked);
            closeButton?.onClick.RemoveListener(Close);
        }

        private void Subscribe()
        {
            if (target?.Health != null)
            {
                target.Health.HealthChanged += HandleHealthChanged;
                target.Health.Died += HandleDied;
            }

            if (wallet != null)
            {
                wallet.ResourceChanged += HandleWalletChanged;
            }
        }

        private void Unsubscribe()
        {
            if (target?.Health != null)
            {
                target.Health.HealthChanged -= HandleHealthChanged;
                target.Health.Died -= HandleDied;
            }

            if (wallet != null)
            {
                wallet.ResourceChanged -= HandleWalletChanged;
            }
        }

        private void HandleRepairClicked() => RepairCampfire();
        private void HandleHealthChanged(HealthChangedEvent _) => Refresh();
        private void HandleDied(DeathEvent _) => Refresh();
        private void HandleWalletChanged(ResourceWalletChangedEvent _) => Refresh();
        private static string FormatHealth(float value) => Mathf.CeilToInt(value).ToString();
    }
}
