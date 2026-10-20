using Unity.Cinemachine;
using UnityEngine;

namespace ProjectRidge.CameraRig
{
    /// <summary>
    /// Golpe de cámara (extensión de Cinemachine): un resorte desplaza y ladea la cámara en la dirección del impacto
    /// y vuelve con un rebote corto. Para golpes que pesan (garrote que conecta, zarpazo recibido).
    /// </summary>
    public class CameraShake : CinemachineExtension
    {
        [Tooltip("Metros de desplazamiento por unidad de fuerza.")]
        [Min(0f)] [SerializeField] private float metersPerUnit = 0.22f;
        [Tooltip("Grados de ladeo por unidad de fuerza.")]
        [Min(0f)] [SerializeField] private float degreesPerUnit = 1.6f;
        [Min(1f)] [SerializeField] private float stiffness = 260f;
        [Min(0f)] [SerializeField] private float damping = 16f;

        private static CameraShake instance;
        private Vector3 offset, offsetVel;
        private float roll, rollVel;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        /// <summary>Patada en dirección (mundo) con fuerza ~0.5 (leve) a 2 (fuerte).</summary>
        public static void Kick(Vector3 direction, float strength)
        {
            if (instance == null) return;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
            instance.offsetVel += direction * (instance.metersPerUnit * strength * instance.stiffness * 0.12f);
            instance.rollVel += Random.Range(-1f, 1f) * instance.degreesPerUnit * strength * instance.stiffness * 0.12f;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f); // también durante el hit-stop
            offsetVel += (-stiffness * offset - damping * offsetVel) * dt;
            offset += offsetVel * dt;
            rollVel += (-stiffness * roll - damping * rollVel) * dt;
            roll += rollVel * dt;
            state.PositionCorrection += offset;
            state.OrientationCorrection = state.OrientationCorrection * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
