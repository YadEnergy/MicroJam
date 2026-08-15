using UnityEngine;

namespace MicroJam.Game
{
    public sealed class BuildPlacementPreview : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer previewRenderer;
        [SerializeField] private Color validColor = new(0.15f, 1f, 0.25f, 0.52f);
        [SerializeField] private Color invalidColor = new(1f, 0.12f, 0.12f, 0.52f);

        public SpriteRenderer PreviewRenderer => previewRenderer;
        public Color ValidColor => validColor;
        public Color InvalidColor => invalidColor;
        public bool IsVisible => previewRenderer != null && previewRenderer.enabled;
        public bool ShowsValidPlacement { get; private set; }
        public BuildingDefinition CurrentDefinition { get; private set; }

        public void Configure(SpriteRenderer renderer, Color configuredValidColor, Color configuredInvalidColor)
        {
            previewRenderer = renderer;
            validColor = configuredValidColor;
            invalidColor = configuredInvalidColor;
            Hide();
        }

        public void Show(BuildingDefinition definition, Vector2 worldCenter, Vector2 worldSize, bool isValid)
        {
            CurrentDefinition = definition;
            ShowsValidPlacement = isValid;
            transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
            transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);
            if (previewRenderer != null)
            {
                previewRenderer.color = isValid ? validColor : invalidColor;
                previewRenderer.enabled = true;
            }
        }

        public void Hide()
        {
            CurrentDefinition = null;
            ShowsValidPlacement = false;
            if (previewRenderer != null)
            {
                previewRenderer.enabled = false;
            }
        }

        private void Awake() => Hide();
    }
}
