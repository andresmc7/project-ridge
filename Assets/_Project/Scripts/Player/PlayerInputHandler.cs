using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Traduce las Input Actions a intenciones del jugador (POLISH_PASS 3.1): mover, ACCIÓN contextual, OBJETO,
    /// cambiar de objeto, linterna, curar y pausa. Teclado, control y táctil pasan por las mismas acciones: los
    /// controles táctiles escriben en un Gamepad virtual. No hay botón de agacharse (C fuerza el agachado solo para
    /// pruebas) ni de sprint en táctil (se fija empujando el joystick al borde).
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionReference move;
        [SerializeField] private InputActionReference sprint;
        [Tooltip("Solo pruebas (C): fuerza el agachado. El juego se agacha solo.")]
        [SerializeField] private InputActionReference crouch;
        [FormerlySerializedAs("interact")]
        [SerializeField] private InputActionReference action;
        [FormerlySerializedAs("fire")]
        [SerializeField] private InputActionReference useObject;
        [FormerlySerializedAs("throwStone")]
        [SerializeField] private InputActionReference cycleObject;
        [SerializeField] private InputActionReference flashlight;
        [SerializeField] private InputActionReference pause;
        [SerializeField] private InputActionReference heal;

        // Teclado: Shift se mantiene. Control: L3 alterna. Táctil: el joystick flotante escribe L3 al arrastrar más
        // allá del anillo; empujar al borde y mantener lo fija el controlador.
        private bool keyboardSprintHeld;
        private bool sprintLatched;

        public Vector2 Move => move.action.ReadValue<Vector2>();
        public bool SprintRequested => keyboardSprintHeld || sprintLatched;
        /// <summary>El movimiento viene del teclado (siempre "al borde": no fija el sprint por empuje).</summary>
        public bool MoveFromKeyboard => move.action.activeControl != null && move.action.activeControl.device is Keyboard;

        public event Action CrouchPressed;
        public event Action FlashlightPressed;
        /// <summary>ACCIÓN contextual (inspeccionar, usar, recoger, registrar, derribo, golpe, empujar).</summary>
        public event Action ActionPressed;
        /// <summary>OBJETO: lanzar el objeto activo al objetivo inteligente.</summary>
        public event Action ObjectPressed;
        public event Action CycleObjectPressed;
        public event Action HealPressed;
        public event Action PausePressed;
        /// <summary>Todas las acciones de botón (para el debug overlay).</summary>
        public event Action<InputAction> ActionPerformed;

        /// <summary>Lo llama el controlador cuando el sprint fijado debe terminar (soltar, frenar o agacharse).</summary>
        public void CancelSprintLatch() => sprintLatched = false;

        /// <summary>Fija el sprint (joystick empujado al borde el tiempo suficiente).</summary>
        public void LatchSprint() => sprintLatched = true;

        private void OnEnable()
        {
            move.action.Enable();
            sprint.action.Enable();
            sprint.action.performed += OnSprintPerformed;
            sprint.action.canceled += OnSprintCanceled;
            foreach (var reference in ButtonActions())
            {
                reference.action.Enable();
                reference.action.performed += OnButtonPerformed;
            }
        }

        private void OnDisable()
        {
            sprint.action.performed -= OnSprintPerformed;
            sprint.action.canceled -= OnSprintCanceled;
            foreach (var reference in ButtonActions())
                reference.action.performed -= OnButtonPerformed;
            keyboardSprintHeld = false;
            sprintLatched = false;
        }

        private InputActionReference[] buttonActions;
        private InputActionReference[] ButtonActions()
        {
            if (buttonActions != null) return buttonActions;
            var list = new System.Collections.Generic.List<InputActionReference>();
            foreach (var r in new[] { crouch, action, useObject, cycleObject, flashlight, pause, heal })
                if (r != null) list.Add(r);
            return buttonActions = list.ToArray();
        }

        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            if (context.control.device is Keyboard)
                keyboardSprintHeld = true;
            else
                sprintLatched = true;
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            if (context.control.device is Keyboard)
                keyboardSprintHeld = false;
        }

        private void OnButtonPerformed(InputAction.CallbackContext context)
        {
            var a = context.action;
            if (crouch != null && a == crouch.action) CrouchPressed?.Invoke();
            else if (a == action.action) ActionPressed?.Invoke();
            else if (useObject != null && a == useObject.action) ObjectPressed?.Invoke();
            else if (cycleObject != null && a == cycleObject.action) CycleObjectPressed?.Invoke();
            else if (flashlight != null && a == flashlight.action) FlashlightPressed?.Invoke();
            else if (a == pause.action) PausePressed?.Invoke();
            else if (heal != null && a == heal.action) HealPressed?.Invoke();
            ActionPerformed?.Invoke(a);
        }
    }
}
