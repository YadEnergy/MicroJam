using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        private static readonly Vector2[] SearchDirections =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1f, 1f).normalized, new Vector2(-1f, 1f).normalized,
            new Vector2(1f, -1f).normalized, new Vector2(-1f, -1f).normalized
        };

        [Header("Player")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private GameObject playerVisual;

        [Header("World")]
        [SerializeField] private CampfireInteraction campfire;
        [SerializeField] private WorldGridService worldGrid;
        [SerializeField] private BuildingSystem buildingSystem;
        [SerializeField] private WorldInteractionController worldInteraction;
        [SerializeField] private LayerMask blockedRespawnLayers;
        [SerializeField, Min(1)] private int searchRings = 8;
        [SerializeField, Min(0.05f)] private float searchStep = 1f;

        [Header("Death UI")]
        [SerializeField] private UIPanelTween deathOverlay;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text countdownText;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float respawnDelay = 10f;
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 3f;

        private readonly List<Collider2D> overlapResults = new(32);
        private Coroutine respawnRoutine;
        private Coroutine invulnerabilityRoutine;
        private bool gameHasEnded;

        public Health PlayerHealth => playerHealth;
        public UIPanelTween DeathOverlay => deathOverlay;
        public TMP_Text CountdownText => countdownText;
        public float RespawnDelay => respawnDelay;
        public float InvulnerabilityDuration => invulnerabilityDuration;
        public bool IsRespawning { get; private set; }
        public bool IsInvulnerable => playerHealth != null && playerHealth.IsInvulnerable;

        public void ConfigureTiming(float configuredRespawnDelay, float configuredInvulnerabilityDuration)
        {
            respawnDelay = Mathf.Max(0.1f, configuredRespawnDelay);
            invulnerabilityDuration = Mathf.Max(0f, configuredInvulnerabilityDuration);
        }

        public void Configure(Health health, Rigidbody2D body, Collider2D bodyCollider, GameObject visual,
            CampfireInteraction configuredCampfire, WorldGridService grid, BuildingSystem buildings,
            WorldInteractionController interactions, LayerMask blockedLayers, UIPanelTween overlay,
            TMP_Text configuredStatusText, TMP_Text configuredCountdownText,
            float configuredRespawnDelay = 10f, float configuredInvulnerabilityDuration = 3f)
        {
            playerHealth = health;
            playerBody = body;
            playerCollider = bodyCollider;
            playerVisual = visual;
            campfire = configuredCampfire;
            worldGrid = grid;
            buildingSystem = buildings;
            worldInteraction = interactions;
            blockedRespawnLayers = blockedLayers;
            deathOverlay = overlay;
            statusText = configuredStatusText;
            countdownText = configuredCountdownText;
            respawnDelay = Mathf.Max(0.1f, configuredRespawnDelay);
            invulnerabilityDuration = Mathf.Max(0f, configuredInvulnerabilityDuration);
        }

        public Vector2 FindRespawnPosition()
        {
            if (campfire == null) return playerBody != null ? playerBody.position : Vector2.zero;
            Collider2D campfireCollider = campfire.GetComponent<Collider2D>();
            Vector2 center = campfireCollider != null ? campfireCollider.bounds.center : campfire.transform.position;
            float campfireRadius = campfireCollider != null
                ? Mathf.Max(campfireCollider.bounds.extents.x, campfireCollider.bounds.extents.y)
                : 1.5f;
            float playerRadius = playerCollider != null
                ? Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y)
                : 0.4f;
            float initialDistance = campfireRadius + playerRadius + 0.25f;

            for (int ring = 0; ring < searchRings; ring++)
            {
                float distance = initialDistance + ring * searchStep;
                foreach (Vector2 direction in SearchDirections)
                {
                    Vector2 candidate = center + direction * distance;
                    if (IsRespawnPositionClear(candidate, playerRadius)) return candidate;
                }
            }

            return center + Vector2.right * (initialDistance + searchRings * searchStep);
        }

        public bool IsRespawnPositionClear(Vector2 position, float radius)
        {
            if (worldGrid != null && worldGrid.Config != null &&
                !worldGrid.Config.IsCellInsidePlayableArea(worldGrid.WorldToCell(position))) return false;

            ContactFilter2D filter = new();
            filter.SetLayerMask(blockedRespawnLayers);
            filter.useTriggers = false;
            overlapResults.Clear();
            Physics2D.OverlapCircle(position, Mathf.Max(0.05f, radius), filter, overlapResults);
            for (int i = 0; i < overlapResults.Count; i++)
            {
                Collider2D hit = overlapResults[i];
                if (hit == null || hit == playerCollider) continue;
                return false;
            }

            return true;
        }

        private void Awake()
        {
            if (statusText != null) statusText.text = "RESPAWNING";
            deathOverlay?.SetHiddenImmediate();
        }

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.Died += HandlePlayerDied;
            GameEvents.CampfireDestroyed += HandleGameEnded;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.Died -= HandlePlayerDied;
            GameEvents.CampfireDestroyed -= HandleGameEnded;
            GameplayInputGate.SetBlocked(this, false);
        }

        private void HandlePlayerDied(DeathEvent _)
        {
            if (gameHasEnded || IsRespawning || respawnRoutine != null) return;
            respawnRoutine = StartCoroutine(RespawnSequence());
        }

        private void HandleGameEnded()
        {
            gameHasEnded = true;
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            if (invulnerabilityRoutine != null) StopCoroutine(invulnerabilityRoutine);
            respawnRoutine = null;
            invulnerabilityRoutine = null;
            IsRespawning = false;
            playerHealth?.SetInvulnerable(false);
            countdownText?.rectTransform.DOKill(false);
            deathOverlay?.SetHiddenImmediate();
            GameplayInputGate.SetBlocked(this, false);
        }

        private IEnumerator RespawnSequence()
        {
            IsRespawning = true;
            GameplayInputGate.SetBlocked(this, true);
            buildingSystem?.CancelBuildMode();
            worldInteraction?.CloseAll();
            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.simulated = false;
            }
            if (playerCollider != null) playerCollider.enabled = false;
            if (playerVisual != null) playerVisual.SetActive(false);
            deathOverlay?.Show();

            float remaining = respawnDelay;
            while (remaining > 0f)
            {
                UpdateCountdown(Mathf.CeilToInt(remaining));
                float wait = Mathf.Min(1f, remaining);
                yield return new WaitForSeconds(wait);
                remaining -= wait;
            }

            UpdateCountdown(0);
            RespawnPlayer();
            bool closeFinished = false;
            if (deathOverlay != null) deathOverlay.Hide(() => closeFinished = true);
            else closeFinished = true;
            while (!closeFinished) yield return null;

            GameplayInputGate.SetBlocked(this, false);
            IsRespawning = false;
            respawnRoutine = null;
        }

        private void RespawnPlayer()
        {
            Vector2 position = FindRespawnPosition();
            if (playerBody != null)
            {
                playerBody.position = position;
                playerBody.transform.position = position;
                playerBody.linearVelocity = Vector2.zero;
                playerBody.simulated = true;
            }
            if (playerCollider != null) playerCollider.enabled = true;
            playerHealth?.Revive(playerHealth.MaxHealth);
            if (playerVisual != null) playerVisual.SetActive(true);
            if (playerHealth != null) playerHealth.SetInvulnerable(invulnerabilityDuration > 0f);
            if (invulnerabilityRoutine != null) StopCoroutine(invulnerabilityRoutine);
            if (invulnerabilityDuration > 0f) invulnerabilityRoutine = StartCoroutine(EndInvulnerability());
        }

        private IEnumerator EndInvulnerability()
        {
            yield return new WaitForSeconds(invulnerabilityDuration);
            playerHealth?.SetInvulnerable(false);
            invulnerabilityRoutine = null;
        }

        private void UpdateCountdown(int value)
        {
            if (countdownText == null) return;
            countdownText.text = value.ToString();
            countdownText.rectTransform.DOKill(false);
            countdownText.rectTransform.localScale = Vector3.one;
            countdownText.rectTransform.DOPunchScale(Vector3.one * 0.14f, 0.25f, 4, 0.35f)
                .SetUpdate(true).SetLink(countdownText.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void OnValidate()
        {
            respawnDelay = Mathf.Max(0.1f, respawnDelay);
            invulnerabilityDuration = Mathf.Max(0f, invulnerabilityDuration);
            searchRings = Mathf.Max(1, searchRings);
            searchStep = Mathf.Max(0.05f, searchStep);
        }
    }
}
