using UnityEngine;

namespace MicroJam.Game
{
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("idle");
        private static readonly int WalkState = Animator.StringToHash("walk");
        private static readonly int AttackState = Animator.StringToHash("atack");

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private Health health;
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.05f;
        [SerializeField, Min(0.01f)] private float attackAnimationDuration = 0.43f;

        private int currentState;
        private float attackEndsAt;

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>(true);
            movement ??= GetComponent<PlayerMovement>();
            combat ??= GetComponent<PlayerCombat>();
            health ??= GetComponent<Health>();
            PlayState(IdleState, true);
        }

        private void OnEnable()
        {
            if (combat != null) combat.AttackPerformed += HandleAttackPerformed;
        }

        private void OnDisable()
        {
            if (combat != null) combat.AttackPerformed -= HandleAttackPerformed;
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            if (health != null && health.IsDead)
            {
                PlayState(IdleState);
                return;
            }

            if (Time.time < attackEndsAt) return;

            bool walking = movement != null && movement.DesiredVelocity.sqrMagnitude > 0.01f;
            PlayState(walking ? WalkState : IdleState);
        }

        private void HandleAttackPerformed()
        {
            attackEndsAt = Time.time + attackAnimationDuration;
            PlayState(AttackState, true);
        }

        private void PlayState(int state, bool restart = false)
        {
            if (animator == null || animator.runtimeAnimatorController == null || !restart && currentState == state) return;
            animator.CrossFade(state, crossFadeDuration, 0, restart ? 0f : float.NegativeInfinity);
            currentState = state;
        }

        private void OnValidate()
        {
            crossFadeDuration = Mathf.Max(0f, crossFadeDuration);
            attackAnimationDuration = Mathf.Max(0.01f, attackAnimationDuration);
        }
    }
}
