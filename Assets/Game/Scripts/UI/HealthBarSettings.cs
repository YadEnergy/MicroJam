using UnityEngine;

namespace MicroJam.Game
{
    [CreateAssetMenu(fileName = "HealthBarSettings", menuName = "MicroJam/Health Bar Settings")]
    public sealed class HealthBarSettings : ScriptableObject
    {
        [SerializeField] private Color friendlyColor = new(0.12f, 0.85f, 0.22f, 1f);
        [SerializeField] private Color enemyColor = new(0.9f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color backgroundColor = new(0.06f, 0.07f, 0.08f, 0.9f);
        [SerializeField, Min(0f)] private float damagedVisibleDuration = 3f;

        public Color FriendlyColor => friendlyColor;
        public Color EnemyColor => enemyColor;
        public Color BackgroundColor => backgroundColor;
        public float DamagedVisibleDuration => damagedVisibleDuration;

        private void OnValidate()
        {
            damagedVisibleDuration = Mathf.Max(0f, damagedVisibleDuration);
        }
    }
}
