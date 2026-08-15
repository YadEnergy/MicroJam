using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class PointsInfoUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text textLabel;

        private void Awake()
        {
            textLabel ??= GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            PlayerPoints.Changed += UpdateText;
            UpdateText(PlayerPoints.Current);
        }

        private void OnDisable()
        {
            PlayerPoints.Changed -= UpdateText;
        }

        private void UpdateText(int points)
        {
            if (textLabel != null)
            {
                textLabel.text = $"{points}";
            }
        }
    }
}
