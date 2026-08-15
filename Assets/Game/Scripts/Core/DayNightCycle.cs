using System.Collections;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private DinosaurSpawner dinosaurSpawner;
        [SerializeField, Min(0.01f)] private float dayDuration = 60f;

        private bool isDay;
        private float dayEndsAt;
        private int currentDayNumber;

        public bool IsDay => isDay;
        public float DaySecondsRemaining => isDay ? Mathf.Max(0f, dayEndsAt - Time.time) : 0f;
        public int CurrentDayNumber => currentDayNumber;

        private void Awake()
        {
            dinosaurSpawner ??= FindFirstObjectByType<DinosaurSpawner>();
        }

        private void Start()
        {
            if (dinosaurSpawner == null || !dinosaurSpawner.HasConfiguredWaves)
            {
                Debug.LogError("DayNightCycle needs a DinosaurSpawner with at least one wave.", this);
                enabled = false;
                return;
            }

            StartCoroutine(Cycle());
        }

        private IEnumerator Cycle()
        {
            while (enabled)
            {
                currentDayNumber++;
                isDay = true;
                dayEndsAt = Time.time + dayDuration;
                yield return new WaitForSeconds(dayDuration);

                isDay = false;
                yield return dinosaurSpawner.RunNextWaveAndWaitUntilCleared();
            }
        }

        private void OnValidate() => dayDuration = Mathf.Max(0.01f, dayDuration);
    }
}
