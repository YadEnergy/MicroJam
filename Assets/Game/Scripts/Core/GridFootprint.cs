using UnityEngine;

namespace MicroJam.Game
{
    public sealed class GridFootprint : MonoBehaviour
    {
        [SerializeField] private Vector2Int sizeInCells = Vector2Int.one;

        public Vector2Int SizeInCells => sizeInCells;

        public void Configure(Vector2Int value)
        {
            sizeInCells = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
        }
    }
}
