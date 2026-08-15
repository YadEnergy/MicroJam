using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class WaveInfoText : MonoBehaviour
    {
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private DinosaurSpawner dinosaurSpawner;
        [SerializeField] private TMP_Text textLabel;

        private void Awake()
        {
            dayNightCycle ??= FindFirstObjectByType<DayNightCycle>();
            dinosaurSpawner ??= FindFirstObjectByType<DinosaurSpawner>();
            textLabel ??= GetComponent<TMP_Text>();
        }

        private void Update()
        {
            if (textLabel == null || dayNightCycle == null || dinosaurSpawner == null) return;
            textLabel.text = dayNightCycle.IsDay
                ? $"День {dayNightCycle.CurrentDayNumber}"
                : $"Волна {dinosaurSpawner.CurrentWaveNumber}";
        }

    }
}
