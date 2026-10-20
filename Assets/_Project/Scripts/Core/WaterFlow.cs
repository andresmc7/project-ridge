using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Agua del arroyo: desplaza el mapa de normales (ondas) río abajo, en dos capas a distinta velocidad para que
    /// el reflejo de la luna se mueva y se lea como corriente, no como una superficie quieta.
    /// </summary>
    public class WaterFlow : MonoBehaviour
    {
        [SerializeField] private Material water;
        [Tooltip("Velocidad de la capa principal de ondas (UV por segundo).")]
        [SerializeField] private Vector2 flow = new Vector2(0f, -0.18f);
        [Tooltip("Velocidad de la segunda capa (detalle), cruzada.")]
        [SerializeField] private Vector2 detailFlow = new Vector2(0.05f, -0.31f);

        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int DetailNormal = Shader.PropertyToID("_DetailNormalMap");

        private void Update()
        {
            if (water == null) return;
            float t = Time.time;
            water.SetTextureOffset(BumpMap, flow * t);
            if (water.HasProperty(DetailNormal)) water.SetTextureOffset(DetailNormal, detailFlow * t);
        }

        private void OnDisable()
        {
            if (water == null) return;
            water.SetTextureOffset(BumpMap, Vector2.zero);
            if (water.HasProperty(DetailNormal)) water.SetTextureOffset(DetailNormal, Vector2.zero);
        }
    }
}
