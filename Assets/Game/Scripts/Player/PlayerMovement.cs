using UnityEngine;

namespace MicroJam.Game
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Health health;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        private Vector2 moveInput;

        public float MoveSpeed => moveSpeed;
        public Vector2 MoveInput => moveInput;
        public Vector2 DesiredVelocity => moveInput * moveSpeed;
        public Rigidbody2D Body => body;
        public Health Health => health;

        public void Configure(Rigidbody2D configuredBody, Health configuredHealth, float configuredSpeed)
        {
            body = configuredBody;
            health = configuredHealth;
            moveSpeed = Mathf.Max(0f, configuredSpeed);
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            bool canMove = health == null || !health.IsDead;
            body.linearVelocity = canMove ? DesiredVelocity : Vector2.zero;
            GameAudio.ReportHumanWalking(canMove && DesiredVelocity.sqrMagnitude > 0.01f);
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
        }
    }
}
