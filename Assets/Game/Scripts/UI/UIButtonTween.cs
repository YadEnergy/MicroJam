using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MicroJam.Game
{
    [DisallowMultipleComponent]
    public sealed class UIButtonTween : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform animatedRoot;
        [SerializeField, Min(1f)] private float hoverScale = 1.05f;
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.95f;
        [SerializeField, Min(1f)] private float selectedScale = 1.08f;
        [SerializeField, Min(0f)] private float hoverDuration = 0.1f;
        [SerializeField, Min(0f)] private float pressDuration = 0.07f;
        [SerializeField] private Ease ease = Ease.OutCubic;

        private Vector3 baseScale = Vector3.one;
        private bool pointerInside;
        private bool selected;
        private bool initialized;

        public bool IsSelected => selected;
        public bool IsPointerInside => pointerInside;
        public float HoverScale => hoverScale;
        public float PressedScale => pressedScale;

        public void Configure(RectTransform root, float configuredHoverScale = 1.05f,
            float configuredPressedScale = 0.95f, float configuredSelectedScale = 1.08f,
            float configuredHoverDuration = 0.1f, float configuredPressDuration = 0.07f)
        {
            animatedRoot = root;
            hoverScale = Mathf.Max(1f, configuredHoverScale);
            pressedScale = Mathf.Clamp(configuredPressedScale, 0.5f, 1f);
            selectedScale = Mathf.Max(1f, configuredSelectedScale);
            hoverDuration = Mathf.Max(0f, configuredHoverDuration);
            pressDuration = Mathf.Max(0f, configuredPressDuration);
            InitializeIfNeeded();
        }

        public void SetSelected(bool value, bool immediate = false)
        {
            selected = value;
            AnimateToRest(immediate ? 0f : hoverDuration);
        }

        public void OnPointerEnter(PointerEventData _) { pointerInside = true; AnimateToRest(hoverDuration); }
        public void OnPointerExit(PointerEventData _) { pointerInside = false; AnimateToRest(hoverDuration); }

        public void OnPointerDown(PointerEventData _)
        {
            InitializeIfNeeded();
            AnimateScale(RestMultiplier * pressedScale, pressDuration);
        }

        public void OnPointerUp(PointerEventData _) => AnimateToRest(pressDuration);

        private float RestMultiplier => (selected ? selectedScale : 1f) * (pointerInside ? hoverScale : 1f);

        private void Awake() => InitializeIfNeeded();

        private void OnDisable()
        {
            pointerInside = false;
            if (animatedRoot != null)
            {
                animatedRoot.DOKill(false);
                animatedRoot.localScale = baseScale * (selected ? selectedScale : 1f);
            }
        }

        private void OnDestroy() => animatedRoot?.DOKill(false);

        private void AnimateToRest(float duration) => AnimateScale(RestMultiplier, duration);

        private void AnimateScale(float multiplier, float duration)
        {
            InitializeIfNeeded();
            if (animatedRoot == null) return;
            animatedRoot.DOKill(false);
            if (duration <= 0f)
            {
                animatedRoot.localScale = baseScale * multiplier;
                return;
            }

            animatedRoot.DOScale(baseScale * multiplier, duration).SetEase(ease).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void InitializeIfNeeded()
        {
            animatedRoot ??= transform as RectTransform;
            if (initialized || animatedRoot == null) return;
            baseScale = animatedRoot.localScale;
            initialized = true;
        }

        private void OnValidate()
        {
            animatedRoot ??= transform as RectTransform;
            hoverScale = Mathf.Max(1f, hoverScale);
            pressedScale = Mathf.Clamp(pressedScale, 0.5f, 1f);
            selectedScale = Mathf.Max(1f, selectedScale);
            hoverDuration = Mathf.Max(0f, hoverDuration);
            pressDuration = Mathf.Max(0f, pressDuration);
        }
    }
}
