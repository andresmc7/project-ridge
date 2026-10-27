using System.Collections.Generic;
using System.IO;
using ProjectRidge.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// POLISH_PASS P4 (3.9): el bosque de producto.
    /// - Coníferas con tronco visible y ramas en tarjetas (atlas de fir_tree_01), copa verde casi negro, con LOD de
    ///   impostor (dos tarjetas cruzadas horneadas) a más de ~30 m; árboles muertos y tocones.
    /// - Rocas Poly Haven (rock_moss_set_01/02) reducidas a &lt;= 3k triángulos, con musgo arriba por normal.
    /// - Vegetación escondite por mapa de densidad (ruido + rasgos del terreno + escondites de diseño deformados):
    ///   masas irregulares con bordes que se difuminan, nunca círculos. El escondite se calcula muestreando el mapa.
    /// - Ramas secas que crujen (+ruido) marcadas en claro, y un hito con luz por sala.
    /// </summary>
    public static partial class HollowBuilder
    {
        const string DensityPath = "Assets/_Project/Data/HideDensityMap.asset";
        const string EnvDir = "Assets/_Project/Prefabs/Environment";
        /// <summary>Lado de celda del mapa de densidad (m).</summary>
        const float DensityCell = 0.25f;
        /// <summary>Densidad por encima de la cual la vegetación esconde (debe coincidir con StealthConfig.hideDensity).</summary>
        const float HideThreshold = 0.5f;
        /// <summary>Distancia (m) fuera de lo caminable hasta la que el papá podría meterse (invisible walls incluidas).</summary>
        const float ReachableBand = 1.5f;

        // ------------------------------------------------------------------ Mapa de densidad

        /// <summary>Forma propia de cada escondite de diseño: orientación, alargamiento y semilla del borde.</summary>
        static (float angle, float aspect, int seed) PatchShape(int i) => (i * 1.37f % Mathf.PI, 1.3f + (i * 0.618f % 1f) * 0.6f, 40 + i * 11);

        static float DesignDensity(Vector2 p)
        {
            float best = 0f;
            for (int i = 0; i < HidePatches.Length; i++)
            {
                var h = HidePatches[i];
                Vector2 d = p - new Vector2(h.x, h.y);
                if (d.sqrMagnitude > h.z * h.z * 6f) continue;
                var (angle, aspect, seed) = PatchShape(i);
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                // Elipse de la misma área que el círculo original, con el borde ondulado por ruido.
                float lx = (d.x * c + d.y * s) / Mathf.Sqrt(aspect), lz = (-d.x * s + d.y * c) * Mathf.Sqrt(aspect);
                float dist = Mathf.Sqrt(lx * lx + lz * lz);
                float ang = Mathf.Atan2(lz, lx);
                float wobble = PeriodicFbm(Mathf.Cos(ang) * 0.5f + 0.5f + seed * 0.01f, Mathf.Sin(ang) * 0.5f + 0.5f, 3, 3, seed) - 0.5f;
                float r = h.z * (1.05f + wobble * 0.7f);
                float v = 1f - Smooth(0.62f * r, 1.18f * r, dist);
                if (v > best) best = v;
            }
            return best;
        }

        /// <summary>
        /// Densidad de vegetación escondite (0–1) en un punto. Más densa junto a rocas, bordes de caminos y salas,
        /// zonas bajas y orillas del arroyo; nunca sobre el sendero, el agua, el refugio o la carretera. Dentro de las
        /// salas, lo procedural no llega a esconder: ahí solo esconden los escondites de diseño.
        /// </summary>
        static float DensityAt(Terrain t, Vector3 w, List<Vector3> rocks)
        {
            var p = new Vector2(w.x, w.z);
            float design = DesignDensity(p);
            float walk = Walk(w);

            float fbm = PeriodicFbm((p.x - TerrainMin.x) / SizeX, (p.y - TerrainMin.y) / SizeX, 4, 18, 77);
            float nearRock = 0f;
            foreach (var r in rocks) { float d = new Vector2(r.x - p.x, r.z - p.y).magnitude; if (d < 2.6f) nearRock = Mathf.Max(nearRock, 1f - d / 2.6f); }
            float edge = 1f - Mathf.Clamp01(Mathf.Abs(walk - 1f) / 2.2f);          // franja del borde de salas y caminos
            float bank = Mathf.Abs(p.x - RavineX(p.y));
            float creek = bank > 2.4f && bank < 6f ? 1f - Mathf.Abs(bank - 4f) / 2f : 0f;  // orillas del arroyo
            float mask = Mathf.Max(Mathf.Max(nearRock, edge), creek);
            float proc = Smooth(0.42f, 0.72f, fbm) * mask;
            // Donde el papá puede llegar (salas, pasillos y su borde), lo procedural no esconde: solo los escondites de
            // diseño, que están medidos contra las patrullas. Más allá, el borde puede ser espeso (marco de la sala).
            if (walk < ReachableBand) proc = Mathf.Min(proc, HideThreshold - 0.08f);

            float d0 = Mathf.Max(design, proc);
            // Despejado: sendero, agua, refugio y carretera.
            float trail = Smooth(1.0f, 1.8f, DistanceToTrails(w));
            if (design < 0.5f) d0 *= trail;
            if (bank < 2.4f) d0 = 0f;
            if (Vector3.Distance(Flat(w), CabinCenter) < 4.6f) d0 = 0f;
            if (Mathf.Abs(p.x - RoadX) < RoadHalf + 0.6f) d0 = 0f;
            return Mathf.Clamp01(d0);
        }

        static HideDensityMap BuildDensityMap(Terrain t, List<Vector3> rocks)
        {
            var map = AssetDatabase.LoadAssetAtPath<HideDensityMap>(DensityPath);
            if (map == null) { map = ScriptableObject.CreateInstance<HideDensityMap>(); AssetDatabase.CreateAsset(map, DensityPath); }
            map.origin = TerrainMin;
            map.cellSize = DensityCell;
            map.width = Mathf.CeilToInt(SizeX / DensityCell);
            map.height = Mathf.CeilToInt(SizeZ / DensityCell);
            map.cells = new byte[map.width * map.height];
            for (int z = 0; z < map.height; z++)
                for (int x = 0; x < map.width; x++)
                {
                    var w = new Vector3(TerrainMin.x + (x + 0.5f) * DensityCell, 0f, TerrainMin.y + (z + 0.5f) * DensityCell);
                    map.cells[z * map.width + x] = (byte)Mathf.RoundToInt(DensityAt(t, w, rocks) * 255f);
                }
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            return map;
        }

        /// <summary>
        /// QA (P4): forma de cada escondite de diseño en el mapa (las celdas que esconden, conectadas con su centro y
        /// a menos de 3 radios). Se mide la variación de la distancia del centro al borde: 0 = círculo perfecto.
        /// Devuelve el informe, la menor irregularidad (la más "redonda") y cuántos escondites esconden de verdad.
        /// </summary>
        public static (string report, float minIrregularity, int masses) DensityReport()
        {
            var map = AssetDatabase.LoadAssetAtPath<HideDensityMap>(DensityPath);
            int w = map.width, h = map.height;
            float minCv = float.MaxValue;
            int masses = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var hp in HidePatches)
            {
                int cx0 = Mathf.FloorToInt((hp.x - map.origin.x) / map.cellSize), cz0 = Mathf.FloorToInt((hp.y - map.origin.y) / map.cellSize);
                float limit = hp.z * 3f / map.cellSize;
                var inside = new HashSet<int>();
                var stack = new Stack<int>();
                int start = cz0 * w + cx0;
                if (map.cells[start] < HideThreshold * 255f) { sb.AppendLine("patch " + hp + ": center does not hide"); continue; }
                stack.Push(start);
                inside.Add(start);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    int cx = c % w, cz = c / w;
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = cx + dx, nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                        if (new Vector2(nx - cx0, nz - cz0).magnitude > limit) continue;
                        int n = nz * w + nx;
                        if (inside.Contains(n) || map.cells[n] < HideThreshold * 255f) continue;
                        inside.Add(n);
                        stack.Push(n);
                    }
                }
                masses++;
                Vector2 centroid = Vector2.zero;
                foreach (int c in inside) centroid += new Vector2(c % w, c / w);
                centroid /= inside.Count;
                var radii = new List<float>();
                foreach (int c in inside)
                    if (!inside.Contains(c - 1) || !inside.Contains(c + 1) || !inside.Contains(c - w) || !inside.Contains(c + w))
                        radii.Add(Vector2.Distance(new Vector2(c % w, c / w), centroid));
                float mean = 0f; foreach (var r in radii) mean += r; mean /= radii.Count;
                float var = 0f; foreach (var r in radii) var += (r - mean) * (r - mean); var /= radii.Count;
                float cv = Mathf.Sqrt(var) / Mathf.Max(0.001f, mean);
                minCv = Mathf.Min(minCv, cv);
                float area = inside.Count * map.cellSize * map.cellSize;
                sb.AppendLine("patch (" + hp.x + ", " + hp.y + "): " + area.ToString("0.0") + " m², irregularity " + cv.ToString("0.00"));
            }
            return (sb.ToString(), minCv, masses);
        }

        /// <summary>Imagen cenital del mapa de densidad (gris = vegetación, blanco = esconde) para revisar formas.</summary>
        public static string DensityImage(string file)
        {
            var map = AssetDatabase.LoadAssetAtPath<HideDensityMap>(DensityPath);
            var tex = new Texture2D(map.width, map.height, TextureFormat.RGB24, false);
            for (int z = 0; z < map.height; z++)
                for (int x = 0; x < map.width; x++)
                {
                    float d = map.At(x, z);
                    var w = new Vector3(map.origin.x + (x + 0.5f) * map.cellSize, 0f, map.origin.y + (z + 0.5f) * map.cellSize);
                    float walk = Walk(w);
                    Color bg = walk < 0f ? new Color(0.18f, 0.16f, 0.12f) : new Color(0.06f, 0.07f, 0.06f);
                    Color c = d >= HideThreshold ? Color.Lerp(new Color(0.55f, 0.75f, 0.35f), Color.white, (d - HideThreshold) * 2f) : Color.Lerp(bg, new Color(0.25f, 0.4f, 0.2f), d / HideThreshold);
                    tex.SetPixel(x, z, c);
                }
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return file;
        }

        // ------------------------------------------------------------------ Vegetación escondite por densidad

        /// <summary>
        /// Planta pasto alto, helechos y matas según el mapa: más alto y apretado donde es más denso, bajo y suelto en
        /// el borde (el contorno se difumina). Las puntas del pasto escondite captan algo más de luz, cálida y sutil.
        /// </summary>
        static int PlantDensityVegetation(Transform root, Terrain t, HideDensityMap map, System.Random rng,
                                          System.Action<Mesh, Vector3, float, Material> tuft, Mesh[] tallMeshes, Material tallMat, Mesh fern, Material fernMat)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int count = 0;
            const float step = 0.6f;
            for (float x = map.origin.x; x < map.origin.x + map.width * map.cellSize; x += step)
                for (float z = map.origin.y; z < map.origin.y + map.height * map.cellSize; z += step)
                {
                    var p = new Vector3(x + R(-0.18f, 0.18f), 0f, z + R(-0.18f, 0.18f));
                    float d = map.Sample(p);
                    if (d < 0.18f || rng.NextDouble() > d * 1.15f) continue;
                    p.y = GroundY(t, p) - 0.03f;
                    float scale = Mathf.Lerp(0.45f, 1.08f, Mathf.Clamp01(d)) * R(0.85f, 1.12f);
                    if (rng.NextDouble() < 0.22) tuft(fern, p, scale * 1.35f, fernMat);
                    else tuft(tallMeshes[rng.Next(tallMeshes.Length)], p, scale, tallMat);
                    count++;
                }
            return count;
        }

        // ------------------------------------------------------------------ Coníferas de tarjetas

        /// <summary>Coníferas del nivel: tronco visible bajo la copa (la cámara a 42° tiene que ver troncos).</summary>
        static readonly ConiferMeshGenerator.Settings[] CardConifers =
        {
            new ConiferMeshGenerator.Settings { height = 8.0f, trunkRadius = 0.2f, crownBase = 0.5f, crownRadius = 2.0f, tiers = 10, segments = 7, droop = 0.36f, jitter = 0.18f },
            new ConiferMeshGenerator.Settings { height = 9.6f, trunkRadius = 0.24f, crownBase = 0.5f, crownRadius = 2.2f, tiers = 11, segments = 7, droop = 0.38f, jitter = 0.2f },
            new ConiferMeshGenerator.Settings { height = 7.0f, trunkRadius = 0.17f, crownBase = 0.45f, crownRadius = 1.8f, tiers = 11, segments = 8, droop = 0.3f, jitter = 0.15f },
            new ConiferMeshGenerator.Settings { height = 11.0f, trunkRadius = 0.27f, crownBase = 0.52f, crownRadius = 2.4f, tiers = 12, segments = 7, droop = 0.4f, jitter = 0.2f },
            new ConiferMeshGenerator.Settings { height = 7.5f, trunkRadius = 0.2f, dead = true },
        };

        /// <summary>Tamaño relativo en pantalla bajo el cual el árbol pasa a su impostor (~30 m con la cámara de juego).</summary>
        const float ImpostorScreenHeight = 0.45f;

        static Material needleMat, barkMat;

        static void ConiferMaterials()
        {
            if (needleMat != null && barkMat != null) return;
            barkMat = PolyHavenMaterialBuilder.BuildLitMaterial("pine_bark", PolyHavenMaterialBuilder.BuildTextureSet(PolyHavenMaterialBuilder.TexturesRoot + "/pine_bark", "pine_bark"), 0.8f);
            string twigFolder = PolyHavenMaterialBuilder.ModelsRoot + "/fir_tree_01/textures";
            var twigSet = PolyHavenMaterialBuilder.BuildTextureSet(twigFolder, "fir_tree_01_twig");
            twigSet.Albedo = PolyHavenMaterialBuilder.CombineAlpha("fir_tree_01_twig", twigSet.Albedo, PolyHavenMaterialBuilder.Find(twigFolder, "fir_tree_01_twig_alpha"));
            needleMat = PolyHavenMaterialBuilder.BuildLitMaterial("hollow_fir_twig", twigSet, 0.35f, doubleSided: true, alphaClip: twigSet.Albedo);
            // Verde oscuro casi negro: la luna a contraluz recorta el borde; sin azul saturado.
            needleMat.SetColor("_BaseColor", new Color(0.26f, 0.31f, 0.23f));
            EditorUtility.SetDirty(needleMat);
        }

        static GameObject CardConiferPrefab(int variant)
        {
            string name = "HollowFir_" + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            ConiferMaterials();
            var s = CardConifers[variant];
            var mesh = ConiferMeshGenerator.SaveMesh(ConiferMeshGenerator.Build("Hollow_Fir_" + variant, s, 600 + variant * 23, LookDevForestCornerBuilder.TwigSprites));
            var go = new GameObject(name);
            var lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(go.transform, false);
            lod0.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = lod0.AddComponent<MeshRenderer>();
            mr.sharedMaterials = s.dead ? new[] { barkMat, barkMat } : new[] { barkMat, needleMat };
            mr.receiveGI = ReceiveGI.LightProbes;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = s.trunkRadius + 0.08f;
            col.height = Mathf.Min(4f, s.height);
            col.center = Vector3.up * col.height * 0.5f;

            // Impostor: dos tarjetas cruzadas con el árbol horneado de lado.
            var lod1 = new GameObject("LOD1_Impostor");
            lod1.transform.SetParent(go.transform, false);
            var (imesh, imat) = BakeImpostor(name, lod0, s.height, s.dead ? 1.2f : s.crownRadius * 1.05f);
            lod1.AddComponent<MeshFilter>().sharedMesh = imesh;
            var ir = lod1.AddComponent<MeshRenderer>();
            ir.sharedMaterial = imat;
            ir.shadowCastingMode = ShadowCastingMode.Off;
            ir.receiveGI = ReceiveGI.LightProbes;
            var lods = go.AddComponent<LODGroup>();
            lods.SetLODs(new[] { new LOD(ImpostorScreenHeight, new Renderer[] { mr }), new LOD(0.01f, new Renderer[] { ir }) });
            lods.RecalculateBounds();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        /// <summary>Hornea un árbol visto de lado (ortográfica, fondo transparente) y arma dos tarjetas cruzadas.</summary>
        static (Mesh, Material) BakeImpostor(string name, GameObject source, float height, float radius)
        {
            const int texW = 128, texH = 256;
            var temp = Object.Instantiate(source);
            temp.transform.position = new Vector3(0f, 500f, 0f);
            var camGo = new GameObject("ImpostorCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = height * 0.52f;
            cam.aspect = texW / (float)texH;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.09f, 0f);
            cam.transform.position = new Vector3(0f, 500f + height * 0.5f, -20f);
            var lightGo = new GameObject("ImpostorLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(40f, 30f, 0f);
            var rt = RenderTexture.GetTemporary(texW, texH, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, texW, texH), 0, 0);
            // Lo que no es árbol queda transparente (el fondo tiene alfa 0).
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) if (px[i].a < 0.5f) px[i] = new Color(0f, 0f, 0f, 0f); else px[i].a = 1f;
            tex.SetPixels(px);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(temp);

            string texPath = PrefabDir + "/" + name + "_Impostor.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string matPath = PrefabDir + "/" + name + "_Impostor.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 0f);
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);

            float halfW = height * 0.52f * (texW / (float)texH);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int k = 0; k < 2; k++)
            {
                Vector3 right = k == 0 ? Vector3.right : Vector3.forward;
                Vector3 n = Vector3.Cross(right, Vector3.up);
                int b = verts.Count;
                verts.Add(-right * halfW); verts.Add(right * halfW); verts.Add(-right * halfW + Vector3.up * height * 1.04f); verts.Add(right * halfW + Vector3.up * height * 1.04f);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
                for (int i = 0; i < 4; i++) normals.Add(Vector3.up * 0.6f + n * 0.4f);
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = name + "_Impostor" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return (ChunkyMeshGenerator.Save(mesh), mat);
        }

        // ------------------------------------------------------------------ Rocas Poly Haven

        static Material mossyRockMat;

        static Material MossyRockMaterial()
        {
            if (mossyRockMat != null) return mossyRockMat;
            string path = DataDir + "/Hollow_MossyRock.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("ProjectRidge/MossyRock")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("ProjectRidge/MossyRock");
            var src = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_rock_moss_set_01.mat");
            if (src != null)
            {
                m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                m.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
            }
            m.SetColor("_BaseColor", new Color(0.62f, 0.6f, 0.56f));
            m.SetTexture("_MossNoise", NoiseTex());
            m.SetColor("_MossColor", new Color(0.13f, 0.19f, 0.08f));
            m.SetFloat("_MossCoverage", 0.58f);
            EditorUtility.SetDirty(m);
            return mossyRockMat = m;
        }

        /// <summary>Malla de una roca Poly Haven con su transformación horneada, reducida y escalada a un tamaño.</summary>
        static Mesh PolyHavenRockMesh(string prefabName, string outName, Vector3 targetSize)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvDir + "/" + prefabName + ".prefab");
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            var src = mf.sharedMesh;
            var baked = Object.Instantiate(src);
            var m = mf.transform.localToWorldMatrix;
            var v = baked.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
            baked.vertices = v;
            baked.RecalculateBounds();
            var low = MeshDecimator.Decimate(baked, MaxRockTriangles, outName);
            Object.DestroyImmediate(baked);
            // Centrada en planta, apoyada en y = 0, escalada al tamaño pedido (la maqueta decide la forma).
            var b = low.bounds;
            var lv = low.vertices;
            Vector3 k = new Vector3(targetSize.x / b.size.x, targetSize.y / b.size.y, targetSize.z / b.size.z);
            for (int i = 0; i < lv.Length; i++)
            {
                Vector3 q = lv[i] - new Vector3(b.center.x, b.min.y, b.center.z);
                lv[i] = new Vector3(q.x * k.x, q.y * k.y, q.z * k.z);
            }
            low.vertices = lv;
            low.RecalculateNormals();
            low.RecalculateTangents();
            low.RecalculateBounds();
            return ChunkyMeshGenerator.Save(low);
        }

        /// <summary>Malla de colisión reducida (un colisionador convexo admite 256 polígonos).</summary>
        static Mesh CollisionMesh(Mesh visual) => ChunkyMeshGenerator.Save(MeshDecimator.Decimate(visual, 200, visual.name + "_Col"));

        /// <summary>Triángulos máximos por roca en móvil (POLISH_PASS 3.9).</summary>
        const int MaxRockTriangles = 3000;

        /// <summary>Mapa de densidad del último Build (lo usan el pasto y los ajustes del jugador).</summary>
        static HideDensityMap densityMap;

        static readonly string[] CliffRockSources = { "Rock_rock_moss_set_01_rock01", "Rock_rock_moss_set_01_rock02", "Rock_rock_moss_set_01_rock04", "Rock_rock_moss_set_01_rock05" };
        static readonly string[] LooseRockSources = { "Rock_rock_moss_set_01_rock03", "Rock_rock_moss_set_02_rock07", "Rock_rock_moss_set_02_rock09", "Rock_rock_moss_set_02_rock12" };

        // ------------------------------------------------------------------ Tocones, troncos y ramas secas

        static GameObject DecimatedPropPrefab(string source, string name, int maxTris, float scale, bool collider)
        {
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvDir + "/" + source + ".prefab");
            var go = new GameObject(name);
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                var baked = Object.Instantiate(mf.sharedMesh);
                var m = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var v = baked.vertices;
                for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]) * scale;
                baked.vertices = v;
                baked.RecalculateBounds();
                var low = ChunkyMeshGenerator.Save(MeshDecimator.Decimate(baked, maxTris, name + "_" + mf.name));
                Object.DestroyImmediate(baked);
                var part = new GameObject(mf.name);
                part.transform.SetParent(go.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = low;
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
                mr.receiveGI = ReceiveGI.LightProbes;
                if (collider) { var mc = part.AddComponent<MeshCollider>(); mc.sharedMesh = CollisionMesh(low); mc.convex = true; }
            }
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        /// <summary>Tocones y troncos muertos fuera de lo caminable (y algún tocón en el borde de las salas).</summary>
        static int ScatterStumps(Transform root, Terrain t, System.Random rng, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var stump = DecimatedPropPrefab("Stump_01", "Hollow_Stump", 2500, 1f, true);
            var trunk = DecimatedPropPrefab("DeadTrunk_01", "Hollow_DeadTrunk", 3500, 1f, true);
            var parent = new GameObject("Stumps").transform;
            parent.SetParent(root, false);
            int count = 0;
            for (int tries = 0; tries < 4000 && count < 46; tries++)
            {
                var p = new Vector3(R(TerrainMin.x + 3f, TerrainMin.x + SizeX - 3f), 0f, R(TerrainMin.y + 3f, TerrainMin.y + SizeZ - 3f));
                float walk = Walk(p);
                if (walk < 0.6f || walk > 9f || Reserved(p, blockers, 1.6f) || DistanceToTrails(p) < 2.2f) continue;
                bool isTrunk = rng.NextDouble() < 0.3;
                float top = GroundY(t, p) + (isTrunk ? 0.8f : 0.7f);
                if (!Fits(p, top, isTrunk ? 1.6f : 0.6f)) continue;
                p.y = GroundY(t, p) - 0.05f;
                var go = Spawn(isTrunk ? trunk : stump, parent, p, R(0f, 360f), R(0.8f, 1.2f));
                blockers.Add(p);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Ramas secas en el suelo que crujen al pisarlas (POLISH_PASS 3.2: +4 m de ruido). Claras y quebradizas para
        /// leerse a distancia; en los caminos entre escondites de las salas con Lurker.
        /// </summary>
        static int PlaceDryBranches(Transform root, Terrain t)
        {
            var paleMat = AssetDatabase.LoadAssetAtPath<Material>(DataDir + "/Hollow_DryBranch.mat");
            if (paleMat == null) { paleMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(paleMat, DataDir + "/Hollow_DryBranch.mat"); }
            var src = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_dry_branches_medium_01.mat");
            if (src != null) paleMat.CopyPropertiesFromMaterial(src);
            paleMat.SetColor("_BaseColor", new Color(1.25f, 1.12f, 0.92f)); // hueso: se lee claro contra el suelo oscuro
            paleMat.EnableKeyword("_EMISSION");
            paleMat.SetColor("_EmissionColor", new Color(0.06f, 0.05f, 0.04f));
            paleMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(paleMat);
            var prefab = DecimatedPropPrefab("Branches_dry_branches_medium_01_a", "Hollow_DryBranch", 900, 1f, false);
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paleMat;
            var parent = new GameObject("DryBranches").transform;
            parent.SetParent(root, false);
            var noise = AssetDatabase.LoadAssetAtPath<ProjectRidge.Noise.NoiseConfig>("Assets/_Project/Data/NoiseConfig.asset");
            int n = 0;
            foreach (var s in DryBranchSpots)
            {
                var p = OnGround(t, s.x, s.y, 0.02f);
                var go = Spawn(prefab, parent, p, s.x * 37f % 360f, 1f);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paleMat;
                var trig = go.AddComponent<SphereCollider>();
                trig.isTrigger = true;
                trig.radius = 0.55f;
                trig.center = Vector3.up * 0.3f;
                var nb = go.AddComponent<ProjectRidge.Items.DryBranch>();
                var so = new SerializedObject(nb);
                so.FindProperty("noise").objectReferenceValue = noise;
                so.ApplyModifiedPropertiesWithoutUndo();
                n++;
            }
            return n;
        }

        /// <summary>Ramas secas: en los atajos tentadores, junto a las rutas de los Lurker (x, z).</summary>
        static readonly Vector2[] DryBranchSpots =
        {
            new Vector2(-36f, -2.2f), new Vector2(-40f, 1.8f), new Vector2(-12.5f, -2.6f), new Vector2(-6.4f, 1.2f),
            new Vector2(-3.2f, -1.4f), new Vector2(6.2f, -0.6f), new Vector2(15.9f, 0.4f), new Vector2(30.4f, -0.4f),
            new Vector2(38.6f, -1.9f), new Vector2(46.2f, 0.9f), new Vector2(55.6f, -2.4f), new Vector2(59f, 2.6f),
        };

        // ------------------------------------------------------------------ Hitos con luz

        /// <summary>
        /// Un hito por sala visible desde la entrada (POLISH_PASS 3.9): el árbol muerto gigante en el prado del OVNI
        /// y los restos humeantes de algo que cayó en la espesura (brasas cálidas = isla de luz, humo). El refugio,
        /// el puente y la camioneta ya existen.
        /// </summary>
        static void BuildRoomLandmarks(Transform root, Terrain t, List<Vector3> blockers)
        {
            var parent = new GameObject("RoomLandmarks").transform;
            parent.SetParent(root, false);
            ConiferMaterials();
            // Sala 3: árbol muerto gigante al fondo (norte): se ve desde la entrada, no tapa nada.
            var giant = Spawn(CardConiferPrefab(4), parent, OnGround(t, GiantTree.x, GiantTree.y, -0.2f), 25f, 2.1f);
            giant.name = "Landmark_GiantDeadTree";
            blockers.Add(giant.transform.position);

            // Sala 4: restos humeantes (chapa retorcida + brasas + humo).
            var wreck = new GameObject("Landmark_Wreck");
            wreck.transform.SetParent(parent, false);
            wreck.transform.position = OnGround(t, Wreck.x, Wreck.y);
            blockers.Add(wreck.transform.position);
            var metal = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_rusty_metal_02.mat");
            var ember = AssetDatabase.LoadAssetAtPath<Material>(DataDir + "/Hollow_Ember.mat");
            if (ember == null) { ember = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(ember, DataDir + "/Hollow_Ember.mat"); }
            ember.SetColor("_BaseColor", new Color(0.08f, 0.04f, 0.03f));
            ember.EnableKeyword("_EMISSION");
            ember.SetColor("_EmissionColor", new Color(1f, 0.32f, 0.07f) * 6f);
            ember.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(ember);
            var rng = new System.Random(5);
            for (int i = 0; i < 7; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Shard";
                shard.transform.SetParent(wreck.transform, false);
                shard.transform.localPosition = new Vector3((float)rng.NextDouble() * 2.4f - 1.2f, 0.25f + (float)rng.NextDouble() * 0.3f, (float)rng.NextDouble() * 1.6f - 0.8f);
                shard.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 70f - 35f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 60f - 30f);
                shard.transform.localScale = new Vector3(0.9f + (float)rng.NextDouble(), 0.05f, 0.5f + (float)rng.NextDouble() * 0.6f);
                shard.GetComponent<MeshRenderer>().sharedMaterial = metal;
            }
            for (int i = 0; i < 9; i++)
            {
                var coal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(coal.GetComponent<Collider>());
                coal.name = "Ember";
                coal.transform.SetParent(wreck.transform, false);
                coal.transform.localPosition = new Vector3((float)rng.NextDouble() * 1.8f - 0.9f, 0.05f, (float)rng.NextDouble() * 1.2f - 0.6f);
                coal.transform.localScale = Vector3.one * (0.12f + (float)rng.NextDouble() * 0.18f);
                coal.GetComponent<MeshRenderer>().sharedMaterial = ember;
            }
            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.name = "EmberGlow";
            glow.transform.SetParent(wreck.transform, false);
            glow.transform.localPosition = Vector3.up * 0.06f;
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localScale = Vector3.one * 4.2f;
            // Material propio, con partículas suaves: aquí el suelo es irregular y sin ellas el quad corta recto contra la pendiente.
            glow.GetComponent<MeshRenderer>().sharedMaterial = FxMaterial("FX_EmberGlow", "Universal Render Pipeline/Particles/Unlit", SoftDot(), new Color(2.6f, 0.57f, 0.08f, 0.9f), true);
            // Humo que sube y se deshace (sin luz dinámica: el presupuesto es de 4).
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(wreck.transform, false);
            smoke.transform.localPosition = Vector3.up * 0.4f;
            var ps = smoke.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startColor = new Color(0.22f, 0.2f, 0.19f, 0.35f);
            main.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = 5f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.7f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(0.15f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0.05f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = smoke.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = FxMaterial("FX_WreckSmoke", "Universal Render Pipeline/Particles/Unlit", SoftDot(), new Color(0.6f, 0.58f, 0.55f, 1f), false);
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static readonly Vector2 GiantTree = new Vector2(12.5f, 8.6f);
        static readonly Vector2 Wreck = new Vector2(36.5f, 6.8f); // en la terraza norte de la espesura: se ve desde la entrada, lejos de la patrulla
    }
}
