using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MicroJam.Game
{
    /// <summary>
    /// A one-time, action-driven introduction to the core game loop.
    /// It listens to the existing gameplay events, so a step only completes when the player actually does it.
    /// </summary>
    public sealed class TutorialManager : MonoBehaviour
    {
        private const string CompletedPreferenceKey = "MicroJam.Tutorial.Completed";

        private enum Step
        {
            Move,
            Attack,
            HealAtBush,
            GatherWood,
            GatherStone,
            CampfireGoal,
            SelectWall,
            PlaceWall,
            SelectDoor,
            PlaceDoor,
            SellBuilding,
            FightDinosaur,
            Complete
        }

        [Header("Progress")]
        [SerializeField, Min(0.1f)] private float movementDistance = 1.5f;
        [SerializeField, Min(0f)] private float campfireMessageDuration = 4f;

        [Header("Scene UI")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private TMP_Text messageLabel;

        private PlayerMovement playerMovement;
        private PlayerCombat playerCombat;
        private PlayerResourceWallet wallet;
        private BuildingSystem buildingSystem;
        private BuildHotbarHintsUI buildHotbar;
        private DayNightCycle dayNightCycle;
        private Step currentStep;
        private Vector2 movementStart;
        private float stepEndsAt;
        private readonly HashSet<BuildingInstance> tutorialBuildings = new();

        private void Awake()
        {
            playerMovement = FindFirstObjectByType<PlayerMovement>();
            playerCombat = FindFirstObjectByType<PlayerCombat>();
            wallet = FindFirstObjectByType<PlayerResourceWallet>();
            buildingSystem = FindFirstObjectByType<BuildingSystem>();
            buildHotbar = FindFirstObjectByType<BuildHotbarHintsUI>();
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();

            if (HasCompletedTutorial())
            {
                dayNightCycle?.SetFirstNightTutorialGate(false);
                enabled = false;
                return;
            }

            if (tutorialPanel == null || messageLabel == null)
            {
                Debug.LogError("TutorialManager needs TutorialPanel and its message text assigned in the scene.", this);
                dayNightCycle?.SetFirstNightTutorialGate(false);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (playerCombat != null)
            {
                playerCombat.AttackPerformed += HandleAttackPerformed;
                playerCombat.SuccessfulHit += HandleSuccessfulHit;
            }

            if (wallet != null)
            {
                wallet.ResourceChanged += HandleResourceChanged;
            }

            if (buildingSystem != null)
            {
                buildingSystem.SelectionChanged += HandleSelectionChanged;
                buildingSystem.BuildingPlaced += HandleBuildingPlaced;
            }
        }

        private void Start() => ShowStep(Step.Move);

        private void OnDisable()
        {
            if (playerCombat != null)
            {
                playerCombat.AttackPerformed -= HandleAttackPerformed;
                playerCombat.SuccessfulHit -= HandleSuccessfulHit;
            }

            if (wallet != null)
            {
                wallet.ResourceChanged -= HandleResourceChanged;
            }

            if (buildingSystem != null)
            {
                buildingSystem.SelectionChanged -= HandleSelectionChanged;
                buildingSystem.BuildingPlaced -= HandleBuildingPlaced;
            }

            foreach (BuildingInstance building in tutorialBuildings)
            {
                if (building != null)
                {
                    building.Removing -= HandleBuildingRemoving;
                }
            }

            tutorialBuildings.Clear();
        }

        private void Update()
        {
            if (currentStep == Step.Move && playerMovement != null)
            {
                Vector2 position = playerMovement.transform.position;
                if ((position - movementStart).sqrMagnitude >= movementDistance * movementDistance)
                {
                    Advance();
                }
            }

            if (currentStep == Step.CampfireGoal && Time.time >= stepEndsAt)
            {
                Advance();
            }
        }

        private void HandleAttackPerformed()
        {
            if (currentStep == Step.Attack)
            {
                Advance();
            }
        }

        private void HandleSuccessfulHit(PlayerMeleeHitEvent hit)
        {
            ResourceNode resource = hit.Target != null ? hit.Target.GetComponentInParent<ResourceNode>() : null;
            if (currentStep == Step.HealAtBush && resource != null && resource.NodeType == ResourceNodeType.Bush)
            {
                Advance();
                return;
            }

            if (currentStep == Step.FightDinosaur && hit.Target != null && hit.Target.GetComponentInParent<DinosaurAgent>() != null)
            {
                Advance();
            }
        }

        private void HandleResourceChanged(ResourceWalletChangedEvent change)
        {
            if (change.Delta <= 0)
            {
                return;
            }

            if (currentStep == Step.GatherWood && change.ResourceType == PlayerResourceType.Wood ||
                currentStep == Step.GatherStone && change.ResourceType == PlayerResourceType.Stone)
            {
                Advance();
            }
        }

        private void HandleSelectionChanged(BuildSelection selection)
        {
            if (currentStep == Step.SelectWall && selection == BuildSelection.Wall ||
                currentStep == Step.SelectDoor && selection == BuildSelection.Door)
            {
                Advance();
            }
        }

        private void HandleBuildingPlaced(BuildingInstance building)
        {
            if (building == null || building.Definition == null)
            {
                return;
            }

            if (tutorialBuildings.Add(building))
            {
                building.Removing += HandleBuildingRemoving;
            }

            if (currentStep == Step.PlaceWall && building.Definition.BuildingType == BuildingType.Wall ||
                currentStep == Step.PlaceDoor && building.Definition.BuildingType == BuildingType.Door)
            {
                Advance();
            }
        }

        private void HandleBuildingRemoving(BuildingRemovalEvent removal)
        {
            if (currentStep == Step.SellBuilding && removal.Reason == BuildingRemovalReason.PlayerRemoval)
            {
                Advance();
            }
        }

        private void Advance()
        {
            ShowStep(currentStep + 1);
        }

        private void ShowStep(Step step)
        {
            currentStep = step;
            buildHotbar?.SetTutorialHighlight(BuildSelection.None);

            switch (step)
            {
                case Step.Move:
                    movementStart = playerMovement != null ? playerMovement.transform.position : Vector2.zero;
                    SetMessage("Move: Use WASD keys to move.");
                    break;
                case Step.Attack:
                    SetMessage("Attack: Press E to attack.");
                    break;
                case Step.HealAtBush:
                    SetMessage("Bushes heal you. Hit a bush to restore health.");
                    break;
                case Step.GatherWood:
                    SetMessage("Trees provide wood. Hit a tree.");
                    break;
                case Step.GatherStone:
                    SetMessage("Stones provide stone. Hit a stone.");
                    break;
                case Step.CampfireGoal:
                    SetMessage("Protect the campfire: dinosaurs are coming straight for it.");
                    stepEndsAt = Time.time + campfireMessageDuration;
                    break;
                case Step.SelectWall:
                    buildHotbar?.SetTutorialHighlight(BuildSelection.Wall);
                    SetMessage("Select wall with key 1.");
                    break;
                case Step.PlaceWall:
                    SetMessage("Place wall with left mouse button. Right-click cancels construction.");
                    break;
                case Step.SelectDoor:
                    buildHotbar?.SetTutorialHighlight(BuildSelection.Door);
                    SetMessage("Select door with key 2.");
                    break;
                case Step.PlaceDoor:
                    SetMessage("Place door with left mouse button.");
                    break;
                case Step.SellBuilding:
                    buildingSystem?.CancelBuildMode();
                    SetMessage("Click a placed wall or door, then press Remove to sell it and get some wood back.");
                    break;
                case Step.FightDinosaur:
                    dayNightCycle?.StartFirstNightFromTutorial();
                    SetMessage("At night, dinosaurs come after the campfire. Attack the dinosaur and prevent it from destroying the campfire.");
                    break;
                case Step.Complete:
                    buildHotbar?.SetTutorialHighlight(BuildSelection.None);
                    PlayerPrefs.SetInt(CompletedPreferenceKey, 1);
                    PlayerPrefs.Save();
                    if (tutorialPanel != null)
                    {
                        tutorialPanel.SetActive(false);
                    }
                    break;
            }
        }

        private static bool HasCompletedTutorial() => PlayerPrefs.GetInt(CompletedPreferenceKey, 0) == 1;

        private void SetMessage(string message)
        {
            if (tutorialPanel != null && !tutorialPanel.activeSelf)
            {
                tutorialPanel.SetActive(true);
            }

            if (messageLabel != null)
            {
                messageLabel.text = message;
            }
        }

    }
}
