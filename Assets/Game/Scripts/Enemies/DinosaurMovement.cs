using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class DinosaurMovement : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField, Min(0.01f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0.01f)] private float waypointTolerance = 0.08f;

        private readonly List<Vector2> path = new();
        private int waypointIndex;

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
        }

        public void FollowPath()
        {
            if (body == null || waypointIndex >= path.Count) return;

            Vector2 offset = path[waypointIndex] - body.position;
            if (offset.sqrMagnitude <= waypointTolerance * waypointTolerance)
            {
                waypointIndex++;
                if (waypointIndex >= path.Count) return;
                offset = path[waypointIndex] - body.position;
            }

            Vector2 direction = offset.normalized;
            float distance = Mathf.Min(moveSpeed * Time.fixedDeltaTime, offset.magnitude);
            body.MovePosition(body.position + direction * distance);
            if (visual != null && Mathf.Abs(direction.x) > 0.01f) visual.flipX = direction.x < 0f;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.01f, moveSpeed);
            waypointTolerance = Mathf.Max(0.01f, waypointTolerance);
        }
    }
}
