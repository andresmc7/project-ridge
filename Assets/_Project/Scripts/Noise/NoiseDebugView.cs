using UnityEngine;

namespace ProjectRidge.Noise
{
    /// <summary>Debug: dibuja en el juego un anillo por cada ruido, del tamaño de su radio, que se desvanece.</summary>
    public class NoiseDebugView : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color color = new Color(1f, 0.85f, 0.3f, 0.8f);
        [Min(0.05f)] [SerializeField] private float lifetime = 0.6f;
        [Min(1)] [SerializeField] private int poolSize = 12;
        [Min(8)] [SerializeField] private int segments = 40;
        [Min(0.005f)] [SerializeField] private float width = 0.05f;

        private LineRenderer[] rings;
        private float[] bornAt;
        private int next;

        private void Awake()
        {
            rings = new LineRenderer[poolSize];
            bornAt = new float[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("NoiseRing");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = lineMaterial;
                lr.loop = true;
                lr.useWorldSpace = true;
                lr.positionCount = segments;
                lr.widthMultiplier = width;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                rings[i] = lr;
            }
        }

        private void OnEnable() => NoiseSystem.NoiseEmitted += OnNoise;
        private void OnDisable() => NoiseSystem.NoiseEmitted -= OnNoise;

        private void OnNoise(NoiseEvent noise)
        {
            var lr = rings[next];
            bornAt[next] = Time.time;
            next = (next + 1) % rings.Length;

            Vector3 center = noise.Position + Vector3.up * 0.05f;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * noise.Radius);
            }
            lr.enabled = true;
        }

        private void Update()
        {
            for (int i = 0; i < rings.Length; i++)
            {
                var lr = rings[i];
                if (!lr.enabled) continue;
                float age = (Time.time - bornAt[i]) / lifetime;
                if (age >= 1f) { lr.enabled = false; continue; }
                Color c = color;
                c.a *= 1f - age;
                lr.startColor = c;
                lr.endColor = c;
            }
        }
    }
}
