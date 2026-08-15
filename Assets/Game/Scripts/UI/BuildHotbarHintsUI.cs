using UnityEngine;
using UnityEngine.UI;

namespace MicroJam.Game
{
    public sealed class BuildHotbarHintsUI : MonoBehaviour
    {
        [SerializeField] private BuildingSystem buildingSystem;
        [SerializeField] private Image wallSlot;
        [SerializeField] private Image doorSlot;
        [SerializeField] private Image turretSlot;
        [SerializeField] private Color selectedColor = new(1f, 0.72f, 0.2f, 1f);
        [SerializeField, Min(1f)] private float selectedScale = 1.1f;

        private static readonly Color WallColor = new(0.42f, 0.22f, 0.08f, 1f);
        private static readonly Color DoorColor = new(0.24f, 0.43f, 0.55f, 1f);
        private static readonly Color TurretColor = new(0.28f, 0.3f, 0.36f, 1f);
        private BuildSelection tutorialHighlight;

        private void Awake()
        {
            buildingSystem ??= FindFirstObjectByType<BuildingSystem>();
        }

        private void OnEnable()
        {
            if (buildingSystem != null)
            {
                buildingSystem.SelectionChanged += Refresh;
            }

            Refresh(buildingSystem != null ? buildingSystem.Selection : BuildSelection.None);
        }

        private void OnDisable()
        {
            if (buildingSystem != null)
            {
                buildingSystem.SelectionChanged -= Refresh;
            }
        }

        private void Refresh(BuildSelection selection)
        {
            BuildSelection displayedSelection = tutorialHighlight != BuildSelection.None ? tutorialHighlight : selection;
            SetSlot(wallSlot, displayedSelection == BuildSelection.Wall, WallColor);
            SetSlot(doorSlot, displayedSelection == BuildSelection.Door, DoorColor);
            SetSlot(turretSlot, false, TurretColor);
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
            slot.rectTransform.localScale = selected ? Vector3.one * selectedScale : Vector3.one;
        }

        private void OnValidate() => selectedScale = Mathf.Max(1f, selectedScale);
    }
}
