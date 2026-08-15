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

        public bool IsDay => isDay;
        public float DaySecondsRemaining => isDay ? Mathf.Max(0f, dayEndsAt - Time.time) : 0f;

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
                isDay = true;
                dayEndsAt = Time.time + dayDuration;
                Debug.Log($"Day started. Night begins in {dayDuration:0.#} seconds.", this);
                yield return new WaitForSeconds(dayDuration);

                isDay = false;
                Debug.Log("Night started. The next dinosaur wave is spawning.", this);
                yield return dinosaurSpawner.RunNextWaveAndWaitUntilCleared();
                Debug.Log("Night ended. All dinosaurs were defeated.", this);
            }
        }

        private void OnValidate() => dayDuration = Mathf.Max(0.01f, dayDuration);
    }
}
