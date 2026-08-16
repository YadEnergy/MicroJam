using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class DayNightUI : MonoBehaviour
    {
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private TMP_Text textLabel;
        [SerializeField] private RectTransform progressMarker;

        private const float MarkerTravel = 168f;

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
                Vector2 position = progressMarker.anchoredPosition;
                position.x = Mathf.Lerp(-MarkerTravel, MarkerTravel, dayNightCycle.DayProgress01);
                progressMarker.anchoredPosition = position;
            }
        }
    }
}
