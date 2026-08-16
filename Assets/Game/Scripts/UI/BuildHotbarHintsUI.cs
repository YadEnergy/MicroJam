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

        private static readonly Color WallColor = new(0.42f, 0.22f, 0.08f, 1f);
        private static readonly Color DoorColor = new(0.24f, 0.43f, 0.55f, 1f);
        private static readonly Color BowTowerColor = new(0.28f, 0.42f, 0.25f, 1f);
        private static readonly Color StoneTowerColor = new(0.38f, 0.4f, 0.46f, 1f);
        private BuildSelection tutorialHighlight;
        private Button wallButton;
        private Button doorButton;
        private Button bowTowerButton;
        private Button stoneTowerButton;

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
            SetSlot(wallSlot, displayedSelection == BuildSelection.Wall, WallColor);
            SetSlot(doorSlot, displayedSelection == BuildSelection.Door, DoorColor);
            SetSlot(bowTowerSlot, displayedSelection == BuildSelection.BowTower, BowTowerColor);
            SetSlot(stoneTowerSlot, displayedSelection == BuildSelection.StoneTower, StoneTowerColor);
        }

        /// <summary>Draws attention to a build slot while the tutorial is teaching it.</summary>
        public void SetTutorialHighlight(BuildSelection selection)
        {
            tutorialHighlight = selection;
            Refresh(buildingSystem != null ? buildingSystem.Selection : BuildSelection.None);
        }

        private void SetSlot(Image slot, bool selected, Color normalColor)
        {
            if (slot == null) return;

            slot.color = selected ? selectedColor : normalColor;
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
