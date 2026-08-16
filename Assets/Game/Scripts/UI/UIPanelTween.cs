using System;
using DG.Tweening;
using UnityEngine;

namespace MicroJam.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIPanelTween : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform animatedRoot;
        [SerializeField] private bool startHidden = true;
        [SerializeField, Min(0f)] private float openDuration = 0.25f;
        [SerializeField, Min(0f)] private float closeDuration = 0.17f;
        [SerializeField, Range(0.1f, 1f)] private float openStartScale = 0.85f;
        [SerializeField, Range(0.1f, 1f)] private float closeEndScale = 0.9f;
        [SerializeField] private Ease openEase = Ease.OutBack;
        [SerializeField] private Ease closeEase = Ease.OutCubic;
        [SerializeField] private bool disableAfterClose = true;

        private Tween activeTween;
        private Vector3 baseScale = Vector3.one;
        private bool initialized;

        public CanvasGroup CanvasGroup => canvasGroup;
        public RectTransform AnimatedRoot => animatedRoot;
        public float OpenDuration => openDuration;
        public float CloseDuration => closeDuration;
        public bool IsAnimating => activeTween != null && activeTween.IsActive() && activeTween.IsPlaying();
        public bool IsVisible => gameObject.activeInHierarchy && canvasGroup != null && canvasGroup.alpha > 0.001f;

        public void Configure(CanvasGroup group, RectTransform root, bool hiddenInitially = true,
            float configuredOpenDuration = 0.25f, float configuredCloseDuration = 0.17f,
            float configuredOpenStartScale = 0.85f, float configuredCloseEndScale = 0.9f,
            bool configuredDisableAfterClose = true)
        {
            canvasGroup = group;
            animatedRoot = root;
            startHidden = hiddenInitially;
            openDuration = Mathf.Max(0f, configuredOpenDuration);
            closeDuration = Mathf.Max(0f, configuredCloseDuration);
            openStartScale = Mathf.Clamp(configuredOpenStartScale, 0.1f, 1f);
            closeEndScale = Mathf.Clamp(configuredCloseEndScale, 0.1f, 1f);
            disableAfterClose = configuredDisableAfterClose;
            InitializeIfNeeded();
        }

        public void Show(bool immediate = false)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            InitializeIfNeeded();
            KillActiveTween();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            if (immediate || openDuration <= 0f)
            {
                canvasGroup.alpha = 1f;
                animatedRoot.localScale = baseScale;
                return;
            }

            canvasGroup.alpha = 0f;
            animatedRoot.localScale = baseScale * openStartScale;
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            sequence.Join(DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 1f, openDuration)
                .SetEase(Ease.OutCubic));
            sequence.Join(animatedRoot.DOScale(baseScale, openDuration).SetEase(openEase));
            activeTween = sequence.OnComplete(() => activeTween = null);
        }

        public void Hide(Action onComplete = null, bool immediate = false)
        {
            InitializeIfNeeded();
            KillActiveTween();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            void Complete()
            {
                activeTween = null;
                canvasGroup.alpha = 0f;
                animatedRoot.localScale = baseScale * closeEndScale;
                onComplete?.Invoke();
                if (disableAfterClose) gameObject.SetActive(false);
            }

            if (!gameObject.activeSelf || immediate || closeDuration <= 0f)
            {
                Complete();
                return;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            sequence.Join(DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 0f, closeDuration)
                .SetEase(closeEase));
            sequence.Join(animatedRoot.DOScale(baseScale * closeEndScale, closeDuration).SetEase(closeEase));
            activeTween = sequence.OnComplete(Complete);
        }

        public void SetHiddenImmediate()
        {
            InitializeIfNeeded();
            KillActiveTween();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            animatedRoot.localScale = baseScale * closeEndScale;
            if (disableAfterClose && gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Awake()
        {
            InitializeIfNeeded();
            if (startHidden)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                animatedRoot.localScale = baseScale * closeEndScale;
            }
        }

        private void OnDisable() => KillActiveTween();
        private void OnDestroy() => KillActiveTween();

        private void InitializeIfNeeded()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            animatedRoot ??= transform as RectTransform;
            if (initialized || animatedRoot == null) return;
            baseScale = animatedRoot.localScale;
            initialized = true;
        }

        private void KillActiveTween()
        {
            if (activeTween != null && activeTween.IsActive()) activeTween.Kill(false);
            activeTween = null;
            animatedRoot?.DOKill(false);
            canvasGroup?.DOKill(false);
        }

        private void OnValidate()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            animatedRoot ??= transform as RectTransform;
            openDuration = Mathf.Max(0f, openDuration);
            closeDuration = Mathf.Max(0f, closeDuration);
            openStartScale = Mathf.Clamp(openStartScale, 0.1f, 1f);
            closeEndScale = Mathf.Clamp(closeEndScale, 0.1f, 1f);
        }
    }
}
