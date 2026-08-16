using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Camera))]
    public sealed class SquareGameplayViewport : MonoBehaviour
    {
        [SerializeField] private WorldGridConfig worldConfig;
        [SerializeField] private Color barColor = Color.black;

        private Camera gameplayCamera;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;

        public Rect NormalizedGameplayViewport => gameplayCamera != null ? gameplayCamera.rect : default;
        public Rect PixelGameplayViewport => gameplayCamera != null ? gameplayCamera.pixelRect : default;

        public static Rect CalculateSquareViewport(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            if (screenWidth > screenHeight)
            {
                float width = (float)screenHeight / screenWidth;
                return new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }

            float height = (float)screenWidth / screenHeight;
            return new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }

        public void Configure(WorldGridConfig config, Color bars)
        {
            worldConfig = config;
            barColor = bars;
            ApplyViewport(true);
        }

        public bool TryScreenToWorld(Vector2 screenPosition, out Vector2 worldPosition)
        {
            EnsureCamera();
            if (gameplayCamera == null || !gameplayCamera.pixelRect.Contains(screenPosition))
            {
                worldPosition = default;
                return false;
            }

            Vector3 converted = gameplayCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -gameplayCamera.transform.position.z));
            worldPosition = converted;
            return true;
        }

        private void Awake() => ApplyViewport(true);
        private void OnEnable() => ApplyViewport(true);
        private void LateUpdate() => ApplyViewport(false);

        private void ApplyViewport(bool force)
        {
            EnsureCamera();
            if (gameplayCamera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            if (!force && lastScreenWidth == Screen.width && lastScreenHeight == Screen.height)
            {
                return;
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            gameplayCamera.rect = CalculateSquareViewport(Screen.width, Screen.height);

            if (worldConfig != null)
            {
                gameplayCamera.orthographic = true;
                gameplayCamera.orthographicSize = worldConfig.WorldSize.y * 0.5f;
                Vector3 position = gameplayCamera.transform.position;
                gameplayCamera.transform.position = new Vector3(0f, 0f, position.z);
            }
        }

        private void OnGUI()
        {
            EnsureCamera();
            if (gameplayCamera == null)
            {
                return;
            }

            Rect rect = gameplayCamera.rect;
            Color previousColor = GUI.color;
            GUI.color = barColor;

            if (rect.xMin > 0f)
            {
                float sideWidth = Screen.width * rect.xMin;
                GUI.DrawTexture(new Rect(0f, 0f, sideWidth, Screen.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width - sideWidth, 0f, sideWidth, Screen.height), Texture2D.whiteTexture);
            }
            else if (rect.yMin > 0f)
            {
                float barHeight = Screen.height * rect.yMin;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, barHeight), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, Screen.height - barHeight, Screen.width, barHeight), Texture2D.whiteTexture);
            }

            GUI.color = previousColor;
        }

        private void EnsureCamera()
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = GetComponent<Camera>();
            }
        }

        private void OnValidate()
        {
            EnsureCamera();
            if (!Application.isPlaying && gameplayCamera != null)
            {
                gameplayCamera.rect = new Rect(0f, 0f, 1f, 1f);
            }
        }
    }
}
