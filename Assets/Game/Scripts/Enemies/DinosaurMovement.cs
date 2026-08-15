using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class DinosaurMovement : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField, Min(0.01f)] private float moveSpeed = 2.5f;
        [SerializeField] private LayerMask resourceLayers;
        [SerializeField, Min(0.01f)] private float avoidanceRadius = 0.42f;
        [SerializeField, Min(0.01f)] private float avoidanceLookAhead = 1.25f;
        [SerializeField, Range(10f, 90f)] private float avoidanceAngleStep = 35f;

        public Vector2 Position => body != null ? body.position : transform.position;

        private void Awake()
        {
            body ??= GetComponent<Rigidbody2D>();
            visual ??= transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            if (resourceLayers.value == 0 && GameLayers.ResourceIndex >= 0)
            {
                resourceLayers = 1 << GameLayers.ResourceIndex;
            }
        }

        public void MoveTowards(Vector2 targetPosition, float stoppingDistance)
        {
            if (body == null)
            {
                return;
            }

            Vector2 offset = targetPosition - body.position;
            if (offset.sqrMagnitude <= stoppingDistance * stoppingDistance)
            {
                return;
            }

            Vector2 direction = GetSteeringDirection(offset.normalized);
            body.MovePosition(body.position + direction * moveSpeed * Time.fixedDeltaTime);
            if (visual != null && Mathf.Abs(direction.x) > 0.01f)
            {
                visual.flipX = direction.x < 0f;
            }
        }

        private Vector2 GetSteeringDirection(Vector2 desiredDirection)
        {
            if (!IsResourceAhead(desiredDirection))
            {
                return desiredDirection;
            }

            for (int multiplier = 1; multiplier <= 3; multiplier++)
            {
                float angle = avoidanceAngleStep * multiplier;
                Vector2 left = Rotate(desiredDirection, angle);
                if (!IsResourceAhead(left)) return left;

                Vector2 right = Rotate(desiredDirection, -angle);
                if (!IsResourceAhead(right)) return right;
            }

            return desiredDirection;
        }

        private bool IsResourceAhead(Vector2 direction) => resourceLayers.value != 0 &&
            Physics2D.CircleCast(body.position, avoidanceRadius, direction, avoidanceLookAhead, resourceLayers).collider != null;

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(
                direction.x * Mathf.Cos(radians) - direction.y * Mathf.Sin(radians),
                direction.x * Mathf.Sin(radians) + direction.y * Mathf.Cos(radians));
        }
    }
}
