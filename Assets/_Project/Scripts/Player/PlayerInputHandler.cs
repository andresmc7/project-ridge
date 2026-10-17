using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Traduce las Input Actions a intenciones del jugador. Teclado, control y táctil pasan por las mismas acciones:
    /// los controles táctiles (On-Screen Stick/Button) escriben en un Gamepad virtual.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionReference move;
        [SerializeField] private InputActionReference sprint;
        [SerializeField] private InputActionReference crouch;
        [SerializeField] private InputActionReference interact;
        [SerializeField] private InputActionReference attack;
        [SerializeField] private InputActionReference aim;
        [SerializeField] private InputActionReference flashlight;
        [SerializeField] private InputActionReference pause;

        // Teclado: Shift se mantiene. Control y táctil: el botón alterna (mantener L3 mientras se mueve el stick es incómodo).
        private bool keyboardSprintHeld;
        private bool sprintLatched;

        public Vector2 Move => move.action.ReadValue<Vector2>();
        public bool SprintRequested => keyboardSprintHeld || sprintLatched;

        public event Action CrouchPressed;
        /// <summary>Interact, Attack, Aim, Flashlight y Pause. Sin gameplay todavía (M4); por ahora solo los muestra el debug.</summary>
        public event Action<InputAction> ActionPerformed;

        /// <summary>Lo llama el controlador cuando el sprint con toggle debe terminar (jugador quieto o agachado).</summary>
        public void CancelSprintLatch() => sprintLatched = false;

        private void OnEnable()
        {
            move.action.Enable();
            sprint.action.Enable();
            crouch.action.Enable();
            sprint.action.performed += OnSprintPerformed;
            sprint.action.canceled += OnSprintCanceled;
            crouch.action.performed += OnCrouchPerformed;
            foreach (var reference in OtherActions())
            {
                reference.action.Enable();
                reference.action.performed += OnOtherPerformed;
            }
        }

        private void OnDisable()
        {
            sprint.action.performed -= OnSprintPerformed;
            sprint.action.canceled -= OnSprintCanceled;
            crouch.action.performed -= OnCrouchPerformed;
            foreach (var reference in OtherActions())
                reference.action.performed -= OnOtherPerformed;
            keyboardSprintHeld = false;
            sprintLatched = false;
        }

        private InputActionReference[] otherActions;
        private InputActionReference[] OtherActions() =>
            otherActions ??= new[] { interact, attack, aim, flashlight, pause };

        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            if (context.control.device is Keyboard)
                keyboardSprintHeld = true;
            else
                sprintLatched = !sprintLatched;
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            if (context.control.device is Keyboard)
                keyboardSprintHeld = false;
        }

        private void OnCrouchPerformed(InputAction.CallbackContext context) => CrouchPressed?.Invoke();

        private void OnOtherPerformed(InputAction.CallbackContext context) => ActionPerformed?.Invoke(context.action);
    }
}
