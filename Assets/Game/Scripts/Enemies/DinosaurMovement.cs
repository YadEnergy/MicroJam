using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class DinosaurMovement : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Transform healthBarRoot;
        [SerializeField, Min(0.01f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0.01f)] private float waypointTolerance = 0.08f;
        [SerializeField, Min(1f)] private float pushResistanceMass = 100f;
        [SerializeField, Min(0.1f)] private float playerAvoidanceDistance = 0.8f;
        [SerializeField, Range(0.1f, 0.95f)] private float avoidanceForwardWeight = 0.45f;

        private readonly List<Vector2> path = new();
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[4];
        private int waypointIndex;
        private float avoidanceSide;
        private float keepAvoidanceSideUntil;
        private float facingAngle;

        public Vector2 Position => body != null ? body.position : transform.position;
        public IReadOnlyList<Vector2> CurrentPath => path;
        public bool HasPath => waypointIndex < path.Count;
        public float MoveSpeed => moveSpeed;
        public float WaypointTolerance => waypointTolerance;

        public void Configure(Rigidbody2D configuredBody, SpriteRenderer configuredVisual)
        {
            body = configuredBody;
            visual = configuredVisual;
        }

        private void Awake()
        {
            body ??= GetComponent<Rigidbody2D>();
            visual ??= transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            healthBarRoot ??= transform.Find("HealthBarAnchor");
            if (visual != null)
            {
                visual.flipX = false;
            }
            if (body != null) body.mass = Mathf.Max(body.mass, pushResistanceMass);
        }

        public void SetPath(IReadOnlyList<Vector2> waypoints)
        {
            path.Clear();
            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Count; i++) path.Add(waypoints[i]);
            }

            waypointIndex = path.Count > 1 && (path[0] - Position).sqrMagnitude < 0.5f ? 1 : 0;
        }

        public void ClearPath()
        {
            path.Clear();
            waypointIndex = 0;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public void FollowPath()
        {
            if (body == null || waypointIndex >= path.Count) return;

            // A collision with the Player may leave a small physics impulse on this dynamic
            // body. Dinosaur locomotion is fully controlled here, so carrying that impulse
            // into the next path step only produces visible oscillation.
            body.linearVelocity = Vector2.zero;

            Vector2 offset = path[waypointIndex] - body.position;
            if (offset.sqrMagnitude <= waypointTolerance * waypointTolerance)
            {
                waypointIndex++;
                if (waypointIndex >= path.Count) return;
                offset = path[waypointIndex] - body.position;
            }

            Vector2 direction = offset.normalized;
            direction = AvoidPlayer(direction);
            float distance = Mathf.Min(moveSpeed * Time.fixedDeltaTime, offset.magnitude);
            body.MovePosition(body.position + direction * distance);
            UpdateFacing(direction);
            GameAudio.ReportDinosaurWalking();
        }

        public void FaceTowards(Vector2 worldPosition)
        {
            UpdateFacing(worldPosition - Position);
        }

        private void UpdateFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (body != null)
            {
                body.angularVelocity = 0f;
                // Keep the actor root fixed so child UI anchors do not orbit around it.
                // Only the authored dinosaur visual rotates to face movement/attack direction.
                body.SetRotation(0f);
            }
        }

        private void LateUpdate()
        {
            if (visual == null) return;

            // Rotate only the sprite. The root, collider, and HealthBarAnchor stay aligned
            // with the world, keeping the health bar horizontally above the dinosaur.
            Transform visualTransform = visual.transform;
            Vector3 scale = visualTransform.localScale;
            scale.x = Mathf.Abs(scale.x);
            visualTransform.localScale = scale;
            visualTransform.rotation = Quaternion.Euler(0f, 0f, facingAngle);
            visual.flipX = false;
            if (healthBarRoot != null) healthBarRoot.rotation = Quaternion.identity;
        }

        private Vector2 AvoidPlayer(Vector2 forward)
        {
            int playerLayer = GameLayers.PlayerIndex;
            if (body == null || playerLayer < 0) return forward;

            ContactFilter2D filter = new();
            filter.SetLayerMask(1 << playerLayer);
            filter.useTriggers = false;
            int hitCount = body.Cast(forward, filter, castHits, playerAvoidanceDistance);
            if (hitCount == 0)
            {
                if (Time.time >= keepAvoidanceSideUntil) avoidanceSide = 0f;
                return forward;
            }

            Vector2 playerOffset = castHits[0].centroid - body.position;
            if (Mathf.Approximately(avoidanceSide, 0f))
            {
                float cross = forward.x * playerOffset.y - forward.y * playerOffset.x;
                avoidanceSide = Mathf.Abs(cross) > 0.05f
                    ? -Mathf.Sign(cross)
                    : (GetInstanceID() & 1) == 0 ? 1f : -1f;
            }

            // Extend the lock while the Player is still blocking the route. Previously the
            // side was recalculated during continuous contact and could alternate each time.
            keepAvoidanceSideUntil = Time.time + 0.6f;

            Vector2 sideways = new(-forward.y * avoidanceSide, forward.x * avoidanceSide);
            return (forward * avoidanceForwardWeight + sideways * (1f - avoidanceForwardWeight)).normalized;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.01f, moveSpeed);
            waypointTolerance = Mathf.Max(0.01f, waypointTolerance);
            pushResistanceMass = Mathf.Max(1f, pushResistanceMass);
            playerAvoidanceDistance = Mathf.Max(0.1f, playerAvoidanceDistance);
            avoidanceForwardWeight = Mathf.Clamp(avoidanceForwardWeight, 0.1f, 0.95f);
        }
    }
}
