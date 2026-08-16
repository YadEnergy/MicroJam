using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MicroJam.Game
{
    public sealed class NightLightingController : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField, Min(0.01f)] private float transitionDuration = 2f;

        [Header("Night Ambient")]
        [SerializeField] private Color dayAmbientColor = Color.white;
        [SerializeField] private Color nightAmbientColor = new(0.22f, 0.3f, 0.48f, 1f);
        [SerializeField, Range(0f, 1f)] private float nightAmbientIntensity = 0.28f;

        [Header("Player Glow")]
        [SerializeField] private Color playerLightColor = new(0.72f, 0.82f, 1f, 1f);
        [SerializeField, Min(0f)] private float playerLightIntensity = 0.45f;
        [SerializeField, Min(0f)] private float playerInnerRadius = 0.5f;
        [SerializeField, Min(0.01f)] private float playerOuterRadius = 3.5f;

        [Header("Campfire Glow")]
        [SerializeField] private Color campfireLightColor = new(1f, 0.48f, 0.12f, 1f);
        [SerializeField, Min(0f)] private float campfireLightIntensity = 1.15f;
        [SerializeField, Min(0f)] private float campfireInnerRadius = 1.4f;
        [SerializeField, Min(0.01f)] private float campfireOuterRadius = 6.5f;

        private DayNightCycle cycle;
        private CampfireInteraction campfire;
        private Light2D ambientLight;
        private Light2D playerLight;
        private Light2D campfireLight;
        private bool targetNight;

        private void Awake()
        {
            cycle = GetComponent<DayNightCycle>();
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            campfire = FindFirstObjectByType<CampfireInteraction>();

            ambientLight = CreateLight(transform, "Night Ambient Light", Light2D.LightType.Global);
            playerLight = CreateLight(player != null ? player.transform : transform, "Player Night Glow", Light2D.LightType.Point);
            campfireLight = CreateLight(campfire != null ? campfire.transform : transform, "Campfire Night Glow", Light2D.LightType.Point);

            ConfigurePointLight(playerLight, playerLightColor, playerInnerRadius, playerOuterRadius);
            ConfigurePointLight(campfireLight, campfireLightColor, campfireInnerRadius, campfireOuterRadius);
            targetNight = cycle != null && !cycle.IsDay;
            ApplyImmediately();
        }

        private void OnEnable()
        {
            if (cycle != null) cycle.DayStateChanged += HandleDayStateChanged;
        }

        private void OnDisable()
        {
            if (cycle != null) cycle.DayStateChanged -= HandleDayStateChanged;
        }

        private void Update()
        {
            float speed = Time.unscaledDeltaTime / transitionDuration;
            Color ambientTarget = targetNight ? nightAmbientColor : dayAmbientColor;
            float ambientIntensityTarget = targetNight ? nightAmbientIntensity : 1f;
            float playerTarget = targetNight ? playerLightIntensity : 0f;
            float campfireTarget = targetNight && campfire != null && campfire.Health != null && !campfire.Health.IsDead
                ? campfireLightIntensity
                : 0f;

            ambientLight.color = Color.Lerp(ambientLight.color, ambientTarget, speed);
            ambientLight.intensity = Mathf.MoveTowards(ambientLight.intensity, ambientIntensityTarget, speed);
            playerLight.intensity = Mathf.MoveTowards(playerLight.intensity, playerTarget, speed);
            campfireLight.intensity = Mathf.MoveTowards(campfireLight.intensity, campfireTarget, speed);
        }

        private void HandleDayStateChanged(bool isDay) => targetNight = !isDay;

        private void ApplyImmediately()
        {
            ambientLight.color = targetNight ? nightAmbientColor : dayAmbientColor;
            ambientLight.intensity = targetNight ? nightAmbientIntensity : 1f;
            playerLight.intensity = targetNight ? playerLightIntensity : 0f;
            campfireLight.intensity = targetNight && campfire != null && campfire.Health != null && !campfire.Health.IsDead
                ? campfireLightIntensity
                : 0f;
        }

        private static Light2D CreateLight(Transform parent, string objectName, Light2D.LightType type)
        {
            Transform existing = parent.Find(objectName);
            GameObject lightObject = existing != null ? existing.gameObject : new GameObject(objectName);
            lightObject.transform.SetParent(parent, false);
            Light2D light = lightObject.GetComponent<Light2D>();
            if (light == null) light = lightObject.AddComponent<Light2D>();
            light.lightType = type;
            light.blendStyleIndex = 0;
            light.shadowsEnabled = false;
            return light;
        }

        private static void ConfigurePointLight(Light2D light, Color color, float innerRadius, float outerRadius)
        {
            light.color = color;
            light.pointLightInnerRadius = Mathf.Min(innerRadius, outerRadius);
            light.pointLightOuterRadius = Mathf.Max(0.01f, outerRadius);
            light.falloffIntensity = 0.75f;
            light.intensity = 0f;
        }

        private void OnValidate()
        {
            transitionDuration = Mathf.Max(0.01f, transitionDuration);
            playerOuterRadius = Mathf.Max(0.01f, playerOuterRadius);
            campfireOuterRadius = Mathf.Max(0.01f, campfireOuterRadius);
            playerInnerRadius = Mathf.Clamp(playerInnerRadius, 0f, playerOuterRadius);
            campfireInnerRadius = Mathf.Clamp(campfireInnerRadius, 0f, campfireOuterRadius);
        }
    }
}
