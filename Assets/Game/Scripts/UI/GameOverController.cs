using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MicroJam.Game
{
    public sealed class GameOverController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DayNightCycle dayNightCycle;
        [SerializeField] private GameObject endedInfoPanel;
        [SerializeField] private TMP_Text endedInfoText;
        [SerializeField] private Button restartButton;
        [SerializeField] private UIPanelTween endedPanelTween;
        [SerializeField] private SceneTransitionController sceneTransition;

        private bool gameEnded;

        private void Awake()
        {
            Time.timeScale = 1f;
            PlayerPoints.Reset();
            dayNightCycle ??= FindFirstObjectByType<DayNightCycle>();

            if (endedPanelTween != null) endedPanelTween.SetHiddenImmediate();
            else if (endedInfoPanel != null) endedInfoPanel.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.CampfireDestroyed += OnCampfireDestroyed;

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(RestartGame);
            }
        }

        private void OnDisable()
        {
            GameEvents.CampfireDestroyed -= OnCampfireDestroyed;

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(RestartGame);
            }
        }

        private void OnCampfireDestroyed()
        {
            if (gameEnded) return;

            gameEnded = true;
            int daysSurvived = dayNightCycle != null ? dayNightCycle.CurrentDayNumber : 0;

            if (endedInfoText != null)
            {
                endedInfoText.text = $"Days survived: {daysSurvived} days\nPoints: {PlayerPoints.Current}";
            }

            if (endedPanelTween != null) endedPanelTween.Show();
            else if (endedInfoPanel != null) endedInfoPanel.SetActive(true);

            Time.timeScale = 0f;
        }

        public void RestartGame()
        {
            if (sceneTransition != null)
            {
                sceneTransition.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ConfigureTween(UIPanelTween panelTween, SceneTransitionController transition)
        {
            endedPanelTween = panelTween;
            sceneTransition = transition;
        }
    }
}
