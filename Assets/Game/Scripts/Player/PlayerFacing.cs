using UnityEngine;

namespace MicroJam.Game
{
    public sealed class PlayerFacing : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private SquareGameplayViewport gameplayViewport;
        [SerializeField] private Transform facingVisualRoot;
        [SerializeField] private Vector2 initialFacingDirection = Vector2.right;
        [SerializeField, Min(0f)] private float minimumDirectionDistance = 0.01f;

        private Vector2 facingDirection = Vector2.right;

        public Vector2 FacingDirection => facingDirection;
        public SquareGameplayViewport GameplayViewport => gameplayViewport;
        public Transform FacingVisualRoot => facingVisualRoot;
        public Health Health => health;

        public void Configure(
            Health configuredHealth,
            SquareGameplayViewport configuredViewport,
            Transform configuredVisualRoot,
            Vector2 initialDirection)
        {
            health = configuredHealth;
            gameplayViewport = configuredViewport;
            facingVisualRoot = configuredVisualRoot;
            initialFacingDirection = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : Vector2.right;
            SetFacingDirection(initialFacingDirection);
        }

        public void SetGameplayViewport(SquareGameplayViewport viewport) => gameplayViewport = viewport;

        public bool TrySetFacingFromScreen(Vector2 screenPosition)
        {
            if (health != null && health.IsDead)
            {
                return false;
            }

            if (gameplayViewport == null || !gameplayViewport.TryScreenToWorld(screenPosition, out Vector2 worldPosition))
            {
                return false;
            }

            Vector2 direction = worldPosition - (Vector2)transform.position;
            if (direction.sqrMagnitude < minimumDirectionDistance * minimumDirectionDistance)
            {
                return false;
            }

            SetFacingDirection(direction);
            return true;
        }

        public bool SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            facingDirection = direction.normalized;
            if (facingVisualRoot != null)
            {
                float angle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
                facingVisualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            return true;
        }

        private void Awake()
        {
            SetFacingDirection(initialFacingDirection);
        }

        private void OnValidate()
        {
            minimumDirectionDistance = Mathf.Max(0f, minimumDirectionDistance);
            if (initialFacingDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                initialFacingDirection = Vector2.right;
            }
        }
    }
}
