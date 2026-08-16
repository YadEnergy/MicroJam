using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MicroJam.Game
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private UIPanelTween pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private SceneTransitionController sceneTransition;
        [SerializeField] private WorldInteractionController worldInteraction;
        [SerializeField] private string mainMenuSceneName = "SampleScene";

        private bool closing;

        public UIPanelTween PausePanel => pausePanel;
        public bool IsPaused { get; private set; }
        public string MainMenuSceneName => mainMenuSceneName;

        public void Configure(UIPanelTween panel, Button resume, Button restart, Button mainMenu,
            SceneTransitionController transition, WorldInteractionController interactions, string configuredMainMenuScene)
        {
            pausePanel = panel;
            resumeButton = resume;
            restartButton = restart;
            mainMenuButton = mainMenu;
            sceneTransition = transition;
            worldInteraction = interactions;
            mainMenuSceneName = configuredMainMenuScene;
        }

        public void TogglePause()
        {
            if (closing || (sceneTransition != null && sceneTransition.IsTransitioning)) return;
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused || closing) return;
            IsPaused = true;
            GameplayInputGate.SetBlocked(this, true);
            worldInteraction?.CloseAll();
            Time.timeScale = 0f;
            pausePanel?.Show();
        }

        public void Resume()
        {
            if (!IsPaused || closing) return;
            closing = true;
            void Complete()
            {
                closing = false;
                IsPaused = false;
                Time.timeScale = 1f;
                GameplayInputGate.SetBlocked(this, false);
            }

            if (pausePanel != null) pausePanel.Hide(Complete);
            else Complete();
        }

        public void Restart()
        {
            if (!IsPaused || sceneTransition == null || sceneTransition.IsTransitioning) return;
            sceneTransition.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            if (!IsPaused || sceneTransition == null || sceneTransition.IsTransitioning) return;
            sceneTransition.LoadScene(mainMenuSceneName);
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            pausePanel?.SetHiddenImmediate();
        }

        private void OnEnable()
        {
            resumeButton?.onClick.AddListener(Resume);
            restartButton?.onClick.AddListener(Restart);
            mainMenuButton?.onClick.AddListener(ReturnToMainMenu);
        }

        private void OnDisable()
        {
            resumeButton?.onClick.RemoveListener(Resume);
            restartButton?.onClick.RemoveListener(Restart);
            mainMenuButton?.onClick.RemoveListener(ReturnToMainMenu);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TogglePause();
        }

        private void OnDestroy()
        {
            GameplayInputGate.SetBlocked(this, false);
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }
    }
}
