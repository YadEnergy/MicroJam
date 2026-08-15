using System.Collections;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private DinosaurSpawner dinosaurSpawner;
        [SerializeField, Min(0.01f)] private float dayDuration = 60f;
        [SerializeField] private bool waitForTutorialBeforeFirstNight;

        private bool isDay;
        private bool isDayCountdownActive;
        private float dayEndsAt;
        private int currentDayNumber;
        private bool firstNightRequested;

        public bool IsDay => isDay;
        public bool IsDayCountdownActive => isDayCountdownActive;
        public float DaySecondsRemaining => isDayCountdownActive ? Mathf.Max(0f, dayEndsAt - Time.time) : 0f;
        public int CurrentDayNumber => currentDayNumber;

        /// <summary>
        /// Ends the held first day. Used by the tutorial immediately before it asks the player to fight a dinosaur.
        /// </summary>
        public bool StartFirstNightFromTutorial()
        {
            if (!waitForTutorialBeforeFirstNight || currentDayNumber != 1 || !isDay)
            {
                return false;
            }

            firstNightRequested = true;
            return true;
        }

        /// <summary>Disables the tutorial-only hold before the first night for returning players.</summary>
        public void SetFirstNightTutorialGate(bool enabled)
        {
            waitForTutorialBeforeFirstNight = enabled;
            if (!enabled)
            {
                firstNightRequested = true;
            }
        }

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
                if (currentDayNumber == 1 && waitForTutorialBeforeFirstNight)
                {
                    yield return new WaitUntil(() => firstNightRequested);
                }
                else
                {
                    isDayCountdownActive = true;
                    dayEndsAt = Time.time + dayDuration;
                    yield return new WaitForSeconds(dayDuration);
                }

                isDay = false;
                isDayCountdownActive = false;
                yield return dinosaurSpawner.RunNextWaveAndWaitUntilCleared();
            }
        }

        private void OnValidate() => dayDuration = Mathf.Max(0.01f, dayDuration);
    }
}
