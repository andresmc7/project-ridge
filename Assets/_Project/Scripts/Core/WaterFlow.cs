using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Agua del arroyo: desplaza el mapa de normales (ondas) río abajo, en dos capas a distinta velocidad para que
    /// el reflejo de la luna se mueva y se lea como corriente, no como una superficie quieta.
    /// Anima una copia del material (en el editor, mover el asset compartido lo dejaba modificado tras cada partida).
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

        private Material instance;

        private void Start()
        {
            if (water == null) return;
            instance = new Material(water) { name = water.name + " (flow)" };
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == water) { mats[i] = instance; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private void Update()
        {
            if (instance == null) return;
            float t = Time.time;
            instance.SetTextureOffset(BumpMap, flow * t);
            if (instance.HasProperty(DetailNormal)) instance.SetTextureOffset(DetailNormal, detailFlow * t);
        }

        private void OnDestroy()
        {
            if (instance != null) Destroy(instance);
        }
    }
}
