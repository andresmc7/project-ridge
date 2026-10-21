using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Densidad de vegetación escondite del nivel (POLISH_PASS 3.9), 0–1 por celda. Lo genera el editor a partir de
    /// ruido y rasgos del terreno (rocas, bordes, zonas húmedas, orillas) más los escondites de diseño; la vegetación
    /// se planta según este mismo mapa, así lo que se ve y lo que esconde coinciden. El escondite se calcula
    /// muestreando aquí la posición del papá (no con triggers).
    /// </summary>
    [CreateAssetMenu(menuName = "Project Ridge/Hide Density Map", fileName = "HideDensityMap")]
    public class HideDensityMap : ScriptableObject
    {
        [Tooltip("Esquina (x, z) de la celda 0.")]
        public Vector2 origin;
        [Min(0.05f)] public float cellSize = 0.25f;
        public int width, height;
        [Tooltip("Densidad por celda (0–255), fila a fila desde origin.")]
        [HideInInspector] public byte[] cells;

        public float At(int x, int z)
        {
            if (cells == null || x < 0 || z < 0 || x >= width || z >= height) return 0f;
            return cells[z * width + x] / 255f;
        }

        /// <summary>Densidad interpolada en un punto del mundo.</summary>
        public float Sample(Vector3 world)
        {
            float fx = (world.x - origin.x) / cellSize - 0.5f, fz = (world.z - origin.y) / cellSize - 0.5f;
            int x = Mathf.FloorToInt(fx), z = Mathf.FloorToInt(fz);
            float tx = fx - x, tz = fz - z;
            float a = Mathf.Lerp(At(x, z), At(x + 1, z), tx);
            float b = Mathf.Lerp(At(x, z + 1), At(x + 1, z + 1), tx);
            return Mathf.Lerp(a, b, tz);
        }
    }
}
