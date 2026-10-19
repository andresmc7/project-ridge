using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Geometría "chunky" para The Hollow (formas redondeadas y legibles, tipo diorama) que lleva texturas
    /// fotográficas encima. Rocas (superelipsoides con ruido), coníferas de copas macizas, arbustos de burbujas,
    /// troncos y cajas con UV en metros. También genera texturas procedurales tileables (agujas, hojas, piel).
    /// </summary>
    public static class ChunkyMeshGenerator
    {
        public const string OutMeshes = "Assets/_Project/Art/Meshes/Chunky";
        public const string OutTextures = "Assets/_Project/Art/Textures/Chunky";

        // ------------------------------------------------------------------ Ruido

        static float Noise3(Vector3 p)
        {
            return (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y + 31.7f, p.z) + Mathf.PerlinNoise(p.z + 71.3f, p.x)) / 3f;
        }

        static float Fbm(Vector3 p, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * (Noise3(p) * 2f - 1f);
                norm += amp;
                p *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------------ Esfera base

        static void Icosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>(tris.Count * 4);
                var vv = verts;
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    vv.Add(((vv[a] + vv[b]) * 0.5f).normalized);
                    cache[key] = vv.Count - 1;
                    return vv.Count - 1;
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
        }

        /// <summary>Desuelda los triángulos y asigna UV por proyección de caja (el eje dominante de cada cara), en metros.</summary>
        static Mesh BoxProjected(string name, List<Vector3> verts, List<Vector3> normals, List<int> tris, float uvScale)
        {
            var v = new List<Vector3>(tris.Count);
            var n = new List<Vector3>(tris.Count);
            var uv = new List<Vector2>(tris.Count);
            var t = new int[tris.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                fn = new Vector3(Mathf.Abs(fn.x), Mathf.Abs(fn.y), Mathf.Abs(fn.z));
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = verts[tris[i + k]];
                    Vector2 q = fn.y >= fn.x && fn.y >= fn.z ? new Vector2(p.x, p.z) : fn.x >= fn.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                    t[i + k] = v.Count;
                    v.Add(p);
                    n.Add(normals[tris[i + k]]);
                    uv.Add(q * uvScale);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static List<Vector3> SmoothNormals(List<Vector3> verts, List<int> tris)
        {
            var n = new Vector3[verts.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                n[tris[i]] += fn; n[tris[i + 1]] += fn; n[tris[i + 2]] += fn;
            }
            var list = new List<Vector3>(n.Length);
            foreach (var x in n) list.Add(x.normalized);
            return list;
        }

        // ------------------------------------------------------------------ Rocas

        /// <param name="blockiness">0 = canto rodado, 1 = bloque de acantilado con bordes redondeados.</param>
        public static Mesh Rock(string name, int seed, Vector3 size, float blockiness, float roughness)
        {
            Icosphere(2, out var verts, out var tris);
            var rng = new System.Random(seed);
            Vector3 off = new Vector3((float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f);
            float pow = Mathf.Lerp(2f, 6f, blockiness);
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 d = verts[i];
                // Superelipsoide: normalizar con norma Lp (p alto = más cúbico).
                float lp = Mathf.Pow(Mathf.Pow(Mathf.Abs(d.x), pow) + Mathf.Pow(Mathf.Abs(d.y), pow) + Mathf.Pow(Mathf.Abs(d.z), pow), 1f / pow);
                Vector3 p = d / lp;
                float bump = Fbm(d * 1.7f + off, 3) * roughness + Fbm(d * 5.5f + off, 2) * roughness * 0.25f;
                p *= 1f + bump;
                p = Vector3.Scale(p, size * 0.5f);
                // Base plana apenas enterrada, para que asiente en el suelo.
                float floor = -size.y * 0.32f;
                if (p.y < floor) p.y = floor + (p.y - floor) * 0.15f;
                verts[i] = p + Vector3.up * size.y * 0.3f;
            }
            return BoxProjected(name, verts, SmoothNormals(verts, tris), tris, 0.35f);
        }

        // ------------------------------------------------------------------ Coníferas macizas

        public struct TreeShape
        {
            public float height;
            public float radius;       // radio de la copa más baja
            public int tiers;
            public float trunkRadius;
            public int lobes;          // ondas del borde de cada copa
        }

        /// <summary>Tronco (submalla 0) + copas apiladas con borde abultado (submalla 1). ~450 triángulos.</summary>
        public static Mesh Conifer(string name, int seed, TreeShape s)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var trunk = new List<int>();
            var crown = new List<int>();

            // Tronco
            const int ts = 8;
            float trunkTop = s.height * 0.45f;
            for (int i = 0; i <= ts; i++)
            {
                float a = i / (float)ts * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * s.trunkRadius * 1.25f + Vector3.down * 0.2f); uv.Add(new Vector2(i / (float)ts * 1.5f, 0f));
                v.Add(d * s.trunkRadius * 0.7f + Vector3.up * trunkTop); uv.Add(new Vector2(i / (float)ts * 1.5f, trunkTop * 0.6f));
            }
            for (int i = 0; i < ts; i++)
            {
                int b = i * 2;
                trunk.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
            }

            // Copas: anillos (bajo-dentro, borde, hombro) + punta. Cada copa se superpone a la siguiente.
            const int seg = 14;
            float crownStart = s.height * 0.22f;
            float span = s.height - crownStart;
            for (int k = 0; k < s.tiers; k++)
            {
                float f = k / (float)s.tiers;
                float yb = crownStart + span * f * 0.82f;
                float th = span * (1.05f / s.tiers) * (k == s.tiers - 1 ? 1.5f : 1.25f);
                float rad = s.radius * Mathf.Lerp(1f, 0.38f, f) * R(0.92f, 1.08f);
                float phase = R(0f, 6.28f);
                int ringStart = v.Count;
                var rings = new[]
                {
                    new Vector2(0.25f, 0.18f),   // bajo, cerca del tronco
                    new Vector2(0.82f, -0.02f),  // borde inferior
                    new Vector2(1.00f, 0.10f),   // borde (abultado)
                    new Vector2(0.78f, 0.38f),   // hombro
                    new Vector2(0.40f, 0.72f),   // cerca de la punta
                };
                for (int r = 0; r < rings.Length; r++)
                {
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = i / (float)seg * Mathf.PI * 2f;
                        float lobe = 1f + 0.11f * Mathf.Sin(a * s.lobes + phase) + 0.05f * Mathf.Sin(a * (s.lobes * 2 + 1) + phase * 1.7f);
                        float rr = rad * rings[r].x * (r >= 1 && r <= 2 ? lobe : 1f + (lobe - 1f) * 0.5f);
                        float droop = r == 1 || r == 2 ? -0.12f * th * (1f - lobe) : 0f;
                        v.Add(new Vector3(Mathf.Cos(a) * rr, yb + rings[r].y * th + droop, Mathf.Sin(a) * rr));
                        uv.Add(new Vector2(i / (float)seg * rad * 2.2f, (yb + rings[r].y * th) * 0.7f));
                    }
                }
                int apex = v.Count;
                v.Add(new Vector3(R(-0.05f, 0.05f), yb + th, R(-0.05f, 0.05f)));
                uv.Add(new Vector2(0.5f, (yb + th) * 0.7f));
                int row = seg + 1;
                for (int r = 0; r < rings.Length - 1; r++)
                    for (int i = 0; i < seg; i++)
                    {
                        int a0 = ringStart + r * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                        crown.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                    }
                int last = ringStart + (rings.Length - 1) * row;
                for (int i = 0; i < seg; i++) crown.AddRange(new[] { last + i, apex, last + i + 1 });
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trunk, 0);
            mesh.SetTriangles(crown, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ Arbustos y copas redondas

        /// <summary>Racimo de burbujas deformadas (arbusto o copa de árbol redondo). Una sola submalla.</summary>
        public static Mesh Blob(string name, int seed, float radius, int puffs, float height)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var allV = new List<Vector3>();
            var allT = new List<int>();
            Vector3 off = new Vector3(R(0, 50), R(0, 50), R(0, 50));
            for (int p = 0; p < puffs; p++)
            {
                Icosphere(1, out var verts, out var tris);
                float pr = radius * (p == 0 ? 0.75f : R(0.45f, 0.62f));
                float ang = R(0f, Mathf.PI * 2f);
                float dist = p == 0 ? 0f : radius * R(0.35f, 0.6f);
                Vector3 c = new Vector3(Mathf.Cos(ang) * dist, height * (p == 0 ? 0.55f : R(0.35f, 0.75f)), Mathf.Sin(ang) * dist);
                int baseIdx = allV.Count;
                foreach (var d in verts)
                {
                    Vector3 q = d * pr * (1f + 0.18f * Fbm(d * 2.2f + off + Vector3.one * p, 2));
                    q.y *= height / (radius * 1.6f);
                    allV.Add(c + q);
                }
                foreach (var t in tris) allT.Add(baseIdx + t);
            }
            return BoxProjected(name, allV, SmoothNormals(allV, allT), allT, 0.9f);
        }

        // ------------------------------------------------------------------ Troncos y cajas

        /// <summary>Cilindro a lo largo de Z con tapas. Submalla 0 corteza, 1 tapas (madera cortada).</summary>
        public static Mesh Log(string name, float length, float radius, int sides = 10)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var side = new List<int>();
            var caps = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float wob = 1f + 0.06f * Mathf.Sin(a * 3f + 1.3f);
                var d = new Vector3(Mathf.Cos(a) * wob, Mathf.Sin(a) * wob, 0f) * radius;
                v.Add(d + Vector3.back * length * 0.5f); uv.Add(new Vector2(i / (float)sides * radius * 6.28f, 0f));
                v.Add(d + Vector3.forward * length * 0.5f); uv.Add(new Vector2(i / (float)sides * radius * 6.28f, length));
            }
            for (int i = 0; i < sides; i++)
            {
                int b = i * 2;
                side.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            for (int end = 0; end < 2; end++)
            {
                float z = end == 0 ? -length * 0.5f : length * 0.5f;
                int center = v.Count;
                v.Add(new Vector3(0f, 0f, z)); uv.Add(new Vector2(0.5f, 0.5f));
                int start = v.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float wob = 1f + 0.06f * Mathf.Sin(a * 3f + 1.3f);
                    v.Add(new Vector3(Mathf.Cos(a) * wob * radius, Mathf.Sin(a) * wob * radius, z));
                    uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }
                for (int i = 0; i < sides; i++)
                {
                    if (end == 0) caps.AddRange(new[] { center, start + i, start + i + 1 });
                    else caps.AddRange(new[] { center, start + i + 1, start + i });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(side, 0);
            mesh.SetTriangles(caps, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Caja centrada con UV en metros (la textura no se estira al escalar tablas o paredes).</summary>
        public static Mesh Box(string name, Vector3 size, float uvPerMeter = 1f)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            Vector3 h = size * 0.5f;
            void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sw)
            {
                int b = v.Count;
                Vector3 c = Vector3.Scale(normal, h);
                Vector3 hu = u * (su * 0.5f), hw = w * (sw * 0.5f);
                v.Add(c - hu - hw); v.Add(c + hu - hw); v.Add(c + hu + hw); v.Add(c - hu + hw);
                for (int i = 0; i < 4; i++) n.Add(normal);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(su, 0f) * uvPerMeter); uv.Add(new Vector2(su, sw) * uvPerMeter); uv.Add(new Vector2(0f, sw) * uvPerMeter);
                t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.left, Vector3.forward, size.x, size.z);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ Pasto

        /// <summary>Mata de pasto: hojas curvas de 3 segmentos que se afinan. UV: u a lo ancho, v de la base a la punta.</summary>
        public static Mesh GrassTuft(string name, int seed, float height, int blades, float spread)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int b = 0; b < blades; b++)
            {
                float ang = R(0f, Mathf.PI * 2f);
                Vector3 root = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * R(0f, spread);
                Vector3 lean = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * R(0.15f, 0.45f) * height;
                Vector3 side = new Vector3(-Mathf.Sin(ang + R(-0.6f, 0.6f)), 0f, Mathf.Cos(ang + R(-0.6f, 0.6f)));
                float h = height * R(0.6f, 1.1f);
                float w = R(0.06f, 0.1f);
                int start = v.Count;
                const int seg = 3;
                for (int s = 0; s <= seg; s++)
                {
                    float f = s / (float)seg;
                    Vector3 c = root + Vector3.up * h * f + lean * f * f;
                    float width = w * (1f - f * 0.9f);
                    v.Add(c - side * width); uv.Add(new Vector2(0f, f));
                    v.Add(c + side * width); uv.Add(new Vector2(1f, f));
                }
                for (int s = 0; s < seg; s++)
                {
                    int i = start + s * 2;
                    t.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            // UV2.x = altura sobre la base: el viento la usa también cuando la mata se combina con otras.
            var heightUv = new List<Vector2>(v.Count);
            foreach (var p in v) heightUv.Add(new Vector2(p.y, 0f));
            mesh.SetUVs(1, heightUv);
            // Normales hacia arriba: el pasto se ilumina parejo (como en los dioramas) y no parpadea por cara.
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            mesh.normals = n;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Textura del pasto: base oscura, punta clara y amarillenta, vetas finas a lo largo.</summary>
        public static Texture2D GrassTexture(int seed)
        {
            const int w = 64, h = 256;
            var rng = new System.Random(seed);
            var px = new Color[w * h];
            var baseC = new Color(0.04f, 0.06f, 0.03f);
            var midC = new Color(0.22f, 0.3f, 0.1f);
            var tipC = new Color(0.55f, 0.52f, 0.26f);
            for (int x = 0; x < w; x++)
            {
                float vein = 0.85f + 0.3f * (float)rng.NextDouble();
                for (int y = 0; y < h; y++)
                {
                    float f = y / (float)(h - 1);
                    Color c = f < 0.6f ? Color.Lerp(baseC, midC, f / 0.6f) : Color.Lerp(midC, tipC, (f - 0.6f) / 0.4f);
                    float edge = 1f - Mathf.Pow(Mathf.Abs(x / (float)(w - 1) * 2f - 1f), 3f) * 0.4f;
                    px[y * w + x] = c * vein * edge;
                }
            }
            PolyHavenMaterialBuilder.EnsureFolder(OutTextures);
            string path = OutTextures + "/Grass_albedo.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Torno (miembros, cuerpos, cabezas)

        /// <summary>
        /// Superficie de revolución alrededor de Y. profile: (radio, altura) de arriba hacia abajo; los extremos con
        /// radio 0 cierran la forma. crossSection escala X/Z (cuerpos ovalados). bulge desplaza Z según la altura
        /// (p. ej. pecho o barriga). UV: u alrededor (en metros), v por altura.
        /// </summary>
        public static Mesh Lathe(string name, Vector2[] profile, int segments, Vector2 crossSection, System.Func<float, float> bulge = null)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int row = segments + 1;
            float maxR = 0f;
            foreach (var p in profile) maxR = Mathf.Max(maxR, p.x);
            for (int r = 0; r < profile.Length; r++)
            {
                float rad = profile[r].x, y = profile[r].y;
                float bz = bulge != null ? bulge(y) : 0f;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * rad * crossSection.x, y, Mathf.Sin(a) * rad * crossSection.y + bz * Mathf.Max(0f, Mathf.Sin(a))));
                    uv.Add(new Vector2(i / (float)segments * maxR * 6.28f, y));
                }
            }
            for (int r = 0; r < profile.Length - 1; r++)
                for (int i = 0; i < segments; i++)
                {
                    int a0 = r * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            // Costura: promediar las normales de la primera y última columna (mismo punto).
            var n = mesh.normals;
            for (int r = 0; r < profile.Length; r++)
            {
                int a = r * row, b = r * row + segments;
                Vector3 avg = (n[a] + n[b]).normalized;
                n[a] = avg; n[b] = avg;
            }
            mesh.normals = n;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Miembro redondeado que cuelga del pivote hacia -Y: radio r0 arriba, r1 abajo, extremos semiesféricos.</summary>
        public static Mesh Limb(string name, float length, float r0, float r1, int segments = 10, Vector2? cross = null)
        {
            var prof = new List<Vector2>();
            const int cap = 4;
            for (int i = 0; i <= cap; i++)
            {
                float a = i / (float)cap * Mathf.PI * 0.5f;
                prof.Add(new Vector2(Mathf.Sin(a) * r0, Mathf.Cos(a) * r0 * 0.8f));
            }
            for (int i = 1; i < 4; i++)
            {
                float f = i / 4f;
                prof.Add(new Vector2(Mathf.Lerp(r0, r1, f) * (1f + 0.06f * Mathf.Sin(f * Mathf.PI)), -length * f));
            }
            for (int i = 0; i <= cap; i++)
            {
                float a = i / (float)cap * Mathf.PI * 0.5f;
                prof.Add(new Vector2(Mathf.Cos(a) * r1, -length - Mathf.Sin(a) * r1 * 0.8f));
            }
            return Lathe(name, prof.ToArray(), segments, cross ?? Vector2.one);
        }

        /// <summary>Elipsoide centrado en el origen (cabezas, manos, torsos).</summary>
        public static Mesh Ellipsoid(string name, Vector3 size, int segments = 12, int rings = 8)
        {
            var prof = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = i / (float)rings * Mathf.PI;
                prof[i] = new Vector2(Mathf.Sin(a) * 0.5f * size.x, Mathf.Cos(a) * 0.5f * size.y);
            }
            return Lathe(name, prof, segments, new Vector2(1f, size.z / size.x));
        }

        // ------------------------------------------------------------------ Guardado

        public static Mesh Save(Mesh mesh)
        {
            PolyHavenMaterialBuilder.EnsureFolder(OutMeshes);
            string path = OutMeshes + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ------------------------------------------------------------------ Texturas procedurales tileables

        public enum Pattern { Needles, Leaves, Skin }

        /// <summary>Genera albedo + normal tileables de 512 px. Devuelve (albedo, normal).</summary>
        public static (Texture2D, Texture2D) PatternTextures(Pattern pattern, int seed)
        {
            const int size = 512;
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var col = new Color[size * size];
            var height = new float[size * size];

            Color baseCol, lightCol;
            switch (pattern)
            {
                case Pattern.Needles: baseCol = new Color(0.05f, 0.09f, 0.06f); lightCol = new Color(0.27f, 0.38f, 0.2f); break;
                case Pattern.Leaves: baseCol = new Color(0.055f, 0.085f, 0.04f); lightCol = new Color(0.32f, 0.4f, 0.17f); break;
                default: baseCol = new Color(0.05f, 0.055f, 0.06f); lightCol = new Color(0.22f, 0.2f, 0.21f); break;
            }
            for (int i = 0; i < col.Length; i++) col[i] = baseCol;

            void Plot(int x, int y, Color c, float h, float alpha)
            {
                x = ((x % size) + size) % size;
                y = ((y % size) + size) % size;
                int i = y * size + x;
                if (h < height[i]) return;
                height[i] = h;
                col[i] = Color.Lerp(col[i], c, alpha);
            }

            if (pattern == Pattern.Needles)
            {
                for (int s = 0; s < 9000; s++)
                {
                    float x = R(0, size), y = R(0, size);
                    float ang = R(-0.6f, 0.6f) + (R(0f, 1f) < 0.5f ? -1.2f : 1.2f); // agujas hacia afuera y abajo
                    float len = R(7f, 16f);
                    float lum = R(0.15f, 1f);
                    float h0 = R(0.2f, 1f);
                    Color c = Color.Lerp(baseCol, lightCol, lum) * R(0.85f, 1.1f);
                    for (float d = 0; d < len; d += 0.6f)
                    {
                        float hx = x + Mathf.Sin(ang) * d, hy = y - Mathf.Cos(ang) * d;
                        Plot((int)hx, (int)hy, c, h0 - d / len * 0.3f, 0.9f);
                        Plot((int)hx + 1, (int)hy, c * 0.85f, h0 - d / len * 0.3f - 0.02f, 0.5f);
                    }
                }
            }
            else if (pattern == Pattern.Leaves)
            {
                for (int s = 0; s < 2600; s++)
                {
                    float x = R(0, size), y = R(0, size);
                    float ang = R(0f, Mathf.PI);
                    float len = R(9f, 18f), wid = len * R(0.35f, 0.5f);
                    float lum = R(0.1f, 1f);
                    float h0 = R(0.1f, 1f);
                    Color c = Color.Lerp(baseCol, lightCol, lum);
                    float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                    for (float u = -len; u <= len; u += 0.7f)
                        for (float w = -wid; w <= wid; w += 0.7f)
                        {
                            float e = (u * u) / (len * len) + (w * w) / (wid * wid);
                            if (e > 1f) continue;
                            float shade = 1f - 0.35f * e + (Mathf.Abs(w) < 0.8f ? -0.15f : 0f); // nervadura
                            Plot((int)(x + u * ca - w * sa), (int)(y + u * sa + w * ca), c * shade, h0 + (1f - e) * 0.2f, 1f);
                        }
                }
            }
            else
            {
                // Piel: manchas suaves + venas finas (ruido "ridged") tileable vía coordenadas toroidales.
                float ox = R(0, 100), oy = R(0, 100);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float a = x / (float)size * Mathf.PI * 2f, b = y / (float)size * Mathf.PI * 2f;
                        Vector3 p = new Vector3(Mathf.Cos(a) * 2f + ox, Mathf.Sin(a) * 2f + Mathf.Cos(b) * 2f, Mathf.Sin(b) * 2f + oy);
                        float blot = Fbm(p * 1.2f, 4) * 0.5f + 0.5f;
                        float vein = 1f - Mathf.Abs(Fbm(p * 2.4f + Vector3.one * 7f, 3));
                        vein = Mathf.Pow(Mathf.Clamp01(vein), 14f);
                        float pores = Fbm(p * 14f, 2) * 0.5f + 0.5f;
                        int i = y * size + x;
                        Color c = Color.Lerp(baseCol, lightCol, blot * 0.6f) * (0.85f + pores * 0.25f);
                        c = Color.Lerp(c, new Color(0.22f, 0.04f, 0.05f), vein * 0.6f);
                        col[i] = c;
                        height[i] = blot * 0.4f + pores * 0.2f + vein * 0.5f;
                    }
            }

            // Normal desde la altura (tileable).
            var nrm = new Color[size * size];
            float strength = pattern == Pattern.Skin ? 3f : 5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float hl = height[y * size + (x + size - 1) % size], hr = height[y * size + (x + 1) % size];
                    float hd = height[((y + size - 1) % size) * size + x], hu = height[((y + 1) % size) * size + x];
                    Vector3 n = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                    nrm[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }

            string tag = pattern.ToString();
            var albedo = WriteTexture(tag + "_albedo", col, size, false);
            var normal = WriteTexture(tag + "_normal", nrm, size, true);
            return (albedo, normal);
        }

        static Texture2D WriteTexture(string name, Color[] px, int size, bool normal)
        {
            PolyHavenMaterialBuilder.EnsureFolder(OutTextures);
            string path = OutTextures + "/" + name + ".png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, normal);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.maxTextureSize = 512;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
