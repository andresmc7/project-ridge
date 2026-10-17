using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Genera pinos/abetos low-poly para móvil: tronco cónico + "faldas" de agujas caídas con borde irregular.
    /// ~250–400 triángulos por árbol. Las variantes salen de una semilla. Las texturas de agujas son procedurales.
    /// </summary>
    public static class ConiferMeshGenerator
    {
        public const string OutMeshes = "Assets/_Project/Art/Meshes/Conifers";
        public const string OutTextures = "Assets/_Project/Art/Textures/Conifers";

        public struct Settings
        {
            public float height;         // altura total (m)
            public float trunkRadius;    // radio del tronco en la base
            public float crownBase;      // altura donde empieza la copa (fracción de la altura)
            public float crownRadius;    // radio de la falda más baja
            public int tiers;            // número de faldas
            public int segments;         // lados de cada falda
            public float droop;          // cuánto caen las puntas (m)
            public float jitter;         // irregularidad del borde (fracción)
            public bool dead;            // árbol seco: solo tronco y pocas ramas
        }

        /// <param name="sprites">Opcional: rectángulos UV de ramitas en un atlas (x=uMin, y=vBase, z=uMax, w=vPunta).
        /// Cada rama usa uno al azar. Sin atlas, las UV cubren 0..1 (textura procedural).</param>
        public static Mesh Build(string name, Settings s, int seed, Vector4[] sprites = null)
        {
            var rng = new System.Random(seed);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var trunkTris = new List<int>();
            var foliageTris = new List<int>();

            // Tronco: cono de 7 lados con leve inclinación.
            const int trunkSides = 7;
            Vector3 lean = new Vector3(Rand(-0.04f, 0.04f), 0f, Rand(-0.04f, 0.04f)) * s.height;
            float trunkTop = s.dead ? s.height : s.height * 0.92f;
            for (int i = 0; i <= trunkSides; i++)
            {
                float a = i / (float)trunkSides * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(dir * s.trunkRadius); normals.Add(dir); uvs.Add(new Vector2(i / (float)trunkSides * 2f, 0f));
                verts.Add(dir * s.trunkRadius * 0.15f + Vector3.up * trunkTop + lean); normals.Add(dir); uvs.Add(new Vector2(i / (float)trunkSides * 2f, trunkTop * 0.5f));
            }
            for (int i = 0; i < trunkSides; i++)
            {
                int b = i * 2;
                trunkTris.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
            }

            if (s.dead)
            {
                // Árbol muerto: 4–6 ramas cortas como prismas delgados.
                int branches = rng.Next(4, 7);
                for (int k = 0; k < branches; k++)
                {
                    float y = Rand(0.35f, 0.85f) * s.height;
                    float ang = Rand(0f, Mathf.PI * 2f);
                    var dir = new Vector3(Mathf.Cos(ang), Rand(-0.2f, 0.35f), Mathf.Sin(ang)).normalized;
                    float len = Rand(0.6f, 1.6f) * (1f - y / s.height + 0.3f);
                    Vector3 root = Vector3.up * y + lean * (y / s.height);
                    AddPrism(verts, normals, uvs, trunkTris, root, root + dir * len, s.trunkRadius * 0.25f);
                }
            }
            else
            {
                // Copa: niveles de ramas caídas. Cada rama es una tarjeta de 3x3 vértices (base → medio → punta,
                // borde izq → nervio → borde der) con textura de agujas recortada por alfa.
                float crownStart = s.height * s.crownBase;
                Vector3 crownCenter = Vector3.up * Mathf.Lerp(crownStart, s.height, 0.4f) + lean * 0.5f;
                for (int t = 0; t < s.tiers; t++)
                {
                    float f = (t + Rand(-0.3f, 0.3f)) / Mathf.Max(1, s.tiers - 1); // 0 = abajo, 1 = punta
                    f = Mathf.Clamp01(f);
                    float y = Mathf.Lerp(crownStart, s.height - 0.35f, Mathf.Pow(f, 0.9f));
                    float radius = Mathf.Lerp(s.crownRadius, s.crownRadius * 0.12f, Mathf.Pow(f, 0.85f));
                    Vector3 trunkPoint = Vector3.up * y + lean * (y / s.height);
                    float twist = Rand(0f, Mathf.PI * 2f);
                    for (int b = 0; b < s.segments; b++)
                    {
                        float a = twist + (b + Rand(-0.3f, 0.3f)) / s.segments * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var side = new Vector3(-dir.z, 0f, dir.x);
                        float len = radius * Rand(0.7f, 1.15f) * (1f + s.jitter * Rand(-1f, 1f));
                        float drop = len * s.droop * Rand(0.6f, 1.2f);
                        float tipWidth = len * (sprites != null ? Rand(0.75f, 0.95f) : Rand(0.5f, 0.68f));
                        float tilt = Rand(-0.25f, 0.25f); // giro de la tarjeta sobre su eje
                        Vector4 rect = sprites != null ? sprites[rng.Next(sprites.Length)] : new Vector4(0f, 0f, 1f, 1f);

                        int start = verts.Count;
                        for (int row = 0; row < 3; row++)
                        {
                            float tr = row / 2f;
                            float width = Mathf.Lerp(0.12f, tipWidth, Mathf.Pow(tr, 0.7f));
                            Vector3 center = trunkPoint + dir * (len * tr) + Vector3.down * (drop * tr * tr) + Vector3.up * (0.12f * len * tr * (1f - tr));
                            for (int col = 0; col < 3; col++)
                            {
                                float c = col - 1f; // -1, 0, 1
                                // Bordes caídos hacia abajo (sección en "V" invertida) y leve giro de la tarjeta.
                                Vector3 p = center + side * (c * width * 0.5f) + Vector3.up * (c * tilt * width * 0.5f)
                                          + Vector3.down * (Mathf.Abs(c) * width * 0.22f);
                                verts.Add(p);
                                Vector3 sph = (p - crownCenter).normalized;
                                normals.Add((sph * 0.65f + Vector3.up * 0.55f).normalized);
                                uvs.Add(new Vector2(Mathf.Lerp(rect.x, rect.z, col / 2f), Mathf.Lerp(rect.y, rect.w, tr)));
                            }
                        }
                        for (int row = 0; row < 2; row++)
                            for (int col = 0; col < 2; col++)
                            {
                                int i0 = start + row * 3 + col;
                                int i1 = i0 + 1, i2 = i0 + 3, i3 = i0 + 4;
                                foliageTris.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
                            }
                    }
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trunkTris, 0);
            mesh.SetTriangles(foliageTris, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddPrism(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris, Vector3 a, Vector3 b, float r)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(axis, Vector3.up).normalized : Vector3.right;
            Vector3 up = Vector3.Cross(side, axis);
            int start = v.Count;
            for (int i = 0; i < 3; i++)
            {
                float ang = i / 3f * Mathf.PI * 2f;
                Vector3 off = (side * Mathf.Cos(ang) + up * Mathf.Sin(ang));
                v.Add(a + off * r); n.Add(off); uv.Add(new Vector2(i / 3f, 0f));
                v.Add(b + off * r * 0.3f); n.Add(off); uv.Add(new Vector2(i / 3f, 1f));
            }
            for (int i = 0; i < 3; i++)
            {
                int i0 = start + i * 2, i1 = start + ((i + 1) % 3) * 2;
                tris.AddRange(new[] { i0, i0 + 1, i1, i1, i0 + 1, i1 + 1 });
            }
        }

        public static Mesh SaveMesh(Mesh mesh)
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

        /// <summary>
        /// Textura de rama procedural: u = a lo ancho (0..1, nervio en 0.5), v = de la base (0) a la punta (1).
        /// Las agujas salen en diagonal desde el nervio; el alfa recorta un borde dentado de agujas.
        /// </summary>
        public static (Texture2D albedo, Texture2D normal) BuildNeedleTextures(int size, int seed)
        {
            PolyHavenMaterialBuilder.EnsureFolder(OutTextures);
            var rng = new System.Random(seed);
            var height = new float[size * size];
            var alpha = new float[size * size];
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float d = Mathf.Abs(u - 0.5f) * 2f;                       // 0 en el nervio, 1 en el borde
                    float n1 = Mathf.PerlinNoise(u * 18f + ox, v * 18f + oy);
                    // Agujas finas en diagonal desde el tallo; más cortas hacia la base.
                    float strand = Mathf.Repeat(v * 46f - d * 7f + n1 * 0.6f, 1f);
                    bool needle = strand < 0.42f;
                    float reach = Mathf.Lerp(0.5f, 1f, Mathf.Sqrt(v)) * (0.85f + 0.15f * n1);
                    bool twig = d < 0.12f + Mathf.PerlinNoise(v * 9f + ox, 3f) * 0.06f;
                    bool dense = d < 0.38f * reach && n1 > 0.35f;                // relleno irregular cerca del tallo
                    bool tip = v > 0.93f + Mathf.PerlinNoise(u * 20f + oy, 7f) * 0.07f;
                    bool baseGap = v < 0.05f && d > 0.25f;
                    bool on = (twig || dense || (needle && d < reach)) && !tip && !baseGap;
                    alpha[y * size + x] = on ? 1f : 0f;
                    float fine = Mathf.PerlinNoise(u * 120f + oy, v * 50f + ox);
                    height[y * size + x] = Mathf.Clamp01((needle ? 0.25f : 0f) + fine * 0.45f + (1f - d) * 0.3f);
                }

            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            var normal = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var dark = new Color(0.035f, 0.07f, 0.035f);
            var light = new Color(0.09f, 0.15f, 0.07f);
            var tipTint = new Color(0.13f, 0.19f, 0.09f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float h = height[y * size + x];
                    float v = y / (float)size; // 0 = base (junto al tronco), 1 = punta
                    Color c = Color.Lerp(dark, light, h);
                    c = Color.Lerp(c, tipTint, v * 0.5f * h);                 // puntas más claras
                    c *= Mathf.Lerp(0.55f, 1f, v);                              // más oscuro hacia el tronco
                    c.a = alpha[y * size + x];
                    albedo.SetPixel(x, y, c);

                    float hx = height[y * size + (x + 1) % size] - height[y * size + (x - 1 + size) % size];
                    float hy = height[((y + 1) % size) * size + x] - height[((y - 1 + size) % size) * size + x];
                    var n = new Vector3(-hx * 1.2f, -hy * 1.2f, 1f).normalized;
                    normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            albedo.Apply();
            normal.Apply();

            string aPath = OutTextures + "/Needles_albedo.png";
            string nPath = OutTextures + "/Needles_normal.png";
            File.WriteAllBytes(aPath, albedo.EncodeToPNG());
            File.WriteAllBytes(nPath, normal.EncodeToPNG());
            Object.DestroyImmediate(albedo);
            Object.DestroyImmediate(normal);
            AssetDatabase.ImportAsset(aPath);
            AssetDatabase.ImportAsset(nPath);
            var aImp = (TextureImporter)AssetImporter.GetAtPath(aPath);
            aImp.alphaIsTransparency = true;
            aImp.mipMapsPreserveCoverage = true; // las agujas no desaparecen a distancia
            aImp.alphaTestReferenceValue = 0.5f;
            aImp.SaveAndReimport();
            var nImp = (TextureImporter)AssetImporter.GetAtPath(nPath);
            nImp.textureType = TextureImporterType.NormalMap;
            nImp.SaveAndReimport();
            return (AssetDatabase.LoadAssetAtPath<Texture2D>(aPath), AssetDatabase.LoadAssetAtPath<Texture2D>(nPath));
        }
    }
}
