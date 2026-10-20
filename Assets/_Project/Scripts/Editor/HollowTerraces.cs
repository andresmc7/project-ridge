using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Look de diorama (Echoes of Wisdom) para The Hollow: muros de bloques de piedra en cada escalón de terraza
    /// (siguen el contorno del suelo caminable) y capas de terreno pintadas a mano por código (pasto, tierra del
    /// sendero, hojarasca, piedra) en vez de fotos: formas limpias que se leen desde la cámara alta.
    /// </summary>
    public static partial class HollowBuilder
    {
        static readonly StoneBatch.Palette WallPalette = new StoneBatch.Palette
        {
            stoneA = new Color(0.36f, 0.25f, 0.19f), stoneB = new Color(0.27f, 0.22f, 0.2f),
            moss = new Color(0.16f, 0.27f, 0.12f), grout = new Color(0.06f, 0.045f, 0.04f),
        };

        static Material StoneMaterial()
        {
            var mat = WindMaterial("Hollow_StylizedStone", null, null, Color.white, 0.12f, 0f, 0f, 0f, false, 0f);
            mat.SetFloat("_UseVertexColor", 1f);
            mat.SetColor("_RimColor", new Color(0.05f, 0.08f, 0.13f));
            mat.SetFloat("_RimPower", 4f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Gradiente de Walk (apunta hacia afuera del suelo caminable).</summary>
        static Vector3 WalkNormal(Vector3 p)
        {
            const float e = 0.2f;
            float gx = Walk(p + Vector3.right * e) - Walk(p - Vector3.right * e);
            float gz = Walk(p + Vector3.forward * e) - Walk(p - Vector3.forward * e);
            var g = new Vector3(gx, 0f, gz);
            return g.sqrMagnitude > 1e-6f ? g.normalized : Vector3.forward;
        }

        /// <summary>Muros de piedra sobre los escalones de terraza. Devuelve el número de bloques.</summary>
        static int TerraceWalls(Transform root, Terrain t)
        {
            var parent = new GameObject("TerraceWalls").transform;
            parent.SetParent(root, false);
            var mat = StoneMaterial();
            var rng = new System.Random(Seed + 77);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Escalones: nivel del contorno (Walk), y si es el del norte (sube) o el del sur (baja).
            var levels = new (float d, bool north)[]
            {
                (StepWidth * 0.5f, true), (StepWidth * 0.5f, false),
                (StepUp2At + StepWidth * 0.5f, true), (StepDown2At + StepWidth * 0.5f, false),
            };
            const float grid = 0.25f, spacing = 0.8f;
            var accepted = new List<(Vector3 p, bool north)>();
            var hash = new Dictionary<Vector2Int, List<Vector3>>();
            bool FarEnough(Vector3 p)
            {
                var cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (hash.TryGetValue(new Vector2Int(cell.x + dx, cell.y + dz), out var list))
                            foreach (var q in list) if ((q - p).sqrMagnitude < spacing * spacing) return false;
                return true;
            }
            void Accept(Vector3 p, bool north)
            {
                var cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
                if (!hash.TryGetValue(cell, out var list)) hash[cell] = list = new List<Vector3>();
                list.Add(p);
                accepted.Add((p, north));
            }

            float x0 = TerrainMin.x + 1f, x1 = TerrainMin.x + SizeX - 1f, z0 = TerrainMin.y + 1f, z1 = TerrainMin.y + SizeZ - 1f;
            for (float x = x0; x < x1; x += grid)
                for (float z = z0; z < z1; z += grid)
                {
                    var p = new Vector3(x, 0f, z);
                    float d00 = Walk(p);
                    if (d00 > StepUp2At + 2f) continue;
                    float d10 = Walk(p + Vector3.right * grid), d01 = Walk(p + Vector3.forward * grid);
                    foreach (var (L, north) in levels)
                    {
                        Vector3 hit;
                        if ((d00 - L) * (d10 - L) < 0f) hit = p + Vector3.right * grid * Mathf.InverseLerp(d00, d10, L);
                        else if ((d00 - L) * (d01 - L) < 0f) hit = p + Vector3.forward * grid * Mathf.InverseLerp(d00, d01, L);
                        else continue;
                        if (Mathf.Abs(hit.x - RavineX(hit.z)) < 4.2f) continue;
                        bool south = !float.IsInfinity(SouthClearance(hit, 0f));
                        if (south == north) continue; // este escalón no existe de este lado
                        if (!FarEnough(hit)) continue;
                        Accept(hit, north);
                    }
                }

            // Bloques por lotes de 16 m (frustum culling razonable, pocos draw calls).
            var batches = new Dictionary<int, StoneBatch>();
            int blocks = 0;
            foreach (var (p, north) in accepted)
            {
                Vector3 n = WalkNormal(p);
                Vector3 face = north ? -n : n; // la cara visible mira hacia el lado bajo
                float a = GroundY(t, p - face * 0.9f), b = GroundY(t, p + face * 0.9f);
                float low = Mathf.Min(a, b), high = Mathf.Max(a, b);
                float wallH = high - low + 0.1f;
                if (wallH < 0.6f) continue;
                int rows = Mathf.Max(1, Mathf.RoundToInt(wallH / 0.8f));
                float rowH = wallH / rows;
                Vector3 tangent = Vector3.Cross(Vector3.up, face);
                int key = Mathf.FloorToInt(p.x / 16f);
                if (!batches.TryGetValue(key, out var batch)) batches[key] = batch = new StoneBatch();
                for (int r = 0; r < rows; r++)
                {
                    float width = R(0.95f, 1.4f);
                    float along = (r % 2) * 0.35f + R(-0.15f, 0.15f);
                    float h = rowH * R(0.94f, 1.04f);
                    Vector3 c = new Vector3(p.x, low + rowH * (r + 0.5f) - 0.04f, p.z) - face * 0.22f + tangent * along;
                    var rot = Quaternion.LookRotation(face) * Quaternion.Euler(R(-3f, 3f), R(-5f, 5f), R(-2.5f, 2.5f));
                    batch.Block(c, rot, new Vector3(width, h, 0.9f), 0.14f, WallPalette, R(0f, 1f), r == rows - 1, rng.Next(1000));
                    blocks++;
                }
            }
            foreach (var kv in batches)
            {
                var mesh = ChunkyMeshGenerator.Save(kv.Value.ToMesh("TerraceWall_" + kv.Key));
                var go = new GameObject("Wall_" + kv.Key);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveGI = ReceiveGI.LightProbes;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
            return blocks;
        }

        // ------------------------------------------------------------------ Capas de terreno pintadas

        static float Hash2(int x, int y, int s) => Mathf.Abs(Mathf.Sin(x * 127.1f + y * 311.7f + s * 74.7f) * 43758.5453f) % 1f;

        // Ruido de gradiente periódico (tilea sin costuras ni las diagonales del fbm toroidal).
        static float PNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float G(int ix, int iy, float dx, float dy)
            {
                ix = ((ix % period) + period) % period; iy = ((iy % period) + period) % period;
                float a = Hash2(ix, iy, seed) * Mathf.PI * 2f;
                return Mathf.Cos(a) * dx + Mathf.Sin(a) * dy;
            }
            float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
            float u = Fade(fx), v = Fade(fy);
            float a0 = Mathf.Lerp(G(x0, y0, fx, fy), G(x0 + 1, y0, fx - 1f, fy), u);
            float a1 = Mathf.Lerp(G(x0, y0 + 1, fx, fy - 1f), G(x0 + 1, y0 + 1, fx - 1f, fy - 1f), u);
            return Mathf.Lerp(a0, a1, v) * 0.75f + 0.5f;
        }

        static float PeriodicFbm(float u, float v, int octaves, int baseFreq, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int f = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += PNoise(u * f, v * f, f, seed + o * 31) * amp;
                norm += amp; amp *= 0.5f; f *= 2;
            }
            return sum / norm;
        }

        /// <summary>Textura tileable pintada: base con manchas grandes, trazos pequeños (briznas o piedritas).</summary>
        static Texture2D PaintedTexture(string name, Color a, Color b, Color strokeLight, Color strokeDark, int strokes, float strokeLen, float strokeWidth, bool blades, int seed)
        {
            const int size = 512;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float big = PeriodicFbm(u, v, 3, 3, seed);
                    float mid = PeriodicFbm(u, v, 4, 10, seed + 7);
                    var c = Color.Lerp(a, b, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.62f, big)));
                    c *= 0.92f + (mid - 0.5f) * 0.25f;
                    c.a = 0.12f; // suavidad baja (mate)
                    px[y * size + x] = c;
                }
            var rng = new System.Random(seed);
            for (int s = 0; s < strokes; s++)
            {
                float cx = (float)rng.NextDouble() * size, cy = (float)rng.NextDouble() * size;
                float ang = blades ? Mathf.PI * 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.9f : (float)rng.NextDouble() * Mathf.PI;
                float len = strokeLen * (0.6f + (float)rng.NextDouble() * 0.8f);
                float wid = strokeWidth * (0.7f + (float)rng.NextDouble() * 0.6f);
                bool light = rng.NextDouble() < 0.55;
                var col = light ? strokeLight : strokeDark;
                float strength = 0.35f + (float)rng.NextDouble() * 0.4f;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                int ext = Mathf.CeilToInt(len + wid) + 1;
                for (int oy = -ext; oy <= ext; oy++)
                    for (int ox = -ext; ox <= ext; ox++)
                    {
                        Vector2 o = new Vector2(ox, oy);
                        float along = Vector2.Dot(o, dir), across = Mathf.Abs(o.x * dir.y - o.y * dir.x);
                        if (along < -len * 0.5f || along > len * 0.5f) continue;
                        float tt = (along / len) + 0.5f;
                        float w = blades ? wid * (1f - tt) : wid * Mathf.Sin(Mathf.PI * tt);
                        if (across > w) continue;
                        float k = strength * (1f - across / Mathf.Max(w, 0.01f)) * (blades ? Mathf.Lerp(0.6f, 1f, tt) : 1f);
                        int ix = ((Mathf.RoundToInt(cx) + ox) % size + size) % size, iy = ((Mathf.RoundToInt(cy) + oy) % size + size) % size;
                        var p = px[iy * size + ix];
                        var mixed = Color.Lerp(p, col, k);
                        mixed.a = p.a;
                        px[iy * size + ix] = mixed;
                    }
            }
            return WriteTex(name, size, size, (x, y) => px[y * size + x], true, false);
        }

        static TerrainLayer PaintedLayer(string name, Texture2D albedo, float tile)
        {
            string path = PolyHavenMaterialBuilder.OutTerrainLayers + "/TL_" + name + ".terrainlayer";
            PolyHavenMaterialBuilder.EnsureFolder(PolyHavenMaterialBuilder.OutTerrainLayers);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = albedo;
            layer.normalMapTexture = null;
            layer.maskMapTexture = null;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0.1f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>Reemplaza las capas fotográficas del terreno por capas pintadas (pasto, sendero, hojarasca, piedra).</summary>
        static void PaintedTerrainLayers(Mats m)
        {
            var grass = PaintedTexture("Tex_PaintGrass", new Color(0.2f, 0.36f, 0.2f), new Color(0.15f, 0.3f, 0.19f),
                new Color(0.3f, 0.48f, 0.24f), new Color(0.09f, 0.2f, 0.12f), 2600, 9f, 2.2f, true, 5);
            var dirt = PaintedTexture("Tex_PaintDirt", new Color(0.4f, 0.3f, 0.21f), new Color(0.34f, 0.25f, 0.18f),
                new Color(0.52f, 0.42f, 0.32f), new Color(0.2f, 0.14f, 0.1f), 500, 4f, 2.6f, false, 9);
            var litter = PaintedTexture("Tex_PaintLitter", new Color(0.13f, 0.2f, 0.13f), new Color(0.2f, 0.17f, 0.11f),
                new Color(0.3f, 0.24f, 0.13f), new Color(0.05f, 0.08f, 0.05f), 1800, 7f, 3f, false, 13);
            var stone = PaintedTexture("Tex_PaintStone", new Color(0.32f, 0.26f, 0.22f), new Color(0.24f, 0.21f, 0.2f),
                new Color(0.42f, 0.36f, 0.3f), new Color(0.1f, 0.08f, 0.07f), 700, 14f, 2f, false, 17);
            m.tlGround = PaintedLayer("PaintGrass", grass, 5f);
            m.tlTrail = PaintedLayer("PaintDirt", dirt, 4f);
            m.tlLeaves = PaintedLayer("PaintLitter", litter, 6f);
            m.tlRock = PaintedLayer("PaintStone", stone, 4f);
            m.rock = Simple("Hollow_PaintRock", stone, null, new Color(1.1f, 1.05f, 1f), 0.12f);
        }
    }
}
