using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Matas de flores pequeñas y pálidas (blanco, lila, azul) que salpican el pasto como en Echoes of Wisdom; de
    /// noche recogen la luz de la luna. Mallas por grupo con color por vértice y altura en UV2 (viento en mallas
    /// combinadas), combinadas por zona.
    /// </summary>
    public static partial class HollowBuilder
    {
        static Mesh FlowerClusterMesh(string name, Color petal, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var uv2 = new List<Vector2>(); var t = new List<int>();
            int Add(Vector3 p, Vector3 normal, Color col) { v.Add(p); n.Add(normal); c.Add(col); uv2.Add(new Vector2(p.y, 0f)); return v.Count - 1; }
            var stemCol = new Color(0.12f, 0.26f, 0.12f, 0.8f);
            var leafCol = new Color(0.14f, 0.32f, 0.15f, 0.85f);
            int flowers = rng.Next(5, 9);
            for (int f = 0; f < flowers; f++)
            {
                float a = R(0f, Mathf.PI * 2f), r = R(0f, 0.22f);
                var basePos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float h = R(0.1f, 0.24f);
                var top = basePos + new Vector3(R(-0.03f, 0.03f), h, R(-0.03f, 0.03f));
                // Tallo: cinta fina.
                Vector3 side = new Vector3(0.008f, 0f, 0f);
                int s0 = Add(basePos - side, Vector3.back, stemCol), s1 = Add(basePos + side, Vector3.back, stemCol);
                int s2 = Add(top - side, Vector3.back, stemCol), s3 = Add(top + side, Vector3.back, stemCol);
                t.AddRange(new[] { s0, s2, s1, s1, s2, s3 });
                // Corola: 5 pétalos redondeados mirando arriba (un poco inclinada).
                var tilt = Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f));
                Vector3 up = tilt * Vector3.up;
                var col = petal * R(0.85f, 1.1f);
                col.a = 1f;
                int center = Add(top + up * 0.004f, up, new Color(0.9f, 0.78f, 0.35f, 1f));
                const int petals = 5;
                float size = R(0.035f, 0.055f);
                for (int p = 0; p < petals; p++)
                {
                    float pa = p / (float)petals * Mathf.PI * 2f;
                    Vector3 dir = tilt * new Vector3(Mathf.Cos(pa), 0f, Mathf.Sin(pa));
                    Vector3 perp = Vector3.Cross(up, dir) * size * 0.45f;
                    int i0 = Add(top + dir * size * 0.25f - perp, up, col);
                    int i1 = Add(top + dir * size + up * 0.006f, up, col);
                    int i2 = Add(top + dir * size * 0.25f + perp, up, col);
                    t.AddRange(new[] { center, i0, i1, center, i1, i2 });
                }
            }
            // Hojas en la base.
            for (int l = 0; l < 4; l++)
            {
                float a = l / 4f * Mathf.PI * 2f + R(-0.3f, 0.3f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 perp = Vector3.Cross(Vector3.up, dir) * 0.035f;
                int i0 = Add(-perp, Vector3.up, leafCol), i1 = Add(perp, Vector3.up, leafCol);
                int i2 = Add(dir * 0.16f + Vector3.up * 0.05f, Vector3.up, leafCol);
                t.AddRange(new[] { i0, i2, i1 });
            }
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(1, uv2); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static int Flowers(Transform root, Terrain t)
        {
            var mat = WindMaterial("Hollow_StylizedFlowers", null, null, Color.white, 0.2f, 0.05f, 2.5f, 0.02f, true, 0.35f);
            mat.SetFloat("_UseVertexColor", 1f);
            mat.SetFloat("_UseVertexHeight", 1f);
            mat.SetColor("_RimColor", new Color(0.08f, 0.12f, 0.18f));
            EditorUtility.SetDirty(mat);
            var palette = new[] { new Color(0.85f, 0.86f, 0.9f), new Color(0.62f, 0.5f, 0.85f), new Color(0.5f, 0.68f, 0.95f), new Color(0.9f, 0.62f, 0.7f) };
            var meshes = new Mesh[palette.Length];
            for (int i = 0; i < palette.Length; i++) meshes[i] = ChunkyMeshGenerator.Save(FlowerClusterMesh("Styl_Flowers_" + i, palette[i], 900 + i));

            var rng = new System.Random(Seed + 313);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var chunks = new Dictionary<(int, int), List<CombineInstance>>();
            int count = 0;
            for (int tries = 0; tries < 9000 && count < 420; tries++)
            {
                var p = new Vector3(R(TerrainMin.x, TerrainMin.x + SizeX), 0f, R(TerrainMin.y, TerrainMin.y + SizeZ));
                float d = Walk(p);
                if (d > 9f || Mathf.Abs(p.x - RavineX(p.z)) < 3.8f || DistanceToTrails(p) < 1.4f) continue;
                if (d > -0.3f && d < 0.8f) continue; // no en el borde del muro
                bool inPatch = false;
                foreach (var h in HidePatches) if (new Vector2(p.x - h.x, p.z - h.y).magnitude < h.z + 0.5f) { inPatch = true; break; }
                if (inPatch) continue;
                // Manchas: las flores crecen en grupos, no uniformes.
                if (Mathf.PerlinNoise(p.x * 0.18f + 31f, p.z * 0.18f + 7f) < 0.52f) continue;
                p.y = GroundY(t, p);
                int variant = Mathf.FloorToInt(Mathf.PerlinNoise(p.x * 0.05f, p.z * 0.05f + 50f) * palette.Length * 1.6f) % palette.Length;
                var key = (Mathf.FloorToInt(p.x / 14f), Mathf.FloorToInt(p.z / 14f));
                if (!chunks.TryGetValue(key, out var list)) chunks[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = meshes[variant], transform = Matrix4x4.TRS(p, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(0.9f, 1.4f)) });
                count++;
            }
            var parent = new GameObject("Flowers").transform;
            parent.SetParent(root, false);
            foreach (var kv in chunks)
            {
                var combined = new Mesh { name = "FlowerChunk_" + kv.Key.Item1 + "_" + kv.Key.Item2, indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(kv.Value.ToArray(), true, true);
                combined = ChunkyMeshGenerator.Save(combined);
                var go = new GameObject(combined.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = combined;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveGI = ReceiveGI.LightProbes;
            }
            return count;
        }
    }
}
