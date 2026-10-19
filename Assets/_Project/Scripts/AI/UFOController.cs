using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// OVNI con reflector. Mueve un punto iluminado (foco) entre puntos de escaneo con pausas; el OVNI flota
    /// arriba y desplazado, así el haz cae inclinado. Al detectarte (UFOExposure) sigue tu posición un rato.
    /// </summary>
    public class UFOController : MonoBehaviour
    {
        [SerializeField] private UFOConfig config;
        [SerializeField] private Transform[] scanPoints;
        [SerializeField] private Light beam;
        [Tooltip("Cono visible (malla) que se escala para llegar al suelo.")]
        [SerializeField] private Transform beamVisual;
        [SerializeField] private LayerMask groundMask = 1;

        private enum Mode { Scanning, Tracking }

        private Mode mode = Mode.Scanning;
        private Vector3 focus;
        private int pointIndex;
        private int direction = 1;
        private float pauseTimer;
        private Vector3 trackTarget;
        private float trackTimer;

        public Vector3 BeamOrigin => beam.transform.position;
        public Vector3 BeamDirection => beam.transform.forward;
        public float BeamAngle => config.beamAngle;
        public float BeamRange => config.beamRange;
        public bool IsTracking => mode == Mode.Tracking;

        private void Start()
        {
            focus = scanPoints.Length > 0 ? scanPoints[0].position : transform.position;
            beam.spotAngle = config.beamAngle;
            beam.innerSpotAngle = config.beamAngle * 0.7f;
            beam.intensity = config.lightIntensity;
            beam.color = config.lightColor;
            beam.range = config.beamRange;
            Apply();
        }

        /// <summary>Lo llama UFOExposure mientras te está viendo.</summary>
        public void Track(Vector3 target)
        {
            mode = Mode.Tracking;
            trackTarget = target;
            trackTimer = config.trackLinger;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (mode == Mode.Tracking)
            {
                focus = Vector3.MoveTowards(focus, trackTarget, config.trackSpeed * dt);
                trackTimer -= dt;
                if (trackTimer <= 0f)
                {
                    mode = Mode.Scanning;
                    pointIndex = NearestPoint();
                }
            }
            else if (scanPoints.Length > 0)
            {
                Vector3 target = scanPoints[pointIndex].position;
                if ((focus - target).sqrMagnitude < 0.01f)
                {
                    pauseTimer += dt;
                    if (pauseTimer >= config.pauseAtPoint)
                    {
                        pauseTimer = 0f;
                        // Barrido de ida y vuelta.
                        if (pointIndex + direction >= scanPoints.Length || pointIndex + direction < 0) direction = -direction;
                        pointIndex += direction;
                    }
                }
                else
                {
                    focus = Vector3.MoveTowards(focus, target, config.scanSpeed * dt);
                }
            }
            Apply();
        }

        private void Apply()
        {
            // Proyectar el foco sobre el terreno (no sobre copas ni techos: la nave no debe saltar de altura).
            Vector3 ground = focus;
            var terrain = Terrain.activeTerrain;
            if (terrain != null) ground.y = terrain.SampleHeight(focus) + terrain.transform.position.y;
            else if (Physics.Raycast(focus + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
                ground = hit.point;

            Vector3 offsetDir = Quaternion.Euler(0f, config.offsetBearing, 0f) * Vector3.forward;
            transform.position = ground + Vector3.up * config.altitude + offsetDir * config.horizontalOffset;
            Vector3 toGround = ground - transform.position;
            beam.transform.rotation = Quaternion.LookRotation(toGround);

            if (beamVisual != null)
            {
                float length = toGround.magnitude * config.beamVisualOvershoot;
                float radius = Mathf.Tan(config.beamAngle * 0.5f * Mathf.Deg2Rad) * length;
                beamVisual.rotation = beam.transform.rotation;
                beamVisual.localScale = new Vector3(radius, radius, length);
            }
        }

        private int NearestPoint()
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < scanPoints.Length; i++)
            {
                float d = (scanPoints[i].position - focus).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}
