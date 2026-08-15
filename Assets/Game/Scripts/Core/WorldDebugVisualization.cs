using UnityEngine;

namespace MicroJam.Game
{
    public sealed class WorldDebugVisualization : MonoBehaviour
    {
        [SerializeField] private WorldGridConfig worldConfig;
        [SerializeField] private bool showVisualization = true;
        [SerializeField] private Color playableBoundaryColor = new(0.1f, 0.8f, 1f, 1f);
        [SerializeField] private Color buildZoneColor = new(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color protectedAreaColor = new(1f, 0.2f, 0.15f, 1f);

        public bool ShowVisualization
        {
            get => showVisualization;
            set => showVisualization = value;
        }

        public void Configure(WorldGridConfig config, bool visible)
        {
            worldConfig = config;
            showVisualization = visible;
        }

        private void OnDrawGizmos()
        {
            if (!showVisualization || worldConfig == null)
            {
                return;
            }

            DrawBounds(worldConfig.PlayableWorldBounds, playableBoundaryColor);
            DrawBounds(worldConfig.BuildZoneWorldBounds, buildZoneColor);
            DrawBounds(worldConfig.ProtectedCampfireWorldBounds, protectedAreaColor);
        }

        private static void DrawBounds(Bounds bounds, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, bounds.size.y, 0f));
        }
    }
}
