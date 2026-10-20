using System.Collections.Generic;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Bloques de piedra de los muros de terraza (Echoes of Wisdom): cajas de bordes redondeados con algo de ruido,
    /// color por vértice (piedra cálida, junta oscura abajo, musgo arriba). Se acumulan en lotes por zona para pocos
    /// draw calls; un solo material (FoliageWind sin viento, con _UseVertexColor).
    /// </summary>
    public class StoneBatch
    {
        public struct Palette { public Color stoneA, stoneB, moss, grout; }

        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        readonly List<int> t = new List<int>();

        public int VertexCount => v.Count;

        static float Hash(Vector3 p) => Mathf.Abs(Mathf.Sin(Vector3.Dot(p, new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f) % 1f;

        /// <summary>
        /// Un bloque: centro y tamaño en metros, orientado con forward = cara visible. Esquinas redondeadas (radio
        /// relativo), ruido de superficie y color por vértice. mossTop: musgo en la cara de arriba.
        /// </summary>
        public void Block(Vector3 center, Quaternion rot, Vector3 size, float round, Palette pal, float tone, bool mossTop, int seed)
        {
            const int div = 4;
            Vector3 half = size * 0.5f;
            float r = Mathf.Min(round, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.9f);
            Vector3 inner = half - Vector3.one * r;
            Color baseCol = Color.Lerp(pal.stoneA, pal.stoneB, tone);
            Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var fn in faceN)
            {
                if (fn == Vector3.down) continue; // nunca se ve
                Vector3 u = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(fn, u);
                int start = v.Count;
                for (int j = 0; j <= div; j++)
                    for (int i = 0; i <= div; i++)
                    {
                        // Punto del cubo [-1,1]^3 en esta cara, luego caja redondeada.
                        Vector3 q = fn + u * (i / (float)div * 2f - 1f) + w * (j / (float)div * 2f - 1f);
                        Vector3 p = Vector3.Scale(q, half);
                        Vector3 clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 dir = (p - clamped);
                        Vector3 normal = dir.sqrMagnitude > 1e-6f ? dir.normalized : fn;
                        Vector3 pos = clamped + normal * r;
                        // Ruido suave: la piedra no es una caja perfecta.
                        float bump = (Mathf.PerlinNoise(pos.x * 2.1f + seed * 0.37f, pos.y * 2.1f + pos.z * 1.7f) - 0.5f) * 0.07f;
                        pos += normal * bump;
                        Vector3 world = center + rot * pos;
                        Vector3 worldN = rot * normal;
                        // Color: juntas y base oscuras, cara de arriba con musgo si toca, motas.
                        float speck = Hash(world * 7.3f) * 0.12f - 0.06f;
                        Color col = baseCol * (1f + speck);
                        float edge = 1f - Mathf.Clamp01(Vector3.Dot(normal, fn)); // en el redondeo
                        col = Color.Lerp(col, pal.grout, edge * 0.35f);
                        float lower = Mathf.InverseLerp(half.y, -half.y, pos.y);
                        col = Color.Lerp(col, pal.grout, lower * lower * 0.45f);
                        if (mossTop && worldN.y > 0.55f) col = Color.Lerp(col, pal.moss, Mathf.InverseLerp(0.55f, 0.9f, worldN.y) * 0.85f);
                        col.a = Mathf.Lerp(0.55f, 1f, 1f - lower * 0.7f) * (1f - edge * 0.25f);
                        v.Add(world);
                        n.Add(worldN);
                        c.Add(col);
                    }
                for (int j = 0; j < div; j++)
                    for (int i = 0; i < div; i++)
                    {
                        int a = start + j * (div + 1) + i, b = a + 1, d = a + div + 1, e = d + 1;
                        t.Add(a); t.Add(b); t.Add(d);
                        t.Add(b); t.Add(e); t.Add(d);
                    }
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
