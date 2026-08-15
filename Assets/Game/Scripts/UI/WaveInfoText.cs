using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class WaveInfoText : MonoBehaviour
    {
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private TMP_Text textLabel;

        private void Awake()
        {
            dayNightCycle ??= FindFirstObjectByType<DayNightCycle>();
            textLabel ??= GetComponent<TMP_Text>();
        }

        private void Update()
        {
            if (textLabel == null || dayNightCycle == null) return;
            textLabel.text = dayNightCycle.IsDay
                ? $"Day {dayNightCycle.CurrentDayNumber}"
                : $"Wave {dayNightCycle.CurrentDayNumber}";
        }

    }
}
