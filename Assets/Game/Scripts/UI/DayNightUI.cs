using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class DayNightUI : MonoBehaviour
    {
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private TMP_Text textLabel;

        private void Awake()
        {
            textLabel ??= GetComponent<TMP_Text>();
            dayNightCycle ??= FindFirstObjectByType<DayNightCycle>();
        }

        private void Update()
        {
            if (textLabel == null || dayNightCycle == null)
            {
                return;
            }

            textLabel.text = !dayNightCycle.IsDay
                ? "Night"
                : dayNightCycle.IsDayCountdownActive
                    ? $"Day: {Mathf.CeilToInt(dayNightCycle.DaySecondsRemaining)} sec"
                    : "Day 1";
        }
    }
}
