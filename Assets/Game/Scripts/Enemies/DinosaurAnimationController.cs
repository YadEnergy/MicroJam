using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    [RequireComponent(typeof(Health), typeof(DinosaurMovement), typeof(DinosaurAttack))]
    public sealed class DinosaurAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Health health;
        [SerializeField] private DinosaurMovement movement;
        [SerializeField] private DinosaurAttack attack;

        private readonly List<AnimationClip> attackClips = new();
        private AnimationClip runClip;
        private AnimationClip deathClip;
        private AnimationClip activeAttack;
        private float activeAttackEndsAt;
        private bool attackQueued;
        private bool dying;
        private int previousAttackIndex = -1;

        public bool IsDying => dying;

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>(true);
            health ??= GetComponent<Health>();
            movement ??= GetComponent<DinosaurMovement>();
            attack ??= GetComponent<DinosaurAttack>();
            FindAnimationClips();
        }

        private void OnEnable()
        {
            if (attack != null) attack.SuccessfulAttack += OnSuccessfulAttack;
        }

        private void OnDisable()
        {
            if (attack != null) attack.SuccessfulAttack -= OnSuccessfulAttack;
        }

        private void Update()
        {
            if (dying || animator == null) return;

            bool isRunning = movement != null && movement.HasPath;
            if (isRunning)
            {
                attackQueued = false;
                activeAttack = null;
                PlayIfNeeded(runClip);
                return;
            }

            if (activeAttack != null && Time.time < activeAttackEndsAt) return;
            activeAttack = null;
            if (attackQueued) PlayRandomAttack();
        }

        public bool PlayDeathAndDestroy()
        {
            if (dying) return true;
            if (animator == null || deathClip == null) return false;

            dying = true;
            attackQueued = false;
            activeAttack = null;
            movement?.ClearPath();

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }

            foreach (Collider2D bodyCollider in GetComponentsInChildren<Collider2D>())
            {
                bodyCollider.enabled = false;
            }

            animator.Play(deathClip.name, 0, 0f);
            StartCoroutine(DestroyAfterDeathAnimation(deathClip.length));
            return true;
        }

        private void OnSuccessfulAttack(Health _)
        {
            if (dying || attackClips.Count == 0) return;
            attackQueued = true;
            if (movement == null || !movement.HasPath)
            {
                if (activeAttack == null || Time.time >= activeAttackEndsAt) PlayRandomAttack();
            }
        }

        private void PlayRandomAttack()
        {
            attackQueued = false;
            int index = Random.Range(0, attackClips.Count);
            if (attackClips.Count > 1 && index == previousAttackIndex)
            {
                index = (index + Random.Range(1, attackClips.Count)) % attackClips.Count;
            }

            previousAttackIndex = index;
            activeAttack = attackClips[index];
            activeAttackEndsAt = Time.time + Mathf.Max(0.01f, activeAttack.length);
            animator.Play(activeAttack.name, 0, 0f);
        }

        private void PlayIfNeeded(AnimationClip clip)
        {
            if (clip == null) return;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(clip.name)) animator.Play(clip.name, 0, 0f);
        }

        private void FindAnimationClips()
        {
            attackClips.Clear();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                string clipName = clip.name.ToLowerInvariant();
                if (clipName.Contains("die") || clipName.Contains("death")) deathClip = clip;
                else if (clipName.Contains("run")) runClip = clip;
                else if (clipName.Contains("attack") || clipName.Contains("atack")) attackClips.Add(clip);
            }
        }

        private IEnumerator DestroyAfterDeathAnimation(float duration)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
            Destroy(gameObject);
        }
    }
}
