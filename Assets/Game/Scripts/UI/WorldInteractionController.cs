using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MicroJam.Game
{
    public enum WorldInteractionPopupType
    {
        None,
        Building,
        Campfire
    }

    public sealed class WorldInteractionController : MonoBehaviour
    {
        public event Action<CampfireInteraction> CampfireOpened;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "WorldInteraction";
        [SerializeField] private BuildingSystem buildingSystem;
        [SerializeField] private SquareGameplayViewport gameplayViewport;
        [SerializeField] private PlayerResourceWallet playerWallet;
        [SerializeField] private BuildingInteractionPopup buildingPopup;
        [SerializeField] private CampfireInteractionPopup campfirePopup;
        [SerializeField] private LayerMask interactableLayers;

        private InputAction interactAction;
        private InputAction pointAction;
        private InputAction cancelAction;
        private bool suppressInteractionUntilRelease;

        public InputActionAsset InputActions => inputActions;
        public BuildingSystem BuildingSystem => buildingSystem;
        public SquareGameplayViewport GameplayViewport => gameplayViewport;
        public PlayerResourceWallet PlayerWallet => playerWallet;
        public BuildingInteractionPopup BuildingPopup => buildingPopup;
        public CampfireInteractionPopup CampfirePopup => campfirePopup;
        public LayerMask InteractableLayers => interactableLayers;
        public WorldInteractionPopupType OpenPopupType => buildingPopup != null && buildingPopup.IsOpen
            ? WorldInteractionPopupType.Building
            : campfirePopup != null && campfirePopup.IsOpen
                ? WorldInteractionPopupType.Campfire
                : WorldInteractionPopupType.None;
        public bool HasOpenPopup => OpenPopupType != WorldInteractionPopupType.None;
        public bool HasValidInputActions
        {
            get
            {
                InputActionMap map = inputActions != null ? inputActions.FindActionMap(actionMapName, false) : null;
                return map?.FindAction("Interact", false) != null && map.FindAction("Point", false) != null &&
                       map.FindAction("Cancel", false) != null;
            }
        }

        public void Configure(
            InputActionAsset configuredInput,
            BuildingSystem configuredBuildingSystem,
            SquareGameplayViewport configuredViewport,
            PlayerResourceWallet configuredWallet,
            BuildingInteractionPopup configuredBuildingPopup,
            CampfireInteractionPopup configuredCampfirePopup,
            LayerMask configuredInteractableLayers)
        {
            inputActions = configuredInput;
            buildingSystem = configuredBuildingSystem;
            gameplayViewport = configuredViewport;
            playerWallet = configuredWallet;
            buildingPopup = configuredBuildingPopup;
            campfirePopup = configuredCampfirePopup;
            interactableLayers = configuredInteractableLayers;
            ResolveActions();
            CloseAll();
        }

        public bool TryInteractAtScreen(Vector2 screenPosition, bool pointerOverUi = false)
        {
            if (pointerOverUi || buildingSystem == null || buildingSystem.Selection != BuildSelection.None ||
                gameplayViewport == null || !gameplayViewport.TryScreenToWorld(screenPosition, out Vector2 worldPosition))
            {
                return false;
            }

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition, interactableLayers);
            foreach (Collider2D hit in hits)
            {
                BuildingInstance building = hit.GetComponentInParent<BuildingInstance>();
                if (building != null && !building.RemovalStarted)
                {
                    OpenBuilding(building);
                    return true;
                }
            }

            foreach (Collider2D hit in hits)
            {
                CampfireInteraction campfire = hit.GetComponentInParent<CampfireInteraction>();
                if (campfire != null)
                {
                    OpenCampfire(campfire);
                    return true;
                }
            }

            CloseAll();
            return false;
        }

        public void OpenBuilding(BuildingInstance building)
        {
            campfirePopup?.Close();
            if (building != null)
            {
                buildingPopup?.Open(building, playerWallet);
            }
        }

        public void OpenCampfire(CampfireInteraction campfire)
        {
            buildingPopup?.Close();
            if (campfire != null)
            {
                campfirePopup?.Open(campfire, playerWallet);
                CampfireOpened?.Invoke(campfire);
            }
        }

        public void CloseAll()
        {
            buildingPopup?.Close();
            campfirePopup?.Close();
        }

        private void Awake()
        {
            ResolveActions();
            CloseAll();
        }

        private void OnEnable()
        {
            ResolveActions();
            interactAction?.Enable();
            pointAction?.Enable();
            cancelAction?.Enable();
            if (buildingSystem != null)
            {
                buildingSystem.SelectionChanged += HandleBuildSelectionChanged;
                buildingSystem.BuildingPlaced += HandleBuildingPlaced;
            }
        }

        private void OnDisable()
        {
            if (buildingSystem != null)
            {
                buildingSystem.SelectionChanged -= HandleBuildSelectionChanged;
                buildingSystem.BuildingPlaced -= HandleBuildingPlaced;
            }

            interactAction?.Disable();
            pointAction?.Disable();
            cancelAction?.Disable();
            CloseAll();
        }

        private void Update()
        {
            if (GameplayInputGate.IsBlocked) return;

            if (suppressInteractionUntilRelease)
            {
                if (interactAction == null || !interactAction.IsPressed())
                {
                    suppressInteractionUntilRelease = false;
                }

                return;
            }

            if (buildingSystem != null && buildingSystem.Selection != BuildSelection.None)
            {
                return;
            }

            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                CloseAll();
                return;
            }

            if (interactAction != null && interactAction.WasPressedThisFrame() && pointAction != null)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                TryInteractAtScreen(pointAction.ReadValue<Vector2>(), overUi);
            }
        }

        private void ResolveActions()
        {
            interactAction = null;
            pointAction = null;
            cancelAction = null;
            InputActionMap map = inputActions != null ? inputActions.FindActionMap(actionMapName, false) : null;
            if (map == null)
            {
                return;
            }

            interactAction = map.FindAction("Interact", false);
            pointAction = map.FindAction("Point", false);
            cancelAction = map.FindAction("Cancel", false);
        }

        private void HandleBuildSelectionChanged(BuildSelection selection)
        {
            if (selection != BuildSelection.None)
            {
                CloseAll();
            }
        }

        private void HandleBuildingPlaced(BuildingInstance _)
        {
            // A left click is shared by construction and interaction. Ignore that same press,
            // so a just-built object never immediately opens its sell popup.
            suppressInteractionUntilRelease = true;
            CloseAll();
        }
    }
}
