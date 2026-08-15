using UnityEngine;

namespace MicroJam.Game
{
    public enum SpawnSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    public sealed class SpawnPerimeterProvider : MonoBehaviour
    {
        [SerializeField] private WorldGridConfig worldConfig;

        public void Configure(WorldGridConfig config) => worldConfig = config;

        public SpawnSide GetRandomSide() => (SpawnSide)Random.Range(0, 4);

        public Vector2 GetRandomPosition(SpawnSide side)
        {
            return GetPosition(side, Random.value);
        }

        public Vector2 GetPosition(SpawnSide side, float normalizedPosition)
        {
            if (worldConfig == null)
            {
                Debug.LogError("SpawnPerimeterProvider requires a WorldGridConfig asset.", this);
                return default;
            }

            Bounds bounds = worldConfig.PlayableWorldBounds;
            float t = Mathf.Clamp01(normalizedPosition);
            float x = Mathf.Lerp(bounds.min.x + worldConfig.TileSize * 0.5f, bounds.max.x - worldConfig.TileSize * 0.5f, t);
            float y = Mathf.Lerp(bounds.min.y + worldConfig.TileSize * 0.5f, bounds.max.y - worldConfig.TileSize * 0.5f, t);
            float offset = worldConfig.SpawnDistanceBeyondBoundary;

            return side switch
            {
                SpawnSide.Top => new Vector2(x, bounds.max.y + offset),
                SpawnSide.Bottom => new Vector2(x, bounds.min.y - offset),
                SpawnSide.Left => new Vector2(bounds.min.x - offset, y),
                SpawnSide.Right => new Vector2(bounds.max.x + offset, y),
                _ => throw new System.ArgumentOutOfRangeException(nameof(side), side, null)
            };
        }
    }
}
