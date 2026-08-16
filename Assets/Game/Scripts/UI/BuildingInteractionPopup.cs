using UnityEngine;
using UnityEngine.UI;

namespace MicroJam.Game
{
    public sealed class BuildingInteractionPopup : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text removalPromptText;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button closeButton;

        private BuildingInstance target;
        private PlayerResourceWallet wallet;

        public BuildingInstance Target => target;
        public Text TitleText => titleText;
        public Text HealthText => healthText;
        public Text RemovalPromptText => removalPromptText;
        public Button RemoveButton => removeButton;
        public Button CloseButton => closeButton;
        public bool IsOpen => gameObject.activeSelf;

        public void Configure(Text configuredTitle, Text configuredHealth, Text configuredPrompt, Button configuredRemove, Button configuredClose)
        {
            titleText = configuredTitle;
            healthText = configuredHealth;
            removalPromptText = configuredPrompt;
            removeButton = configuredRemove;
            closeButton = configuredClose;
        }

        public void Open(BuildingInstance building, PlayerResourceWallet playerWallet)
        {
            UnsubscribeTarget();
            target = building;
            wallet = playerWallet;
            SubscribeTarget();
            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            UnsubscribeTarget();
            target = null;
            wallet = null;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public bool RemoveSelectedBuilding()
        {
            if (target == null || wallet == null || !target.TryRemoveByPlayer(wallet))
            {
                Refresh();
                return false;
            }

            Close();
            return true;
        }

        public void Refresh()
        {
            bool valid = target != null && target.Health != null && !target.RemovalStarted;
            if (titleText != null)
            {
                titleText.text = valid ? target.Definition.DisplayName : "Building unavailable";
            }

            if (healthText != null)
            {
                healthText.text = valid ? $"HP: {FormatHealth(target.Health.CurrentHealth)} / {FormatHealth(target.Health.MaxHealth)}" : "HP: --";
            }

            if (removalPromptText != null)
            {
                removalPromptText.text = valid ? BuildRefundText(target) : "This building no longer exists.";
            }

            if (removeButton != null)
            {
                removeButton.interactable = valid && target.IsRegistered && wallet != null;
            }
        }

        private void Awake()
        {
            removeButton?.onClick.AddListener(HandleRemoveClicked);
            closeButton?.onClick.AddListener(Close);
        }

        private void OnDisable() => UnsubscribeTarget();

        private void OnDestroy()
        {
            UnsubscribeTarget();
            removeButton?.onClick.RemoveListener(HandleRemoveClicked);
            closeButton?.onClick.RemoveListener(Close);
        }

        private void SubscribeTarget()
        {
            if (target == null)
            {
                return;
            }

            target.Health.HealthChanged += HandleHealthChanged;
            target.Removing += HandleBuildingRemoving;
        }

        private void UnsubscribeTarget()
        {
            if (target == null)
            {
                return;
            }

            if (target.Health != null)
            {
                target.Health.HealthChanged -= HandleHealthChanged;
            }

            target.Removing -= HandleBuildingRemoving;
        }

        private void HandleRemoveClicked() => RemoveSelectedBuilding();
        private void HandleHealthChanged(HealthChangedEvent _) => Refresh();
        private void HandleBuildingRemoving(BuildingRemovalEvent _) => Close();
        private static string FormatHealth(float value) => Mathf.CeilToInt(value).ToString();

        private static string BuildRefundText(BuildingInstance building)
        {
            int wood = building.RemovalRefundWood;
            int stone = building.RemovalRefundStone;
            if (stone <= 0) return $"Remove this building?\nRefund: {wood} Wood";
            if (wood <= 0) return $"Remove this building?\nRefund: {stone} Stone";
            return $"Remove this building?\nRefund: {wood} Wood + {stone} Stone";
        }
    }
}
