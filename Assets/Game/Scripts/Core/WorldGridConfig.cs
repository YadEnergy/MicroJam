using UnityEngine;

namespace MicroJam.Game
{
    [CreateAssetMenu(fileName = "WorldGridConfig", menuName = "MicroJam/World Grid Config")]
    public sealed class WorldGridConfig : ScriptableObject
    {
        [Header("Grid")]
        [SerializeField, Min(0.01f)] private float tileSize = 1f;
        [SerializeField] private Vector2Int playableSize = new(50, 50);

        [Header("Central Build Area")]
        [SerializeField] private Vector2Int buildZoneSize = new(30, 30);
        [SerializeField] private Vector2Int campfireFootprint = new(3, 3);
        [SerializeField, Min(0)] private int campfireNoBuildPadding = 1;

        [Header("Future Dinosaur Spawning")]
        [SerializeField, Min(0.01f)] private float spawnDistanceBeyondBoundary = 1f;

        public float TileSize => tileSize;
        public Vector2Int PlayableSize => playableSize;
        public Vector2Int BuildZoneSize => buildZoneSize;
        public Vector2Int CampfireFootprint => campfireFootprint;
        public int CampfireNoBuildPadding => campfireNoBuildPadding;
        public float SpawnDistanceBeyondBoundary => spawnDistanceBeyondBoundary;

        public Vector2 WorldSize => new(playableSize.x * tileSize, playableSize.y * tileSize);
        public Vector2 WorldMin => -WorldSize * 0.5f;
        public Bounds PlayableWorldBounds => new(Vector3.zero, new Vector3(WorldSize.x, WorldSize.y, 0f));

        public RectInt PlayableCellRect => new(Vector2Int.zero, playableSize);
        public RectInt BuildZoneCellRect => CenteredCellRect(buildZoneSize);
        public RectInt CampfireCellRect => CenteredCellRect(campfireFootprint);

        public RectInt ProtectedCampfireCellRect
        {
            get
            {
                RectInt footprint = CampfireCellRect;
                int padding = campfireNoBuildPadding;
                return new RectInt(
                    footprint.xMin - padding,
                    footprint.yMin - padding,
                    footprint.width + padding * 2,
                    footprint.height + padding * 2);
            }
        }

        public Bounds BuildZoneWorldBounds => CellRectToWorldBounds(BuildZoneCellRect);
        public Bounds CampfireWorldBounds => CellRectToWorldBounds(CampfireCellRect);
        public Bounds ProtectedCampfireWorldBounds => CellRectToWorldBounds(ProtectedCampfireCellRect);
        public Vector2 CampfireWorldCenter => CampfireWorldBounds.center;

        public bool IsCellInsidePlayableArea(Vector2Int cell) => PlayableCellRect.Contains(cell);
        public bool IsCellInsideBuildZone(Vector2Int cell) => BuildZoneCellRect.Contains(cell);
        public bool IsCellProtectedFromBuilding(Vector2Int cell) => ProtectedCampfireCellRect.Contains(cell);
        public bool CanBuildAt(Vector2Int cell) => IsCellInsideBuildZone(cell) && !IsCellProtectedFromBuilding(cell);

        public Vector2 CellToWorldCenter(Vector2Int cell)
        {
            return WorldMin + new Vector2((cell.x + 0.5f) * tileSize, (cell.y + 0.5f) * tileSize);
        }

        public Vector2Int WorldToCell(Vector2 worldPosition)
        {
            Vector2 local = (worldPosition - WorldMin) / tileSize;
            return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }

        public Bounds CellRectToWorldBounds(RectInt cellRect)
        {
            Vector2 min = WorldMin + new Vector2(cellRect.xMin * tileSize, cellRect.yMin * tileSize);
            Vector2 size = new(cellRect.width * tileSize, cellRect.height * tileSize);
            return new Bounds(min + size * 0.5f, new Vector3(size.x, size.y, 0f));
        }

        private RectInt CenteredCellRect(Vector2Int size)
        {
            // Even maps cannot place an odd footprint on their exact geometric center while
            // keeping every edge grid-aligned. Integer division supplies one stable tie-break.
            Vector2Int start = new((playableSize.x - size.x) / 2, (playableSize.y - size.y) / 2);
            return new RectInt(start, size);
        }

        private void OnValidate()
        {
            tileSize = Mathf.Max(0.01f, tileSize);
            playableSize = new Vector2Int(Mathf.Max(1, playableSize.x), Mathf.Max(1, playableSize.y));
            buildZoneSize = ClampSize(buildZoneSize, playableSize);
            campfireFootprint = ClampSize(campfireFootprint, buildZoneSize);
            campfireNoBuildPadding = Mathf.Max(0, campfireNoBuildPadding);
            spawnDistanceBeyondBoundary = Mathf.Max(0.01f, spawnDistanceBeyondBoundary);
        }

        private static Vector2Int ClampSize(Vector2Int value, Vector2Int maximum)
        {
            return new Vector2Int(
                Mathf.Clamp(value.x, 1, maximum.x),
                Mathf.Clamp(value.y, 1, maximum.y));
        }
    }
}
