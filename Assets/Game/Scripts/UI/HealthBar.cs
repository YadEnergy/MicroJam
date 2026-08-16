using UnityEngine;

namespace MicroJam.Game
{
    public enum HealthBarVisibilityMode
    {
        AlwaysVisible,
        ShowAfterDamage
    }

    public enum HealthBarColorRole
    {
        Friendly,
        Enemy
    }

    public sealed class HealthBar : MonoBehaviour
    {
        [Header("Owner")]
        [SerializeField] private Health observedHealth;
        [SerializeField] private HealthBarSettings settings;

        [Header("Behavior")]
        [SerializeField] private HealthBarVisibilityMode visibilityMode;
        [SerializeField] private HealthBarColorRole colorRole;

        [Header("Serialized Visuals")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private Transform fillTransform;
        [SerializeField] private Vector2 barSize = new(1f, 0.12f);

        [Header("Custom Visual")]
        [Tooltip("Keeps the colors and transforms authored in the prefab. Only the fill width is changed at runtime.")]
        [SerializeField] private bool preserveAuthoredVisuals;
        [SerializeField] private Vector3 authoredFullFillScale = new(1f, 0.12f, 1f);
        [SerializeField] private Vector3 authoredFullFillPosition = new(0f, 0f, -0.01f);

        private float visibleUntilTime;
        private bool subscribed;

        public Health ObservedHealth => observedHealth;
        public HealthBarSettings Settings => settings;
        public HealthBarVisibilityMode VisibilityMode => visibilityMode;
        public HealthBarColorRole ColorRole => colorRole;
        public Vector2 BarSize => barSize;
        public bool IsVisible => fillRenderer != null && fillRenderer.enabled;
        public float VisibleUntilTime => visibleUntilTime;
        public float DamageVisibleDuration => settings != null ? settings.DamagedVisibleDuration : 3f;
        public Color FillColor => fillRenderer != null ? fillRenderer.color : Color.clear;

        public void Configure(
            Health health,
            HealthBarSettings sharedSettings,
            HealthBarVisibilityMode mode,
            HealthBarColorRole role,
            SpriteRenderer background,
            SpriteRenderer fill,
            Vector2 size)
        {
            Unsubscribe();
            observedHealth = health;
            settings = sharedSettings;
            visibilityMode = mode;
            colorRole = role;
            backgroundRenderer = background;
            fillRenderer = fill;
            fillTransform = fill != null ? fill.transform : null;
            barSize = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
            ApplyVisualState();
            SetVisible(visibilityMode == HealthBarVisibilityMode.AlwaysVisible);
            if (Application.isPlaying && isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void ResetForSpawn()
        {
            visibleUntilTime = 0f;
            ApplyVisualState();
            SetVisible(visibilityMode == HealthBarVisibilityMode.AlwaysVisible);
        }

        private void Awake()
        {
            ApplyVisualState();
            SetVisible(visibilityMode == HealthBarVisibilityMode.AlwaysVisible);
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyVisualState();
            SetVisible(visibilityMode == HealthBarVisibilityMode.AlwaysVisible);
        }

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (visibilityMode == HealthBarVisibilityMode.ShowAfterDamage && IsVisible && Time.time >= visibleUntilTime)
            {
                SetVisible(false);
            }
        }

        private void Subscribe()
        {
            if (subscribed || observedHealth == null)
            {
                return;
            }

            observedHealth.HealthChanged += HandleHealthChanged;
            observedHealth.DamageReceived += HandleDamageReceived;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || observedHealth == null)
            {
                return;
            }

            observedHealth.HealthChanged -= HandleHealthChanged;
            observedHealth.DamageReceived -= HandleDamageReceived;
            subscribed = false;
        }

        private void HandleHealthChanged(HealthChangedEvent change) => UpdateFill(change.CurrentHealth, change.MaxHealth);

        private void HandleDamageReceived(DamageReceivedEvent damage)
        {
            if (visibilityMode != HealthBarVisibilityMode.ShowAfterDamage)
            {
                return;
            }

            visibleUntilTime = Time.time + DamageVisibleDuration;
            SetVisible(true);
        }

        private void ApplyVisualState()
        {
            if (!preserveAuthoredVisuals && backgroundRenderer != null)
            {
                backgroundRenderer.color = settings != null ? settings.BackgroundColor : new Color(0.06f, 0.07f, 0.08f, 0.9f);
                backgroundRenderer.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
            }

            if (!preserveAuthoredVisuals && fillRenderer != null)
            {
                fillRenderer.color = colorRole == HealthBarColorRole.Enemy
                    ? settings != null ? settings.EnemyColor : Color.red
                    : settings != null ? settings.FriendlyColor : Color.green;
            }

            if (observedHealth != null)
            {
                UpdateFill(observedHealth.CurrentHealth, observedHealth.MaxHealth);
            }
            else
            {
                UpdateFill(1f, 1f);
            }
        }

        private void UpdateFill(float currentHealth, float maxHealth)
        {
            if (fillTransform == null)
            {
                return;
            }

            float ratio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
            Vector3 fullScale = preserveAuthoredVisuals
                ? authoredFullFillScale
                : new Vector3(barSize.x, barSize.y, 1f);
            Vector3 fullPosition = preserveAuthoredVisuals
                ? authoredFullFillPosition
                : new Vector3(0f, 0f, -0.01f);

            fillTransform.localScale = new Vector3(fullScale.x * ratio, fullScale.y, fullScale.z);
            fillTransform.localPosition = new Vector3(
                fullPosition.x - fullScale.x * (1f - ratio) * 0.5f,
                fullPosition.y,
                fullPosition.z);
        }

        private void SetVisible(bool visible)
        {
            if (backgroundRenderer != null)
            {
                backgroundRenderer.enabled = visible;
            }

            if (fillRenderer != null)
            {
                fillRenderer.enabled = visible;
            }
        }

        private void OnValidate()
        {
            barSize = new Vector2(Mathf.Max(0.01f, barSize.x), Mathf.Max(0.01f, barSize.y));
        }
    }
}
