using UnityEngine;

namespace MicroJam.Game
{
    public enum BuildingType
    {
        Wall,
        Door
    }

    [CreateAssetMenu(fileName = "BuildingDefinition", menuName = "MicroJam/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [SerializeField] private BuildingType buildingType;
        [SerializeField] private string displayName = "Building";
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int woodCost;
        [SerializeField] private Vector2Int footprintSize = Vector2Int.one;
        [SerializeField] private bool blocksPlayer = true;
        [SerializeField] private bool blocksDinosaur = true;
        [SerializeField] private Color placeholderColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float removalRefundPercent = 0.5f;

        public BuildingType BuildingType => buildingType;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public int WoodCost => woodCost;
        public Vector2Int FootprintSize => footprintSize;
        public bool BlocksPlayer => blocksPlayer;
        public bool BlocksDinosaur => blocksDinosaur;
        public Color PlaceholderColor => placeholderColor;
        public float RemovalRefundPercent => removalRefundPercent;
        public int RemovalRefundWood => Mathf.CeilToInt(woodCost * removalRefundPercent);

        public void Configure(
            BuildingType configuredType,
            string configuredName,
            GameObject configuredPrefab,
            int configuredWoodCost,
            Vector2Int configuredFootprint,
            bool configuredBlocksPlayer,
            bool configuredBlocksDinosaur,
            Color configuredPlaceholderColor,
            float configuredRemovalRefundPercent = 0.5f)
        {
            buildingType = configuredType;
            displayName = string.IsNullOrWhiteSpace(configuredName) ? configuredType.ToString() : configuredName;
            prefab = configuredPrefab;
            woodCost = Mathf.Max(0, configuredWoodCost);
            footprintSize = new Vector2Int(Mathf.Max(1, configuredFootprint.x), Mathf.Max(1, configuredFootprint.y));
            blocksPlayer = configuredBlocksPlayer;
            blocksDinosaur = configuredBlocksDinosaur;
            placeholderColor = configuredPlaceholderColor;
            removalRefundPercent = Mathf.Clamp01(configuredRemovalRefundPercent);
        }

        private void OnValidate()
        {
            woodCost = Mathf.Max(0, woodCost);
            footprintSize = new Vector2Int(Mathf.Max(1, footprintSize.x), Mathf.Max(1, footprintSize.y));
            removalRefundPercent = Mathf.Clamp01(removalRefundPercent);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = buildingType.ToString();
            }
        }
    }
}
