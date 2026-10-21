using System.Collections.Generic;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Reduce mallas para móvil por agrupamiento de vértices (vertex clustering): se cuantiza la posición en una
    /// rejilla y los vértices de una celda se funden en su promedio; se descartan los triángulos que se degeneran.
    /// La clave incluye la UV cuantizada para no fundir a través de costuras de textura. Se ajusta la celda hasta
    /// quedar por debajo del objetivo de triángulos (POLISH_PASS 3.9: rocas &lt;= 3k).
    /// </summary>
    public static class MeshDecimator
    {
        public static Mesh Decimate(Mesh src, int maxTriangles, string name)
        {
            var v = src.vertices;
            var n = src.normals;
            var uv = src.uv;
            var tris = src.triangles;
            if (tris.Length / 3 <= maxTriangles) return Copy(src, name);
            var b = src.bounds;
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float cell = size / 60f;
            Mesh best = null;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                var m = Cluster(v, n, uv, tris, b.min, cell, name);
                best = m;
                if (m.triangles.Length / 3 <= maxTriangles) break;
                cell *= 1.22f;
            }
            return best;
        }

        static Mesh Cluster(Vector3[] v, Vector3[] n, Vector2[] uv, int[] tris, Vector3 origin, float cell, string name)
        {
            bool hasUv = uv != null && uv.Length == v.Length;
            bool hasN = n != null && n.Length == v.Length;
            var map = new Dictionary<(int, int, int, int, int), int>();
            var remap = new int[v.Length];
            var pos = new List<Vector3>();
            var nor = new List<Vector3>();
            var tex = new List<Vector2>();
            var count = new List<int>();
            const float uvCell = 0.08f;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 q = (v[i] - origin) / cell;
                var key = (Mathf.FloorToInt(q.x), Mathf.FloorToInt(q.y), Mathf.FloorToInt(q.z),
                           hasUv ? Mathf.FloorToInt(uv[i].x / uvCell) : 0, hasUv ? Mathf.FloorToInt(uv[i].y / uvCell) : 0);
                if (!map.TryGetValue(key, out int id))
                {
                    id = pos.Count;
                    map[key] = id;
                    pos.Add(Vector3.zero); nor.Add(Vector3.zero); tex.Add(Vector2.zero); count.Add(0);
                }
                remap[i] = id;
                pos[id] += v[i];
                if (hasN) nor[id] += n[i];
                if (hasUv) tex[id] += uv[i];
                count[id]++;
            }
            for (int i = 0; i < pos.Count; i++)
            {
                pos[i] /= count[i];
                tex[i] /= count[i];
                nor[i] = nor[i].sqrMagnitude > 0f ? nor[i].normalized : Vector3.up;
            }
            var outTris = new List<int>(tris.Length);
            var seen = new HashSet<(int, int, int)>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = remap[tris[t]], c = remap[tris[t + 1]], d = remap[tris[t + 2]];
                if (a == c || c == d || a == d) continue;
                // Un mismo triángulo puede quedar repetido: solo una vez.
                var k = a < c ? (a < d ? (a, c, d) : (d, a, c)) : (c < d ? (c, d, a) : (d, a, c));
                if (!seen.Add(k)) continue;
                outTris.Add(a); outTris.Add(c); outTris.Add(d);
            }
            var m = new Mesh { name = name };
            if (pos.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(pos);
            if (hasUv) m.SetUVs(0, tex);
            m.SetTriangles(outTris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        static Mesh Copy(Mesh src, string name)
        {
            var m = Object.Instantiate(src);
            m.name = name;
            return m;
        }
    }
}
