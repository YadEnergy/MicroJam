using UnityEngine;
using UnityEngine.UI;

namespace MicroJam.Game
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private SceneTransitionController sceneTransition;
        [SerializeField] private string gameplaySceneName = "Game";

        public Button PlayButton => playButton;
        public Button ExitButton => exitButton;
        public string GameplaySceneName => gameplaySceneName;

        public void Configure(Button play, Button exit, SceneTransitionController transition, string configuredGameplayScene)
        {
            playButton = play;
            exitButton = exit;
            sceneTransition = transition;
            gameplaySceneName = configuredGameplayScene;
        }

        public void Play()
        {
            if (sceneTransition == null || sceneTransition.IsTransitioning) return;
            Time.timeScale = 1f;
            sceneTransition.LoadScene(gameplaySceneName);
        }

        public void Exit()
        {
#if UNITY_EDITOR
            Debug.Log("Quit requested");
#else
            Application.Quit();
#endif
        }

        private void Awake() => Time.timeScale = 1f;

        private void OnEnable()
        {
            playButton?.onClick.AddListener(Play);
            exitButton?.onClick.AddListener(Exit);
        }

        private void OnDisable()
        {
            playButton?.onClick.RemoveListener(Play);
            exitButton?.onClick.RemoveListener(Exit);
        }
    }
}
