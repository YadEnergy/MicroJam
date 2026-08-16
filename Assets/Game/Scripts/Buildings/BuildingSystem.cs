using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MicroJam.Game
{
    public enum BuildSelection
    {
        None,
        Wall,
        Door,
        BowTower,
        StoneTower
    }

    public enum BuildPlacementStatus
    {
        None,
        OutsideViewport,
        PointerOverUi,
        OutsideBuildZone,
        ProtectedCampfire,
        Occupied,
        DynamicOverlap,
        InsufficientWood,
        InsufficientResources,
        Valid
    }

    public sealed class BuildingSystem : MonoBehaviour
    {
        [Header("Existing Input System Asset")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Building";

        [Header("Scene References")]
        [SerializeField] private SquareGameplayViewport gameplayViewport;
        [SerializeField] private WorldGridService worldGrid;
        [SerializeField] private GridOccupancyService occupancy;
        [SerializeField] private PlayerResourceWallet playerWallet;
        [SerializeField] private BuildPlacementPreview placementPreview;
        [SerializeField] private Transform runtimeBuildingParent;

        [Header("Buildable Definitions")]
        [SerializeField] private BuildingDefinition wallDefinition;
        [SerializeField] private BuildingDefinition doorDefinition;
        [SerializeField] private BuildingDefinition bowTowerDefinition;
        [SerializeField] private BuildingDefinition stoneTowerDefinition;

        [Header("Dynamic Occupancy")]
        [SerializeField] private LayerMask dynamicOccupantLayers;

        private InputAction selectWallAction;
        private InputAction selectDoorAction;
        private InputAction selectBowTowerAction;
        private InputAction selectStoneTowerAction;
        private InputAction cancelAction;
        private InputAction placeAction;
        private InputAction pointAction;
        private bool hasTargetCell;
        private bool pointerBlockedByUi;

        public BuildSelection Selection { get; private set; }
        public BuildPlacementStatus PlacementStatus { get; private set; }
        public Vector2Int TargetCell { get; private set; }
        public BuildingDefinition SelectedDefinition => GetDefinition(Selection);
        public SquareGameplayViewport GameplayViewport => gameplayViewport;
        public WorldGridService WorldGrid => worldGrid;
        public GridOccupancyService Occupancy => occupancy;
        public PlayerResourceWallet PlayerWallet => playerWallet;
        public BuildPlacementPreview PlacementPreview => placementPreview;
        public Transform RuntimeBuildingParent => runtimeBuildingParent;
        public BuildingDefinition WallDefinition => wallDefinition;
        public BuildingDefinition DoorDefinition => doorDefinition;
        public BuildingDefinition BowTowerDefinition => bowTowerDefinition;
        public BuildingDefinition StoneTowerDefinition => stoneTowerDefinition;
        public LayerMask DynamicOccupantLayers => dynamicOccupantLayers;
        public InputActionAsset InputActions => inputActions;
        public bool HasTargetCell => hasTargetCell;
        public bool HasValidInputActions
        {
            get
            {
                InputActionMap map = inputActions != null ? inputActions.FindActionMap(actionMapName, false) : null;
                return map?.FindAction("SelectWall", false) != null &&
                       map.FindAction("SelectDoor", false) != null &&
                       map.FindAction("SelectBowTower", false) != null &&
                       map.FindAction("SelectStoneTower", false) != null &&
                       map.FindAction("Cancel", false) != null &&
                       map.FindAction("Place", false) != null &&
                       map.FindAction("Point", false) != null;
            }
        }

        public event Action<BuildSelection> SelectionChanged;
        public event Action<BuildingInstance> BuildingPlaced;

        public void Configure(
            InputActionAsset configuredInput,
            SquareGameplayViewport configuredViewport,
            WorldGridService configuredGrid,
            GridOccupancyService configuredOccupancy,
            PlayerResourceWallet configuredWallet,
            BuildPlacementPreview configuredPreview,
            Transform configuredRuntimeParent,
            BuildingDefinition configuredWall,
            BuildingDefinition configuredDoor,
            LayerMask configuredDynamicOccupants)
        {
            Configure(configuredInput, configuredViewport, configuredGrid, configuredOccupancy, configuredWallet,
                configuredPreview, configuredRuntimeParent, configuredWall, configuredDoor, null, null, configuredDynamicOccupants);
        }

        public void Configure(
            InputActionAsset configuredInput,
            SquareGameplayViewport configuredViewport,
            WorldGridService configuredGrid,
            GridOccupancyService configuredOccupancy,
            PlayerResourceWallet configuredWallet,
            BuildPlacementPreview configuredPreview,
            Transform configuredRuntimeParent,
            BuildingDefinition configuredWall,
            BuildingDefinition configuredDoor,
            BuildingDefinition configuredBowTower,
            BuildingDefinition configuredStoneTower,
            LayerMask configuredDynamicOccupants)
        {
            inputActions = configuredInput;
            gameplayViewport = configuredViewport;
            worldGrid = configuredGrid;
            occupancy = configuredOccupancy;
            playerWallet = configuredWallet;
            placementPreview = configuredPreview;
            runtimeBuildingParent = configuredRuntimeParent;
            wallDefinition = configuredWall;
            doorDefinition = configuredDoor;
            bowTowerDefinition = configuredBowTower;
            stoneTowerDefinition = configuredStoneTower;
            dynamicOccupantLayers = configuredDynamicOccupants;
            ResolveActions();
            CancelBuildMode();
        }

        public void SelectBuildMode(BuildSelection selection)
        {
            if (selection == BuildSelection.None)
            {
                CancelBuildMode();
                return;
            }

            Selection = selection;
            hasTargetCell = false;
            PlacementStatus = BuildPlacementStatus.None;
            placementPreview?.Hide();
            SelectionChanged?.Invoke(Selection);
        }

        public void CancelBuildMode()
        {
            bool changed = Selection != BuildSelection.None;
            Selection = BuildSelection.None;
            hasTargetCell = false;
            pointerBlockedByUi = false;
            PlacementStatus = BuildPlacementStatus.None;
            placementPreview?.Hide();
            if (changed)
            {
                SelectionChanged?.Invoke(Selection);
            }
        }

        public BuildPlacementStatus UpdateTargetFromScreen(Vector2 screenPosition, bool isPointerOverUi = false)
        {
            BuildingDefinition definition = SelectedDefinition;
            pointerBlockedByUi = isPointerOverUi;
            if (definition == null)
            {
                hasTargetCell = false;
                PlacementStatus = BuildPlacementStatus.None;
                placementPreview?.Hide();
                return PlacementStatus;
            }

            if (gameplayViewport == null || !gameplayViewport.TryScreenToWorld(screenPosition, out Vector2 worldPosition))
            {
                hasTargetCell = false;
                PlacementStatus = BuildPlacementStatus.OutsideViewport;
                placementPreview?.Hide();
                return PlacementStatus;
            }

            TargetCell = worldGrid.WorldToCell(worldPosition);
            hasTargetCell = true;
            PlacementStatus = isPointerOverUi ? BuildPlacementStatus.PointerOverUi : EvaluatePlacement(definition, TargetCell);
            ShowPreview(definition, TargetCell, PlacementStatus == BuildPlacementStatus.Valid);
            return PlacementStatus;
        }

        public BuildPlacementStatus EvaluatePlacement(BuildingDefinition definition, Vector2Int anchorCell)
        {
            if (definition == null || worldGrid == null || worldGrid.Config == null || occupancy == null)
            {
                return BuildPlacementStatus.None;
            }

            Vector2Int[] cells = GetFootprintCells(anchorCell, definition.FootprintSize);
            foreach (Vector2Int cell in cells)
            {
                if (!worldGrid.Config.IsCellInsideBuildZone(cell))
                {
                    return BuildPlacementStatus.OutsideBuildZone;
                }

                if (worldGrid.Config.IsCellProtectedFromBuilding(cell))
                {
                    return BuildPlacementStatus.ProtectedCampfire;
                }

                if (occupancy.IsCellOccupied(cell))
                {
                    return BuildPlacementStatus.Occupied;
                }
            }

            GetFootprintWorldBounds(anchorCell, definition.FootprintSize, out Vector2 center, out Vector2 size);
            if (Physics2D.OverlapBox(center, size, 0f, dynamicOccupantLayers) != null)
            {
                return BuildPlacementStatus.DynamicOverlap;
            }

            if (playerWallet == null || !playerWallet.CanAfford(definition.WoodCost, definition.StoneCost))
            {
                return definition.StoneCost > 0
                    ? BuildPlacementStatus.InsufficientResources
                    : BuildPlacementStatus.InsufficientWood;
            }

            return BuildPlacementStatus.Valid;
        }

        public bool TryPlaceTargeted(out BuildingInstance placed)
        {
            placed = null;
            if (!hasTargetCell || pointerBlockedByUi || SelectedDefinition == null)
            {
                return false;
            }

            return TryPlaceAtCell(SelectedDefinition, TargetCell, out placed);
        }

        public bool TryPlaceAtCell(BuildingDefinition definition, Vector2Int anchorCell, out BuildingInstance placed)
        {
            placed = null;
            PlacementStatus = EvaluatePlacement(definition, anchorCell);
            if (PlacementStatus != BuildPlacementStatus.Valid || definition.Prefab == null || runtimeBuildingParent == null)
            {
                RefreshPreviewAfterAttempt(definition, anchorCell);
                return false;
            }

            Vector2Int[] cells = GetFootprintCells(anchorCell, definition.FootprintSize);
            GetFootprintWorldBounds(anchorCell, definition.FootprintSize, out Vector2 center, out _);
            if (!playerWallet.TrySpend(definition.WoodCost, definition.StoneCost))
            {
                PlacementStatus = definition.StoneCost > 0
                    ? BuildPlacementStatus.InsufficientResources
                    : BuildPlacementStatus.InsufficientWood;
                RefreshPreviewAfterAttempt(definition, anchorCell);
                return false;
            }

            GameObject instanceObject = Instantiate(definition.Prefab, center, Quaternion.identity, runtimeBuildingParent);
            placed = instanceObject.GetComponent<BuildingInstance>();
            if (placed == null || !placed.InitializePlacement(definition, occupancy, cells))
            {
                if (placed != null)
                {
                    placed.ReleaseOccupancy();
                }

                Destroy(instanceObject);
                playerWallet.TryAdd(definition.WoodCost, definition.StoneCost);
                placed = null;
                PlacementStatus = BuildPlacementStatus.Occupied;
                RefreshPreviewAfterAttempt(definition, anchorCell);
                return false;
            }

            instanceObject.name = $"{definition.DisplayName} [{anchorCell.x}, {anchorCell.y}]";
            BuildingPlaced?.Invoke(placed);
            PlacementStatus = BuildPlacementStatus.Occupied;
            ShowPreview(definition, anchorCell, false);
            return true;
        }

        public static Vector2Int[] GetFootprintCells(Vector2Int anchorCell, Vector2Int footprintSize)
        {
            int width = Mathf.Max(1, footprintSize.x);
            int height = Mathf.Max(1, footprintSize.y);
            Vector2Int[] cells = new Vector2Int[width * height];
            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    cells[index++] = anchorCell + new Vector2Int(x, y);
                }
            }

            return cells;
        }

        private void Awake()
        {
            ResolveActions();
            CancelBuildMode();
        }

        private void OnEnable()
        {
            ResolveActions();
            selectWallAction?.Enable();
            selectDoorAction?.Enable();
            selectBowTowerAction?.Enable();
            selectStoneTowerAction?.Enable();
            cancelAction?.Enable();
            placeAction?.Enable();
            pointAction?.Enable();
        }

        private void OnDisable()
        {
            selectWallAction?.Disable();
            selectDoorAction?.Disable();
            selectBowTowerAction?.Disable();
            selectStoneTowerAction?.Disable();
            cancelAction?.Disable();
            placeAction?.Disable();
            pointAction?.Disable();
            CancelBuildMode();
        }

        private void Update()
        {
            if (selectWallAction != null && selectWallAction.WasPressedThisFrame())
            {
                SelectBuildMode(BuildSelection.Wall);
            }

            if (selectDoorAction != null && selectDoorAction.WasPressedThisFrame())
            {
                SelectBuildMode(BuildSelection.Door);
            }

            if (selectBowTowerAction != null && selectBowTowerAction.WasPressedThisFrame())
            {
                SelectBuildMode(BuildSelection.BowTower);
            }

            if (selectStoneTowerAction != null && selectStoneTowerAction.WasPressedThisFrame())
            {
                SelectBuildMode(BuildSelection.StoneTower);
            }

            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                CancelBuildMode();
                return;
            }

            if (Selection == BuildSelection.None || pointAction == null)
            {
                placementPreview?.Hide();
                return;
            }

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            UpdateTargetFromScreen(pointAction.ReadValue<Vector2>(), overUi);
            if (placeAction != null && placeAction.WasPressedThisFrame())
            {
                TryPlaceTargeted(out _);
            }
        }

        private void ResolveActions()
        {
            selectWallAction = null;
            selectDoorAction = null;
            selectBowTowerAction = null;
            selectStoneTowerAction = null;
            cancelAction = null;
            placeAction = null;
            pointAction = null;
            InputActionMap map = inputActions != null ? inputActions.FindActionMap(actionMapName, false) : null;
            if (map == null)
            {
                return;
            }

            selectWallAction = map.FindAction("SelectWall", false);
            selectDoorAction = map.FindAction("SelectDoor", false);
            selectBowTowerAction = map.FindAction("SelectBowTower", false);
            selectStoneTowerAction = map.FindAction("SelectStoneTower", false);
            cancelAction = map.FindAction("Cancel", false);
            placeAction = map.FindAction("Place", false);
            pointAction = map.FindAction("Point", false);
        }

        private BuildingDefinition GetDefinition(BuildSelection selection)
        {
            return selection switch
            {
                BuildSelection.Wall => wallDefinition,
                BuildSelection.Door => doorDefinition,
                BuildSelection.BowTower => bowTowerDefinition,
                BuildSelection.StoneTower => stoneTowerDefinition,
                _ => null
            };
        }

        private void ShowPreview(BuildingDefinition definition, Vector2Int anchorCell, bool valid)
        {
            if (placementPreview == null)
            {
                return;
            }

            GetFootprintWorldBounds(anchorCell, definition.FootprintSize, out Vector2 center, out Vector2 size);
            placementPreview.Show(definition, center, size, valid);
        }

        private void RefreshPreviewAfterAttempt(BuildingDefinition definition, Vector2Int anchorCell)
        {
            if (hasTargetCell && definition == SelectedDefinition)
            {
                ShowPreview(definition, anchorCell, PlacementStatus == BuildPlacementStatus.Valid);
            }
        }

        private void GetFootprintWorldBounds(Vector2Int anchorCell, Vector2Int footprintSize, out Vector2 center, out Vector2 size)
        {
            float tileSize = worldGrid.Config.TileSize;
            Vector2 firstCenter = worldGrid.CellToWorldCenter(anchorCell);
            size = new Vector2(Mathf.Max(1, footprintSize.x) * tileSize, Mathf.Max(1, footprintSize.y) * tileSize);
            center = firstCenter + new Vector2((size.x - tileSize) * 0.5f, (size.y - tileSize) * 0.5f);
        }
    }
}
