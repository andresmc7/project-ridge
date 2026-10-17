using System;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Locomoción del papá con CharacterController: modos Crouch/Walk/Jog/Sprint, movimiento relativo a la cámara,
    /// giro suave y gravedad. Expone el modo y la velocidad real para el sistema de ruido (M2).
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

        public MovementMode Mode { get; private set; } = MovementMode.Idle;
        /// <summary>Velocidad horizontal real en m/s, después de colisiones.</summary>
        public float CurrentSpeed { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public PlayerMovementConfig Config => config;

        public event Action<MovementMode> ModeChanged;

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
            float dt = Time.deltaTime;
            Vector2 stick = Vector2.ClampMagnitude(input.Move, 1f);
            float tilt = stick.magnitude;
            bool hasInput = tilt >= config.stickDeadzone;

            UpdateCrouch(dt);
            if (!hasInput || IsCrouching)
                input.CancelSprintLatch();

            MovementMode mode = ResolveMode(hasInput, tilt);
            Vector3 desiredDirection = hasInput ? CameraRelative(stick / tilt) : Vector3.zero;

            float targetSpeed = hasInput ? SpeedFor(mode) : 0f;
            Vector3 targetVelocity = desiredDirection * targetSpeed;
            float rate = targetVelocity.sqrMagnitude >= horizontalVelocity.sqrMagnitude ? config.acceleration : config.deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * dt);

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = config.groundedStickVelocity;
            verticalVelocity += config.gravity * dt;

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);

            if (hasInput)
            {
                Quaternion target = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, config.turnSpeed * dt);
            }

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
