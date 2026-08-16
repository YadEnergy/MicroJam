using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroJam.Game
{
    public sealed class PlayerInputController : MonoBehaviour
    {
        [Header("Existing Input System Asset")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string attackActionName = "Attack";

        [Header("Player Components")]
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerFacing facing;
        [SerializeField] private PlayerCombat combat;

        private InputAction moveAction;
        private InputAction attackAction;

        public InputActionAsset InputActions => inputActions;
        public PlayerMovement Movement => movement;
        public PlayerFacing Facing => facing;
        public PlayerCombat Combat => combat;
        public bool HasValidActions
        {
            get
            {
                if (inputActions == null)
                {
                    return false;
                }

                InputActionMap map = inputActions.FindActionMap(actionMapName, false);
                return map != null && map.FindAction(moveActionName, false) != null && map.FindAction(attackActionName, false) != null;
            }
        }

        public void Configure(
            InputActionAsset configuredActions,
            PlayerMovement configuredMovement,
            PlayerFacing configuredFacing,
            PlayerCombat configuredCombat)
        {
            inputActions = configuredActions;
            movement = configuredMovement;
            facing = configuredFacing;
            combat = configuredCombat;
            ResolveActions();
        }

        private void OnEnable()
        {
            ResolveActions();
            moveAction?.Enable();
            attackAction?.Enable();
        }

        private void OnDisable()
        {
            movement?.SetMoveInput(Vector2.zero);
            combat?.SetAttackHeld(false);
            moveAction?.Disable();
            attackAction?.Disable();
        }

        private void Update()
        {
            if (GameplayInputGate.IsBlocked)
            {
                movement?.SetMoveInput(Vector2.zero);
                combat?.SetAttackHeld(false);
                return;
            }

            movement?.SetMoveInput(moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero);
            combat?.SetAttackHeld(attackAction != null && attackAction.IsPressed());

            if (facing != null && Mouse.current != null)
            {
                facing.TrySetFacingFromScreen(Mouse.current.position.ReadValue());
            }
        }

        private void ResolveActions()
        {
            moveAction = null;
            attackAction = null;
            if (inputActions == null)
            {
                return;
            }

            InputActionMap map = inputActions.FindActionMap(actionMapName, false);
            if (map == null)
            {
                return;
            }

            moveAction = map.FindAction(moveActionName, false);
            attackAction = map.FindAction(attackActionName, false);
        }
    }
}
