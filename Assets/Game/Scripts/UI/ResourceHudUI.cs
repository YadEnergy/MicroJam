using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class ResourceHudUI : MonoBehaviour
    {
        [SerializeField] private PlayerResourceWallet wallet;
        [SerializeField] private TMP_Text woodAmountText;
        [SerializeField] private TMP_Text stoneAmountText;

        private void Awake()
        {
            wallet ??= FindFirstObjectByType<PlayerResourceWallet>();
        }

        private void OnEnable()
        {
            if (wallet != null)
            {
                wallet.ResourceChanged += OnResourceChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (wallet != null)
            {
                wallet.ResourceChanged -= OnResourceChanged;
            }
        }

        private void OnResourceChanged(ResourceWalletChangedEvent _) => Refresh();

        private void Refresh()
        {
            if (wallet == null) return;

            if (woodAmountText != null) woodAmountText.text = wallet.Wood.ToString();
            if (stoneAmountText != null) stoneAmountText.text = wallet.Stone.ToString();
        }
    }
}
