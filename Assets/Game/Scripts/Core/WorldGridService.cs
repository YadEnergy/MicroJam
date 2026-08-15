using UnityEngine;

namespace MicroJam.Game
{
    public sealed class WorldGridService : MonoBehaviour
    {
        [SerializeField] private WorldGridConfig config;

        public WorldGridConfig Config => config;

        public void Configure(WorldGridConfig value) => config = value;

        public Vector2 CellToWorldCenter(Vector2Int cell) => config.CellToWorldCenter(cell);
        public Vector2Int WorldToCell(Vector2 worldPosition) => config.WorldToCell(worldPosition);
        public bool IsCellInsideBuildZone(Vector2Int cell) => config.IsCellInsideBuildZone(cell);
        public bool IsCellProtectedFromBuilding(Vector2Int cell) => config.IsCellProtectedFromBuilding(cell);
        public bool CanBuildAt(Vector2Int cell) => config.CanBuildAt(cell);

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("WorldGridService requires a WorldGridConfig asset.", this);
            }
        }
    }
}
