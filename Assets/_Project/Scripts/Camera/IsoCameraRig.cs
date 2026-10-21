using ProjectRidge.AI;
using ProjectRidge.Player;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace ProjectRidge.CameraRig
{
    /// <summary>
    /// Aplica el ángulo fijo 3/4 a una CinemachineCamera con CinemachineFollow en World Space (POLISH_PASS 3.6):
    /// - el encuadre se adelanta en la dirección de avance (ver lo que viene);
    /// - con un Lurker en Chase o un haz del OVNI cerca, la cámara se aleja y encuadra entre el papá y la amenaza.
    /// Se reaplica cada frame para poder ajustar IsoCameraConfig en play mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineFollow))]
    public class IsoCameraRig : MonoBehaviour
    {
        [SerializeField] private IsoCameraConfig config;

        private CinemachineCamera cinemachineCamera;
        private CinemachineFollow follow;
        private PlayerController player;
        private Vector3 lookAhead, lookAheadVelocity;
        private float threat;
        private Vector3 threatOffset;

        /// <summary>Multiplicador del campo de visión (1 = normal; la captura del OVNI lo baja para acercar la cámara).</summary>
        public static float ZoomScale = 1f;
        /// <summary>0–1: cuánto pesa ahora el encuadre de amenaza (para QA).</summary>
        public float ThreatBlend => threat;

        private void OnEnable()
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
            follow = GetComponent<CinemachineFollow>();
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (config == null) return;
            bool playing = Application.isPlaying;
            if (playing && player == null) player = FindFirstObjectByType<PlayerController>();
            float dt = playing ? Time.deltaTime : 0f;

            // Mirar adelante: hacia donde avanza, proporcional a la velocidad (quieto, centrado).
            Vector3 wantAhead = Vector3.zero;
            Vector3 threatPoint = Vector3.zero;
            bool threatened = false;
            if (playing && player != null)
            {
                float moving = Mathf.Clamp01(player.CurrentSpeed / Mathf.Max(0.01f, player.Config.walkSpeed));
                wantAhead = player.transform.forward * (config.lookAhead * moving);
                threatened = FindThreat(player.transform.position, out threatPoint);
                lookAhead = Vector3.SmoothDamp(lookAhead, wantAhead, ref lookAheadVelocity, config.lookAheadSmoothing, Mathf.Infinity, dt);
                threat = Mathf.MoveTowards(threat, threatened ? 1f : 0f, dt / config.threatBlendTime);
                if (threatened)
                {
                    Vector3 to = threatPoint - player.transform.position;
                    to.y = 0f;
                    threatOffset = to * config.threatWeight;
                }
            }
            else { lookAhead = Vector3.zero; threat = 0f; }

            float distance = Mathf.Lerp(config.distance, config.threatDistance, threat);
            Quaternion rotation = Quaternion.Euler(config.pitch, config.yaw, 0f);
            transform.rotation = rotation;
            follow.FollowOffset = Vector3.up * config.targetHeight + rotation * new Vector3(0f, 0f, -distance)
                                  + Quaternion.Euler(0f, config.yaw, 0f) * Vector3.forward * config.framingOffset
                                  + lookAhead * (1f - threat) + threatOffset * threat;

            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = Vector3.one * config.positionDamping;
            follow.TrackerSettings = tracker;

            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = config.fieldOfView * (playing ? ZoomScale : 1f);
            cinemachineCamera.Lens = lens;
        }

        /// <summary>La amenaza más cercana: un Lurker cazando o un haz del OVNI a menos de threatBeamRadius.</summary>
        private bool FindThreat(Vector3 p, out Vector3 point)
        {
            point = Vector3.zero;
            float best = float.MaxValue;
            foreach (var e in EnemyController.Active)
            {
                if (!e.IsHunting) continue;
                float d = (e.transform.position - p).sqrMagnitude;
                if (d < best) { best = d; point = e.transform.position; }
            }
            float r2 = config.threatBeamRadius * config.threatBeamRadius;
            foreach (var s in Searchlight.Active)
            {
                Vector3 d = s.Spot - p;
                d.y = 0f;
                if (d.sqrMagnitude < r2 && d.sqrMagnitude < best) { best = d.sqrMagnitude; point = s.Spot; }
            }
            return best < float.MaxValue;
        }
    }
}
