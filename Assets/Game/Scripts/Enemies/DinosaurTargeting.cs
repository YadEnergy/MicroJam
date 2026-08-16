using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    public enum DinosaurTargetState
    {
        None,
        Campfire,
        BreakingBuilding,
        ChasingPlayer,
        AttackingTower
    }

    [RequireComponent(typeof(Health), typeof(DinosaurMovement), typeof(DinosaurAttack))]
    public sealed class DinosaurTargeting : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private DinosaurMovement movement;
        [SerializeField] private DinosaurAttack attack;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.35f;
        [SerializeField, Min(0.1f)] private float playerChaseFailureTimeout = 10f;
        [SerializeField] private bool showPathGizmos;

        private DinosaurNavigationGrid navigation;
        private Health campfireHealth;
        private Health playerHealth;
        private Health towerHealth;
        private Health currentTarget;
        private float nextRepathTime;
        private float playerChaseExpiresAt;
        private int plannedRevision = -1;

        public DinosaurTargetState State { get; private set; }
        public Health CampfireHealth => campfireHealth;
        public Health RetaliatingPlayer => playerHealth;
        public Health RetaliatingTower => towerHealth;
        public Health CurrentTarget => currentTarget;
        public float PlayerChaseFailureTimeout => playerChaseFailureTimeout;
        public DinosaurNavigationGrid Navigation => navigation;

        public void Configure(
            Health configuredHealth,
            DinosaurMovement configuredMovement,
            DinosaurAttack configuredAttack,
            float configuredPlayerChaseFailureTimeout = 10f)
        {
            health = configuredHealth;
            movement = configuredMovement;
            attack = configuredAttack;
            playerChaseFailureTimeout = Mathf.Max(0.1f, configuredPlayerChaseFailureTimeout);
        }

        private void Awake()
        {
            health ??= GetComponent<Health>();
            movement ??= GetComponent<DinosaurMovement>();
            attack ??= GetComponent<DinosaurAttack>();
            navigation = FindFirstObjectByType<DinosaurNavigationGrid>();
            FindCampfire();
        }

        private void OnEnable()
        {
            if (health != null) health.DamageReceived += OnDamaged;
            if (attack != null) attack.SuccessfulAttack += OnSuccessfulAttack;
        }

        private void OnDisable()
        {
            if (health != null) health.DamageReceived -= OnDamaged;
            if (attack != null) attack.SuccessfulAttack -= OnSuccessfulAttack;
            UnsubscribePlayerDeath();
            UnsubscribeTowerDeath();
        }

        public void Initialize()
        {
            navigation ??= FindFirstObjectByType<DinosaurNavigationGrid>();
            FindCampfire();
            ForceRepath();
        }

        public void Tick()
        {
            if (navigation == null || movement == null || attack == null)
            {
                movement?.ClearPath();
                State = DinosaurTargetState.None;
                return;
            }

            if (playerHealth != null)
            {
                if (playerHealth.IsDead || Time.time >= playerChaseExpiresAt)
                {
                    CancelPlayerAggro();
                }
                else if (NeedsRepath())
                {
                    if (!TryPlanFreeRoute(playerHealth, DinosaurTargetState.ChasingPlayer))
                    {
                        CancelPlayerAggro();
                    }
                }
            }

            if (towerHealth != null)
            {
                if (towerHealth.IsDead)
                {
                    CancelTowerAggro();
                }
                else
                {
                    bool isBreakingTowerRoute = State == DinosaurTargetState.BreakingBuilding &&
                                                currentTarget != null && !currentTarget.IsDead;
                    bool towerRouteChanged = plannedRevision != navigation.Revision;
                    bool needsTowerRoute = currentTarget == null || currentTarget.IsDead ||
                                           (!isBreakingTowerRoute && NeedsRepath()) ||
                                           (isBreakingTowerRoute && towerRouteChanged);
                    if (needsTowerRoute) PlanTowerRoute();
                }
            }

            // Once a wall has been selected, keep attacking it until it is destroyed.
            // Replanning the sealed route every few tenths of a second is both unnecessary
            // and very expensive when several dinosaurs surround the same base.
            bool isBreakingSelectedBuilding = State == DinosaurTargetState.BreakingBuilding &&
                                             currentTarget != null && !currentTarget.IsDead;
            bool navigationChanged = plannedRevision != navigation.Revision;
            bool needsCampfireRoute = currentTarget == null || currentTarget.IsDead ||
                                      (!isBreakingSelectedBuilding && NeedsRepath()) ||
                                      (isBreakingSelectedBuilding && navigationChanged);
            if (playerHealth == null && towerHealth == null && needsCampfireRoute)
            {
                PlanCampfireRoute();
            }

            if (currentTarget == null || currentTarget.IsDead)
            {
                movement.ClearPath();
                return;
            }

            if (attack.IsWithinRange(currentTarget))
            {
                movement.ClearPath();
                attack.TryAttack(currentTarget);
            }
            else
            {
                movement.FollowPath();
            }
        }

        public bool TryAggroPlayer(Health player)
        {
            if (player == null || player.IsDead || navigation == null || attack == null) return false;
            if (!navigation.TryFindPathToTarget(transform.position, player, attack, movement.WaypointTolerance, false,
                    out List<Vector2> path, out _))
            {
                return false;
            }

            UnsubscribePlayerDeath();
            UnsubscribeTowerDeath();
            towerHealth = null;
            playerHealth = player;
            playerHealth.Died += OnPlayerDied;
            currentTarget = player;
            State = DinosaurTargetState.ChasingPlayer;
            playerChaseExpiresAt = Time.time + playerChaseFailureTimeout;
            AcceptPath(path);
            return true;
        }

        public bool TryAggroTower(Health tower)
        {
            if (tower == null || tower.IsDead || navigation == null || attack == null) return false;

            // Keep the first living tower that provoked this dinosaur. Without this lock,
            // alternating projectiles from nearby towers make the dinosaur constantly turn around.
            if (towerHealth != null && !towerHealth.IsDead) return towerHealth == tower;

            UnsubscribePlayerDeath();
            playerHealth = null;
            UnsubscribeTowerDeath();
            towerHealth = tower;
            towerHealth.Died += OnTowerDied;
            currentTarget = null;
            ForceRepath();
            PlanTowerRoute();
            return currentTarget != null;
        }

        private void PlanCampfireRoute()
        {
            FindCampfire();
            if (campfireHealth == null || campfireHealth.IsDead)
            {
                currentTarget = null;
                State = DinosaurTargetState.None;
                movement.ClearPath();
                return;
            }

            if (TryPlanFreeRoute(campfireHealth, DinosaurTargetState.Campfire)) return;

            if (navigation.TryFindPathToTarget(transform.position, campfireHealth, attack, movement.WaypointTolerance, true,
                    out _, out BuildingInstance blocker) && blocker != null && blocker.Health != null && !blocker.Health.IsDead &&
                navigation.TryFindPathToTarget(transform.position, blocker.Health, attack, movement.WaypointTolerance, false,
                    out List<Vector2> obstaclePath, out _))
            {
                currentTarget = blocker.Health;
                State = DinosaurTargetState.BreakingBuilding;
                AcceptPath(obstaclePath);
                return;
            }

            currentTarget = null;
            State = DinosaurTargetState.None;
            movement.ClearPath();
            ScheduleRepath();
        }

        private void PlanTowerRoute()
        {
            if (towerHealth == null || towerHealth.IsDead)
            {
                CancelTowerAggro();
                return;
            }

            if (TryPlanFreeRoute(towerHealth, DinosaurTargetState.AttackingTower)) return;

            // A retaliated tower can be behind walls, doors, or other towers. Break the first
            // obstruction, then resume the route to the originally locked tower.
            if (navigation.TryFindPathToTarget(transform.position, towerHealth, attack, movement.WaypointTolerance, true,
                    out _, out BuildingInstance blocker) && blocker != null && blocker.Health != null && !blocker.Health.IsDead &&
                navigation.TryFindPathToTarget(transform.position, blocker.Health, attack, movement.WaypointTolerance, false,
                    out List<Vector2> obstaclePath, out _))
            {
                currentTarget = blocker.Health;
                State = DinosaurTargetState.BreakingBuilding;
                AcceptPath(obstaclePath);
                return;
            }

            currentTarget = null;
            State = DinosaurTargetState.None;
            movement.ClearPath();
            ScheduleRepath();
        }

        private bool TryPlanFreeRoute(Health target, DinosaurTargetState state)
        {
            if (!navigation.TryFindPathToTarget(transform.position, target, attack, movement.WaypointTolerance, false,
                    out List<Vector2> path, out _)) return false;

            currentTarget = target;
            State = state;
            AcceptPath(path);
            return true;
        }

        private void AcceptPath(List<Vector2> path)
        {
            movement.SetPath(path);
            plannedRevision = navigation.Revision;
            ScheduleRepath();
        }

        private bool NeedsRepath() => Time.time >= nextRepathTime || plannedRevision != navigation.Revision;
        private void ScheduleRepath() => nextRepathTime = Time.time + repathInterval;

        private void ForceRepath()
        {
            nextRepathTime = 0f;
            plannedRevision = -1;
        }

        private void CancelPlayerAggro()
        {
            UnsubscribePlayerDeath();
            playerHealth = null;
            currentTarget = null;
            ForceRepath();
            PlanCampfireRoute();
        }

        private void CancelTowerAggro()
        {
            UnsubscribeTowerDeath();
            towerHealth = null;
            currentTarget = null;
            ForceRepath();
            PlanCampfireRoute();
        }

        private void OnDamaged(DamageReceivedEvent damage)
        {
            if (damage.Source == null) return;
            PlayerCombat combat = damage.Source.GetComponentInParent<PlayerCombat>();
            Health player = combat != null ? combat.GetComponent<Health>() : null;
            if (player != null)
            {
                TryAggroPlayer(player);
                return;
            }

            TowerCombat tower = damage.Source.GetComponentInParent<TowerCombat>();
            if (tower != null) TryAggroTower(tower.Health);
        }

        private void OnSuccessfulAttack(Health attacked)
        {
            if (playerHealth != null && attacked == playerHealth)
            {
                playerChaseExpiresAt = Time.time + playerChaseFailureTimeout;
            }
        }

        private void OnPlayerDied(DeathEvent _) => CancelPlayerAggro();
        private void OnTowerDied(DeathEvent _) => CancelTowerAggro();

        private void UnsubscribePlayerDeath()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        private void UnsubscribeTowerDeath()
        {
            if (towerHealth != null) towerHealth.Died -= OnTowerDied;
        }

        private void FindCampfire()
        {
            if (campfireHealth != null && !campfireHealth.IsDead) return;
            GameObject campfire = GameObject.Find("Campfire");
            campfireHealth = campfire != null ? campfire.GetComponent<Health>() : null;
        }

        private void OnValidate()
        {
            repathInterval = Mathf.Max(0.05f, repathInterval);
            playerChaseFailureTimeout = Mathf.Max(0.1f, playerChaseFailureTimeout);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showPathGizmos || movement == null) return;
            IReadOnlyList<Vector2> path = movement.CurrentPath;
            Gizmos.color = State == DinosaurTargetState.BreakingBuilding ? Color.red :
                State == DinosaurTargetState.ChasingPlayer ? Color.yellow :
                State == DinosaurTargetState.AttackingTower ? Color.magenta : Color.cyan;
            Vector3 previous = transform.position;
            for (int i = 0; i < path.Count; i++)
            {
                Gizmos.DrawLine(previous, path[i]);
                Gizmos.DrawSphere(path[i], 0.07f);
                previous = path[i];
            }
        }
    }
}
