using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroJam.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SceneTransitionController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup overlay;
        [SerializeField, Min(0f)] private float fadeDuration = 0.4f;
        [SerializeField] private bool fadeFromBlackOnStart = true;

        private Tween activeTween;

        public CanvasGroup Overlay => overlay;
        public float FadeDuration => fadeDuration;
        public bool IsTransitioning { get; private set; }

        public void Configure(CanvasGroup configuredOverlay, float configuredFadeDuration = 0.4f, bool configuredFadeOnStart = true)
        {
            overlay = configuredOverlay;
            fadeDuration = Mathf.Max(0f, configuredFadeDuration);
            fadeFromBlackOnStart = configuredFadeOnStart;
        }

        public bool LoadScene(string sceneName)
        {
            if (IsTransitioning || string.IsNullOrWhiteSpace(sceneName)) return false;
            return FadeToBlack(() =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(sceneName);
            });
        }

        public bool ReloadCurrentScene() => LoadScene(SceneManager.GetActiveScene().name);

        public bool FadeToBlack(Action onComplete = null)
        {
            if (IsTransitioning || overlay == null) return false;
            KillTween();
            IsTransitioning = true;
            overlay.gameObject.SetActive(true);
            overlay.blocksRaycasts = true;
            overlay.interactable = true;
            activeTween = DOTween.To(() => overlay.alpha, value => overlay.alpha = value, 1f, fadeDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    activeTween = null;
                    onComplete?.Invoke();
                });
            return true;
        }

        public void FadeFromBlack(bool immediate = false)
        {
            if (overlay == null) return;
            KillTween();
            IsTransitioning = true;
            overlay.gameObject.SetActive(true);
            overlay.alpha = 1f;
            overlay.blocksRaycasts = true;
            overlay.interactable = true;

            void Complete()
            {
                activeTween = null;
                IsTransitioning = false;
                overlay.alpha = 0f;
                overlay.blocksRaycasts = false;
                overlay.interactable = false;
            }

            if (immediate || fadeDuration <= 0f)
            {
                Complete();
                return;
            }

            activeTween = DOTween.To(() => overlay.alpha, value => overlay.alpha = value, 0f, fadeDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy).OnComplete(Complete);
        }

        private void Awake()
        {
            overlay ??= GetComponent<CanvasGroup>();
            if (fadeFromBlackOnStart)
            {
                overlay.alpha = 1f;
                overlay.blocksRaycasts = true;
                overlay.interactable = true;
            }
        }

        private void Start()
        {
            if (fadeFromBlackOnStart) FadeFromBlack();
            else
            {
                overlay.alpha = 0f;
                overlay.blocksRaycasts = false;
                overlay.interactable = false;
            }
        }

        private void OnDestroy() => KillTween();

        private void KillTween()
        {
            if (activeTween != null && activeTween.IsActive()) activeTween.Kill(false);
            activeTween = null;
            overlay?.DOKill(false);
        }

        private void OnValidate()
        {
            overlay ??= GetComponent<CanvasGroup>();
            fadeDuration = Mathf.Max(0f, fadeDuration);
            if (!Application.isPlaying && overlay != null)
            {
                overlay.alpha = 0f;
                overlay.interactable = false;
                overlay.blocksRaycasts = false;
            }
        }
    }
}
