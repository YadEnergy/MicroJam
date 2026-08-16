using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MicroJam.Game
{
    public sealed class BuildHotbarHintsUI : MonoBehaviour
    {
        [SerializeField] private BuildingSystem buildingSystem;
        [SerializeField] private Image wallSlot;
        [SerializeField] private Image doorSlot;
        [FormerlySerializedAs("turretSlot"), SerializeField] private Image bowTowerSlot;
        [SerializeField] private Image stoneTowerSlot;
        [SerializeField] private Color selectedColor = new(1f, 0.72f, 0.2f, 1f);
        [SerializeField, Min(1f)] private float selectedScale = 1.1f;

        private BuildSelection tutorialHighlight;
        private Button wallButton;
        private Button doorButton;
        private Button bowTowerButton;
        private Button stoneTowerButton;
        private Text wallCostLabel;
        private Text doorCostLabel;
        private Text bowTowerCostLabel;
        private Text stoneTowerCostLabel;

        public Image WallSlot => wallSlot;
        public Image DoorSlot => doorSlot;
        public Image BowTowerSlot => bowTowerSlot;
        public Image StoneTowerSlot => stoneTowerSlot;

        public void Configure(BuildingSystem system, Image wall, Image door, Image bowTower, Image stoneTower)
        {
            buildingSystem = system;
            wallSlot = wall;
            doorSlot = door;
            bowTowerSlot = bowTower;
            stoneTowerSlot = stoneTower;
            CacheButtons();
        }

        private void Awake()
        {
            buildingSystem ??= FindFirstObjectByType<BuildingSystem>();
            CacheButtons();
            FindCostLabels();
            RefreshCosts();
        }

        private void OnEnable()
        {
            if (buildingSystem != null)
            {
                buildingSystem.SelectionChanged += Refresh;
            }

            AddButtonListeners();

            Refresh(buildingSystem != null ? buildingSystem.Selection : BuildSelection.None);
        }

        private void OnDisable()
        {
            if (buildingSystem != null)
            {
                buildingSystem.SelectionChanged -= Refresh;
            }

            RemoveButtonListeners();
        }

        private void Refresh(BuildSelection selection)
        {
            BuildSelection displayedSelection = tutorialHighlight != BuildSelection.None ? tutorialHighlight : selection;
            SetSlot(wallSlot, displayedSelection == BuildSelection.Wall);
            SetSlot(doorSlot, displayedSelection == BuildSelection.Door);
            SetSlot(bowTowerSlot, displayedSelection == BuildSelection.BowTower);
            SetSlot(stoneTowerSlot, displayedSelection == BuildSelection.StoneTower);
        }

        /// <summary>Draws attention to a build slot while the tutorial is teaching it.</summary>
        public void SetTutorialHighlight(BuildSelection selection)
        {
            tutorialHighlight = selection;
            Refresh(buildingSystem != null ? buildingSystem.Selection : BuildSelection.None);
        }

        private void SetSlot(Image slot, bool selected)
        {
            if (slot == null) return;

            slot.color = Color.white;
            UIButtonTween tween = slot.GetComponent<UIButtonTween>();
            if (tween != null) tween.SetSelected(selected);
            else slot.rectTransform.localScale = selected ? Vector3.one * selectedScale : Vector3.one;
        }

        private void CacheButtons()
        {
            wallButton = wallSlot != null ? wallSlot.GetComponent<Button>() : null;
            doorButton = doorSlot != null ? doorSlot.GetComponent<Button>() : null;
            bowTowerButton = bowTowerSlot != null ? bowTowerSlot.GetComponent<Button>() : null;
            stoneTowerButton = stoneTowerSlot != null ? stoneTowerSlot.GetComponent<Button>() : null;
        }

        private void FindCostLabels()
        {
            wallCostLabel = FindCostLabel(wallSlot);
            doorCostLabel = FindCostLabel(doorSlot);
            bowTowerCostLabel = FindCostLabel(bowTowerSlot);
            stoneTowerCostLabel = FindCostLabel(stoneTowerSlot);
        }

        private void RefreshCosts()
        {
            if (buildingSystem == null) return;

            SetCost(wallCostLabel, buildingSystem.WallDefinition);
            SetCost(doorCostLabel, buildingSystem.DoorDefinition);
            SetCost(bowTowerCostLabel, buildingSystem.BowTowerDefinition);
            SetCost(stoneTowerCostLabel, buildingSystem.StoneTowerDefinition);
        }

        private static Text FindCostLabel(Image slot)
        {
            if (slot == null) return null;
            return slot.transform.Find("CostLabel")?.GetComponent<Text>();
        }

        private static void SetCost(Text label, BuildingDefinition definition)
        {
            if (label == null) return;
            if (definition == null)
            {
                label.text = "--";
                return;
            }

            if (definition.WoodCost > 0 && definition.StoneCost > 0)
            {
                label.text = $"{definition.WoodCost}W {definition.StoneCost}S";
            }
            else if (definition.StoneCost > 0)
            {
                label.text = $"{definition.StoneCost}S";
            }
            else
            {
                label.text = $"{definition.WoodCost}W";
            }
        }

        private void AddButtonListeners()
        {
            wallButton?.onClick.AddListener(SelectWall);
            doorButton?.onClick.AddListener(SelectDoor);
            bowTowerButton?.onClick.AddListener(SelectBowTower);
            stoneTowerButton?.onClick.AddListener(SelectStoneTower);
        }

        private void RemoveButtonListeners()
        {
            wallButton?.onClick.RemoveListener(SelectWall);
            doorButton?.onClick.RemoveListener(SelectDoor);
            bowTowerButton?.onClick.RemoveListener(SelectBowTower);
            stoneTowerButton?.onClick.RemoveListener(SelectStoneTower);
        }

        private void SelectWall() => Select(BuildSelection.Wall);
        private void SelectDoor() => Select(BuildSelection.Door);
        private void SelectBowTower() => Select(BuildSelection.BowTower);
        private void SelectStoneTower() => Select(BuildSelection.StoneTower);

        private void Select(BuildSelection selection)
        {
            if (!GameplayInputGate.IsBlocked) buildingSystem?.SelectBuildMode(selection);
        }

        private void OnValidate() => selectedScale = Mathf.Max(1f, selectedScale);
    }
}
