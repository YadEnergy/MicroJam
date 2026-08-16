using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class DayNightUI : MonoBehaviour
    {
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private TMP_Text textLabel;
        [SerializeField] private RectTransform progressMarker;

        private void Awake()
        {
            textLabel ??= GetComponent<TMP_Text>();
            dayNightCycle ??= FindFirstObjectByType<DayNightCycle>();
            if (progressMarker == null)
            {
                Debug.LogError("DayNightUI requires a scene-authored progress marker.", this);
            }
        }

        private void Update()
        {
            if (dayNightCycle != null && progressMarker != null)
            {
                float progress = dayNightCycle.DayProgress01;
                Vector2 anchorMin = progressMarker.anchorMin;
                Vector2 anchorMax = progressMarker.anchorMax;
                anchorMin.x = progress;
                anchorMax.x = progress;
                progressMarker.anchorMin = anchorMin;
                progressMarker.anchorMax = anchorMax;
            }
        }
    }
}
