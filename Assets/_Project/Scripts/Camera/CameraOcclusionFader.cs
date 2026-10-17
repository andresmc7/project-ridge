using UnityEngine;

namespace ProjectRidge.CameraRig
{
    /// <summary>
    /// Desvanece los OcclusionFadeable que quedan entre la cámara y el jugador, para no perderlo de vista
    /// detrás de árboles o rocas (spec, sección 5).
    /// </summary>
    public class CameraOcclusionFader : MonoBehaviour
    {
        [SerializeField] private IsoCameraConfig config;
        [SerializeField] private Transform target;

        private float nextCheck;

        private void LateUpdate()
        {
            if (target == null) return;

            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + config.occlusionCheckInterval;
                Vector3 from = transform.position;
                Vector3 to = target.position + Vector3.up * config.targetHeight;
                Vector3 delta = to - from;
                float length = delta.magnitude;
                var ray = new Ray(from, delta / length);

                foreach (var f in OcclusionFadeable.All)
                {
                    Bounds b = f.WorldBounds;
                    b.Expand(config.occlusionPadding * 2f);
                    bool blocks = b.IntersectRay(ray, out float hit) && hit < length - config.occlusionPadding;
                    f.TargetAlpha = blocks ? config.occludedAlpha : 1f;
                }
            }

            float dt = Time.deltaTime;
            foreach (var f in OcclusionFadeable.All)
                f.Step(config.occlusionFadeSpeed, dt);
        }
    }
}
