using System;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Locomoción del papá con CharacterController: modos Crouch/Walk/Jog/Sprint, movimiento relativo a la cámara,
    /// giro casi instantáneo con avance alineado al cuerpo (sin deslizarse) y gravedad. Expone el modo y la velocidad real para el sistema de ruido (M2).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerMovementConfig config;
        [SerializeField] private PlayerInputHandler input;
        [Tooltip("Cámara de referencia para el movimiento. Si está vacío se usa Camera.main.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Raíz visual con pivote en los pies; se escala en Y al agacharse.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Capas que impiden levantarse si hay algo encima (no incluir Player ni TallGrass).")]
        [SerializeField] private LayerMask standUpBlockers = ~0;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private bool crouchRequested;
        private Vector3 externalVelocity;

        public MovementMode Mode { get; private set; } = MovementMode.Idle;
        /// <summary>Velocidad horizontal real en m/s, después de colisiones.</summary>
        public float CurrentSpeed { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public PlayerMovementConfig Config => config;

        /// <summary>Lo baja el combate mientras golpea (1 = normal, 0 = quieto).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Sin control (muerto, fabricando, menús). La gravedad sigue.</summary>
        public bool InputLocked { get; set; }
        /// <summary>Bloqueo breve de una acción animada (registrar, curarse, recoger). Independiente del flujo de juego.</summary>
        public bool ActionLocked { get; set; }
        /// <summary>Dirección de movimiento pedida este frame (plana, normalizada; cero si no hay input).</summary>
        public Vector3 DesiredDirection { get; private set; }

        public event Action<MovementMode> ModeChanged;

        /// <summary>Empujón externo (golpe recibido, embestida del garrote): se suma al movimiento y se frena solo.</summary>
        public void AddKnockback(Vector3 velocity)
        {
            velocity.y = 0f;
            externalVelocity += velocity;
        }

        /// <summary>Gira de inmediato hacia un punto (atacar o disparar a un objetivo).</summary>
        public void FaceTowards(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            ApplyHeight(config.standHeight);
        }

        private void OnEnable() => input.CrouchPressed += OnCrouchPressed;
        private void OnDisable() => input.CrouchPressed -= OnCrouchPressed;

        private void OnCrouchPressed() => crouchRequested = !crouchRequested;

        private void Update()
        {
            // Desactivado durante la captura o el reposicionamiento: no hay nada que mover.
            if (!controller.enabled) return;
            float dt = Time.deltaTime;
            Vector2 stick = InputLocked || ActionLocked ? Vector2.zero : Vector2.ClampMagnitude(input.Move, 1f);
            float tilt = stick.magnitude;
            bool hasInput = tilt >= config.stickDeadzone;

            UpdateCrouch(dt);
            if (!hasInput || IsCrouching)
                input.CancelSprintLatch();

            MovementMode mode = ResolveMode(hasInput, tilt);
            Vector3 desiredDirection = hasInput ? CameraRelative(stick / tilt) : Vector3.zero;

            DesiredDirection = desiredDirection;

            // Giro primero, rápido: el cuerpo encara el input casi al instante.
            if (hasInput && SpeedMultiplier > 0.5f)
            {
                Quaternion target = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, config.turnSpeed * dt);
            }

            // Se avanza siempre hacia donde mira el cuerpo, escalado por lo alineado que está con el input:
            // nunca se desliza de lado ni camina de espaldas al cambiar de dirección (pivote limpio).
            float targetSpeed = hasInput ? SpeedFor(mode) * SpeedMultiplier : 0f;
            float alignment = hasInput ? Mathf.Clamp01(Vector3.Dot(transform.forward, desiredDirection)) : 0f;
            alignment = Mathf.Pow(alignment, config.alignmentSharpness);
            Vector3 targetVelocity = transform.forward * targetSpeed * alignment;
            if (SpeedMultiplier > 0.5f) horizontalVelocity = transform.forward * horizontalVelocity.magnitude;
            float rate = targetVelocity.sqrMagnitude >= horizontalVelocity.sqrMagnitude ? config.acceleration : config.deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * dt);

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = config.groundedStickVelocity;
            verticalVelocity += config.gravity * dt;

            controller.Move((horizontalVelocity + externalVelocity + Vector3.up * verticalVelocity) * dt);
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, config.knockbackDrag * dt);

            // Velocidad real después de colisiones; es la que debe usar el ruido. CharacterController ya desliza
            // por paredes y esquinas, así que horizontalVelocity no se recorta al chocar.
            Vector3 actual = controller.velocity;
            actual.y = 0f;
            CurrentSpeed = actual.magnitude;

            SetMode(mode);
        }

        private MovementMode ResolveMode(bool hasInput, float tilt)
        {
            if (IsCrouching) return MovementMode.Crouch;
            if (!hasInput) return MovementMode.Idle;
            if (input.SprintRequested) return MovementMode.Sprint;
            return tilt >= config.jogThreshold ? MovementMode.Jog : MovementMode.Walk;
        }

        private float SpeedFor(MovementMode mode)
        {
            switch (mode)
            {
                case MovementMode.Crouch: return config.crouchSpeed;
                case MovementMode.Walk: return config.walkSpeed;
                case MovementMode.Jog: return config.jogSpeed;
                case MovementMode.Sprint: return config.sprintSpeed;
                default: return 0f;
            }
        }

        private Vector3 CameraRelative(Vector2 direction)
        {
            if (cameraTransform == null)
                return new Vector3(direction.x, 0f, direction.y);
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = cameraTransform.right;
            right.y = 0f;
            right.Normalize();
            return (forward * direction.y + right * direction.x).normalized;
        }

        private void UpdateCrouch(float dt)
        {
            if (!crouchRequested && IsCrouching && !CanStandUp())
                crouchRequested = true;

            IsCrouching = crouchRequested;
            float targetHeight = IsCrouching ? config.crouchHeight : config.standHeight;
            if (!Mathf.Approximately(controller.height, targetHeight))
                ApplyHeight(Mathf.MoveTowards(controller.height, targetHeight, config.crouchTransitionSpeed * dt));
        }

        private bool CanStandUp()
        {
            float radius = controller.radius;
            Vector3 bottom = transform.position + Vector3.up * radius;
            Vector3 top = transform.position + Vector3.up * (config.standHeight - radius);
            return !Physics.CheckCapsule(bottom, top, radius * 0.95f, standUpBlockers, QueryTriggerInteraction.Ignore);
        }

        private void ApplyHeight(float height)
        {
            controller.height = height;
            controller.center = Vector3.up * (height * 0.5f);
            if (visualRoot != null)
                visualRoot.localScale = new Vector3(1f, height / config.standHeight, 1f);
        }

        private void SetMode(MovementMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
