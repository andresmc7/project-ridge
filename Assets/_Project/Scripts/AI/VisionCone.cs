using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Cono de visión del Lurker dibujado en el suelo, recortado por lo que tapa la vista (rocas, troncos, terreno).
    /// Lo que se ve es lo que hay: dentro del cono, de pie o sin esconderse, te detecta. Rojo tenue en calma, se
    /// enciende con la sospecha y queda rojo intenso en la persecución. Se apaga al morir.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VisionCone : MonoBehaviour
    {
        [SerializeField] private EnemyController enemy;
        [SerializeField] private EnemyConfig config;
        [Tooltip("Lo mismo que tapa la vista en EnemyPerception.")]
        [SerializeField] private LayerMask occluders = 1;
        [Range(8, 64)] [SerializeField] private int rays = 28;
        [Tooltip("Altura de los rayos (aprox. el pecho del papá).")]
        [Min(0.1f)] [SerializeField] private float rayHeight = 1.1f;
        [Min(0f)] [SerializeField] private float groundLift = 0.06f;
        [Min(0.02f)] [SerializeField] private float updateInterval = 0.05f;
        // Sin iluminación y con la exposición alta del grade: alfas bajos para que se lea como mirada, no como lámina.
        [SerializeField] private Color calm = new Color(1f, 0.25f, 0.15f, 0.045f);
        [SerializeField] private Color suspicious = new Color(1f, 0.6f, 0.15f, 0.1f);
        [SerializeField] private Color hunting = new Color(1f, 0.08f, 0.05f, 0.13f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Mesh mesh;
        private Vector3[] vertices;
        private Vector2[] uvs;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private float next;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            mesh = new Mesh { name = "VisionCone" };
            mesh.MarkDynamic();
            vertices = new Vector3[rays + 1];
            uvs = new Vector2[rays + 1];
            var tris = new int[(rays - 1) * 3];
            for (int i = 0; i < rays - 1; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = i + 2; }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = tris;
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void LateUpdate()
        {
            // El cono vive en el espacio del Lurker pero plano sobre el suelo.
            transform.localPosition = Vector3.zero;
            transform.rotation = Quaternion.Euler(0f, enemy.transform.eulerAngles.y, 0f);

            bool dead = enemy.IsDead;
            meshRenderer.enabled = !dead;
            if (dead) return;

            if (Time.time >= next)
            {
                next = Time.time + updateInterval;
                Rebuild();
            }

            float s = enemy.Perception.Suspicion;
            Color c = enemy.IsHunting ? hunting : Color.Lerp(calm, suspicious, Mathf.Clamp01(s / config.investigateThreshold));
            if (enemy.State == EnemyState.Investigate || enemy.State == EnemyState.Search) c = suspicious;
            if (block == null) block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            meshRenderer.SetPropertyBlock(block);
        }

        private void Rebuild()
        {
            float range = config.viewDistance;
            float half = config.viewAngle * 0.5f;
            Vector3 origin = enemy.transform.position + Vector3.up * rayHeight;
            Quaternion yaw = Quaternion.Euler(0f, enemy.transform.eulerAngles.y, 0f);
            vertices[0] = Vector3.up * groundLift;
            uvs[0] = Vector2.zero;
            for (int i = 0; i < rays; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)(rays - 1));
                Vector3 dir = yaw * Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                float d = range;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, range, occluders, QueryTriggerInteraction.Ignore)) d = hit.distance;
                Vector3 local = Quaternion.Euler(0f, a, 0f) * Vector3.forward * d;
                local.y = groundLift;
                vertices[i + 1] = local;
                uvs[i + 1] = new Vector2(d / range, i / (float)(rays - 1));
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
        }
    }
}
