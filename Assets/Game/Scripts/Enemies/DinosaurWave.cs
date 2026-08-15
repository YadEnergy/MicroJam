using UnityEngine;

namespace MicroJam.Game
{
    // Wave data is saved as a standalone asset and can be shared by multiple spawners.
    [CreateAssetMenu(fileName = "DinosaurWave", menuName = "MicroJam/Dinosaurs/Wave")]
    public sealed class DinosaurWave : ScriptableObject
    {
        [SerializeField, Min(0)] private int coinBank = 50;
        [SerializeField, Min(0.01f)] private float spawnInterval = 0.75f;
        [SerializeField] private DinosaurAgent[] allowedDinosaurs;

        public int CoinBank => coinBank;
        public float SpawnInterval => spawnInterval;
        public DinosaurAgent[] AllowedDinosaurs => allowedDinosaurs;

        private void OnValidate()
        {
            coinBank = Mathf.Max(0, coinBank);
            spawnInterval = Mathf.Max(0.01f, spawnInterval);
        }
    }
}
