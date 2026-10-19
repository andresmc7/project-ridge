using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Nivel completo "The Hollow" (spec sección 6): refugio en ruinas sin techo → sendero → arroyo con puente
    /// (ruta corta y expuesta por el prado del OVNI) o tronco caído (ruta larga y cubierta por el bosque del norte)
    /// → campamento abandonado → camino de acceso con la camioneta y la radio (final).
    /// Estilo: geometría chunky y legible (terrazas, acantilados, rocas y copas redondeadas) con texturas fotográficas.
    /// Regenerable: reconstruye todo lo generado a partir de la escena del look dev (jugador, cámara, HUD, Lurker, OVNI).
    /// </summary>
    public static partial class HollowBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/TheHollow.unity";
        const string SourceScene = "Assets/_Project/Scenes/LookDev_ForestCorner.unity";
        const string DataDir = "Assets/_Project/Art/Hollow";
        const string PrefabDir = "Assets/_Project/Prefabs/Hollow";
        const int Seed = 4127;

        // Terreno: 150 × 100 m, origen en y = -3 para poder tallar el arroyo.
        const float SizeX = 150f, SizeZ = 100f, MaxHeight = 24f, Base = 3f;
        const int HeightRes = 513, AlphaRes = 512;

        // ------------------------------------------------------------------ Layout

        static readonly Vector3 CabinCenter = new Vector3(-54f, 0f, -6f);
        const float CabinYaw = 8f;
        static readonly Vector3 Meadow = new Vector3(8f, 0f, -14f);
        const float MeadowRadius = 12f;
        static readonly Vector3 Camp = new Vector3(34f, 0f, 5f);
        static readonly Vector3 TruckPos = new Vector3(53f, 0f, 8f);
        const float RoadX = 52f, RoadHalf = 3.2f;
        const float BridgeZ = -6f, LogZ = 24f;

        static readonly Vector3[] MainTrail =
        {
            new Vector3(-54, 0, -6), new Vector3(-46, 0, -5), new Vector3(-38, 0, -9), new Vector3(-30, 0, -6),
            new Vector3(-22, 0, -7), new Vector3(-17, 0, -6), new Vector3(-7, 0, -6), new Vector3(-2, 0, -9),
            new Vector3(8, 0, -13), new Vector3(20, 0, -14), new Vector3(30, 0, -8), new Vector3(34, 0, 1),
            new Vector3(42, 0, 3), new Vector3(RoadX, 0, 4)
        };

        static readonly Vector3[] NorthTrail =
        {
            new Vector3(-30, 0, -6), new Vector3(-27, 0, 5), new Vector3(-22, 0, 16), new Vector3(-17, 0, 24),
            new Vector3(-7, 0, 24), new Vector3(0, 0, 26), new Vector3(10, 0, 24), new Vector3(20, 0, 20),
            new Vector3(28, 0, 12), new Vector3(34, 0, 6)
        };

        static float RavineX(float z) => -12f + 1.5f * Mathf.Sin(z * 0.07f);

        // ------------------------------------------------------------------ Entrada

        [MenuItem("Project Ridge/The Hollow/Build Level")]
        public static string Build()
        {
            var log = new System.Text.StringBuilder();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                AssetDatabase.CopyAsset(SourceScene, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var n in new[] { "LookDevEnvironment", "Items", "LurkerPatrol", "LurkerPatrol_B", "UFO", "UFOScanPoints", "LightProbes",
                                      "Moon", "PostFX", "PlayerSpawn", "Hollow", "Lurker_B", "Checkpoints" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            PolyHavenMaterialBuilder.EnsureFolder(DataDir);
            PolyHavenMaterialBuilder.EnsureFolder(PrefabDir);
            var mats = BuildMaterials();
            log.AppendLine("materials ok");

            var root = new GameObject("Hollow").transform;
            var terrain = BuildTerrain(root, mats);
            log.AppendLine("terrain ok");

            var rng = new System.Random(Seed);
            var blockers = new List<Vector3>(); // posiciones ocupadas por props construidos (no poner árboles encima)
            int built = BuildLandmarks(root, terrain, mats, blockers);
            log.AppendLine("landmarks=" + built);
            int trees = ScatterTrees(root, terrain, mats, rng, blockers);
            log.AppendLine("trees=" + trees);
            int rocks = ScatterRocks(root, terrain, mats, rng, blockers);
            log.AppendLine("rocks=" + rocks);
            int bushes = PlaceBushes(root, terrain, mats, rng, blockers);
            log.AppendLine("bushes=" + bushes);
            int grass = PlaceGrass(root, terrain, rng);
            log.AppendLine("grass tufts=" + grass);
            BuildBounds(root, terrain);

            LookDevForestCornerBuilder.SetupLighting(log);
            LookDevForestCornerBuilder.SetupPostFx();
            TunePostFx();
            int probes = BuildLightProbes(terrain);
            log.AppendLine("probes=" + probes);

            var player = GameObject.Find("Player");
            SetupPlayerAndEnemies(terrain, player, log);
            PlaceItems(terrain, log);
            BuildCheckpoints(terrain);

            LookDevForestCornerBuilder.BuildUFO(terrain, player);
            PlaceUfoScanPoints(terrain);

            // NavMesh
            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1;
            surface.BuildNavMesh();
            string navDir = "Assets/_Project/Scenes/TheHollow";
            PolyHavenMaterialBuilder.EnsureFolder(navDir);
            string navPath = navDir + "/NavMesh-TheHollow.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (existing != null) AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            log.AppendLine("navmesh verts=" + NavMesh.CalculateTriangulation().vertices.Length);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static void SetBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) list.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ Materiales

        internal class Mats
        {
            public Material ground, bark, needles, leaves, rock, planks, metal, water, cutWood, darkMetal, fabric, glowLantern, rope;
            public TerrainLayer tlGround, tlTrail, tlLeaves, tlRock;
        }

        static Mats BuildMaterials()
        {
            string tex = PolyHavenMaterialBuilder.TexturesRoot;
            var m = new Mats();
            var planks = PolyHavenMaterialBuilder.BuildTextureSet(tex + "/weathered_brown_planks", "weathered_brown_planks");
            m.planks = PolyHavenMaterialBuilder.BuildLitMaterial("weathered_brown_planks", planks, 0.8f);
            var rock = PolyHavenMaterialBuilder.BuildTextureSet(tex + "/mossy_rock", "mossy_rock");
            m.rock = PolyHavenMaterialBuilder.BuildLitMaterial("mossy_rock", rock, 0.9f);
            var metal = PolyHavenMaterialBuilder.BuildTextureSet(tex + "/rusty_metal_02", "rusty_metal_02");
            m.metal = PolyHavenMaterialBuilder.BuildLitMaterial("rusty_metal_02", metal, 0.8f);
            var bark = PolyHavenMaterialBuilder.BuildTextureSet(tex + "/pine_bark", "pine_bark");
            m.bark = PolyHavenMaterialBuilder.BuildLitMaterial("pine_bark", bark, 0.8f);
            var fabric = PolyHavenMaterialBuilder.BuildTextureSet(tex + "/denim_fabric", "denim_fabric");
            m.fabric = Tinted(PolyHavenMaterialBuilder.BuildLitMaterial("denim_fabric", fabric, 0.6f), "Hollow_TentFabric", new Color(0.55f, 0.6f, 0.45f));

            var (needleA, needleN) = ChunkyMeshGenerator.PatternTextures(ChunkyMeshGenerator.Pattern.Needles, 11);
            m.needles = Simple("Hollow_Needles", needleA, needleN, new Color(1f, 1.05f, 0.95f), 0.15f);
            var (leafA, leafN) = ChunkyMeshGenerator.PatternTextures(ChunkyMeshGenerator.Pattern.Leaves, 23);
            m.leaves = Simple("Hollow_Leaves", leafA, leafN, new Color(1f, 1.02f, 0.92f), 0.2f);
            m.cutWood = Tinted(m.planks, "Hollow_CutWood", new Color(0.95f, 0.85f, 0.7f));
            m.darkMetal = Tinted(m.metal, "Hollow_DarkMetal", new Color(0.3f, 0.32f, 0.33f));
            m.rope = Simple("Hollow_Rope", null, null, new Color(0.35f, 0.3f, 0.22f), 0.1f);
            m.water = Simple("Hollow_Water", null, null, new Color(0.05f, 0.08f, 0.1f), 0.97f);
            m.glowLantern = Simple("Hollow_LanternGlass", null, null, new Color(1f, 0.75f, 0.4f), 0.6f);
            m.glowLantern.EnableKeyword("_EMISSION");
            m.glowLantern.SetColor("_EmissionColor", new Color(2.2f, 1.2f, 0.45f));

            string tl = PolyHavenMaterialBuilder.OutTerrainLayers;
            m.tlGround = AssetDatabase.LoadAssetAtPath<TerrainLayer>(tl + "/TL_ForestGround.terrainlayer");
            m.tlTrail = AssetDatabase.LoadAssetAtPath<TerrainLayer>(tl + "/TL_Trail.terrainlayer");
            m.tlLeaves = AssetDatabase.LoadAssetAtPath<TerrainLayer>(tl + "/TL_Leaves.terrainlayer");
            m.tlRock = PolyHavenMaterialBuilder.BuildTerrainLayer("MossyRock", rock, 4f);
            return m;
        }

        static Material Simple(string name, Texture2D albedo, Texture2D normal, Color color, float smoothness)
        {
            string path = DataDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", color);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Tinted(Material src, string name, Color tint)
        {
            string path = DataDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            m.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ Terreno

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static float DistanceToPath(Vector3 w, Vector3[] path)
        {
            w = Flat(w);
            float best = float.MaxValue;
            for (int i = 1; i < path.Length; i++)
            {
                Vector3 a = path[i - 1], b = path[i];
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(w - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector3.Distance(w, a + ab * t));
            }
            return best;
        }

        static float DistanceToTrails(Vector3 w) => Mathf.Min(DistanceToPath(w, MainTrail), DistanceToPath(w, NorthTrail));

        /// <summary>Meseta central que separa la ruta expuesta (sur) de la cubierta (norte). 0 fuera, 1 encima.</summary>
        static float PlateauMask(Vector3 w)
        {
            float dx = (w.x - 12f) / 16f, dz = (w.z - 5f) / 6.2f;
            float d = Mathf.Sqrt(dx * dx + dz * dz) + (Mathf.PerlinNoise(w.x * 0.15f + 3f, w.z * 0.15f) - 0.5f) * 0.18f;
            return Smooth(1f, 0.9f, d);
        }

        /// <summary>Distancia (m) al borde del área jugable; negativa fuera.</summary>
        static float EdgeDistance(Vector3 w)
        {
            float wobble = (Mathf.PerlinNoise(w.x * 0.08f + 11f, w.z * 0.08f + 5f) - 0.5f) * 6f;
            float e = Mathf.Min(64f - Mathf.Abs(w.x), 40f - Mathf.Abs(w.z)) + wobble;
            // El camino de acceso corre hasta el borde este: no lo tapa el acantilado.
            if (Mathf.Abs(w.x - RoadX) < RoadHalf + 2f) e = Mathf.Max(e, 64f - Mathf.Abs(w.x));
            return e;
        }

        static float HeightAt(Vector3 w)
        {
            float h = (Mathf.PerlinNoise(w.x * 0.05f + 7f, w.z * 0.05f + 3f) - 0.5f) * 1.2f
                    + (Mathf.PerlinNoise(w.x * 0.16f + 1f, w.z * 0.16f + 9f) - 0.5f) * 0.35f;

            // Lomas suaves para dar profundidad.
            h += 2.4f * Smooth(11f, 2f, Vector3.Distance(Flat(w), new Vector3(-40f, 0f, 22f)));
            h += 2.6f * Smooth(12f, 3f, Vector3.Distance(Flat(w), new Vector3(24f, 0f, -30f)));
            h += 1.8f * Smooth(9f, 2f, Vector3.Distance(Flat(w), new Vector3(-36f, 0f, -28f)));

            // Zonas planas (sendero, refugio, prado, campamento, camino).
            float flat = 0f;
            flat = Mathf.Max(flat, Smooth(3.4f, 1.6f, DistanceToTrails(w)));
            flat = Mathf.Max(flat, Smooth(9f, 6f, Vector3.Distance(Flat(w), CabinCenter)));
            flat = Mathf.Max(flat, Smooth(MeadowRadius + 3f, MeadowRadius - 2f, Vector3.Distance(Flat(w), Meadow)) * 0.8f);
            flat = Mathf.Max(flat, Smooth(8f, 5f, Vector3.Distance(Flat(w), Camp)));
            flat = Mathf.Max(flat, Smooth(RoadHalf + 2.5f, RoadHalf, Mathf.Abs(w.x - RoadX)));
            h = Mathf.Lerp(h, 0f, flat);

            // Terrazas: meseta central (+4 m, acantilado) y bordes del valle en dos escalones.
            h += 4.2f * PlateauMask(w);
            float e = EdgeDistance(w);
            h += 6f * Smooth(0f, -2.4f, e) + 5f * Smooth(-8f, -10.5f, e);

            // Arroyo: barranco de paredes empinadas.
            float dx = Mathf.Abs(w.x - RavineX(w.z));
            h -= 2.7f * Smooth(3.7f, 2.2f, dx);
            return h;
        }

        static Terrain BuildTerrain(Transform parent, Mats m)
        {
            string tdPath = DataDir + "/Hollow_TerrainData.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
            if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, tdPath); }
            td.heightmapResolution = HeightRes;
            td.alphamapResolution = AlphaRes;
            td.size = new Vector3(SizeX, MaxHeight, SizeZ);

            var heights = new float[HeightRes, HeightRes];
            for (int z = 0; z < HeightRes; z++)
                for (int x = 0; x < HeightRes; x++)
                {
                    var w = new Vector3(x / (float)(HeightRes - 1) * SizeX - SizeX * 0.5f, 0f, z / (float)(HeightRes - 1) * SizeZ - SizeZ * 0.5f);
                    heights[z, x] = Mathf.Clamp01((HeightAt(w) + Base) / MaxHeight);
                }
            td.SetHeights(0, 0, heights);

            td.terrainLayers = new[] { m.tlGround, m.tlTrail, m.tlLeaves, m.tlRock };
            var alphas = new float[AlphaRes, AlphaRes, 4];
            for (int z = 0; z < AlphaRes; z++)
                for (int x = 0; x < AlphaRes; x++)
                {
                    float nx = x / (float)(AlphaRes - 1), nz = z / (float)(AlphaRes - 1);
                    var w = new Vector3(nx * SizeX - SizeX * 0.5f, 0f, nz * SizeZ - SizeZ * 0.5f);
                    float steep = td.GetSteepness(nx, nz);
                    float rock = Smooth(24f, 38f, steep);
                    float trail = Smooth(2.4f, 1.1f, DistanceToTrails(w));
                    trail = Mathf.Max(trail, Smooth(RoadHalf + 0.8f, RoadHalf - 0.6f, Mathf.Abs(w.x - RoadX)));
                    trail = Mathf.Max(trail, Smooth(5f, 2.5f, Vector3.Distance(Flat(w), Camp)) * 0.7f);
                    trail = Mathf.Max(trail, Smooth(1.5f, 0.6f, Mathf.Abs(w.x - RavineX(w.z))));
                    float leaves = Smooth(0.5f, 0.68f, Mathf.PerlinNoise(w.x * 0.08f + 40f, w.z * 0.08f + 13f));
                    trail *= 1f - rock;
                    leaves *= (1f - trail) * (1f - rock);
                    float ground = Mathf.Max(0f, 1f - rock - trail - leaves);
                    float sum = ground + trail + leaves + rock;
                    alphas[z, x, 0] = ground / sum;
                    alphas[z, x, 1] = trail / sum;
                    alphas[z, x, 2] = leaves / sum;
                    alphas[z, x, 3] = rock / sum;
                }
            td.SetAlphamaps(0, 0, alphas);
            EditorUtility.SetDirty(td);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-SizeX * 0.5f, -Base, -SizeZ * 0.5f);
            var terrain = go.GetComponent<Terrain>();
            string matPath = DataDir + "/Hollow_TerrainLit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            terrain.materialTemplate = mat;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 60f;
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return terrain;
        }

        internal static float GroundY(Terrain t, Vector3 w) => t.SampleHeight(w) + t.transform.position.y;

        internal static Vector3 OnGround(Terrain t, float x, float z, float lift = 0f)
        {
            var p = new Vector3(x, 0f, z);
            p.y = GroundY(t, p) + lift;
            return p;
        }

        // ------------------------------------------------------------------ Prefabs de vegetación y rocas

        static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

        static GameObject TreePrefab(int variant, Mats m)
        {
            string name = "HollowConifer_" + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            var shapes = new[]
            {
                new ChunkyMeshGenerator.TreeShape { height = 7.5f, radius = 2.3f, tiers = 4, trunkRadius = 0.28f, lobes = 5 },
                new ChunkyMeshGenerator.TreeShape { height = 9.5f, radius = 2.6f, tiers = 5, trunkRadius = 0.32f, lobes = 6 },
                new ChunkyMeshGenerator.TreeShape { height = 6f, radius = 2.0f, tiers = 3, trunkRadius = 0.24f, lobes = 4 },
                new ChunkyMeshGenerator.TreeShape { height = 11f, radius = 2.8f, tiers = 5, trunkRadius = 0.36f, lobes = 7 },
                new ChunkyMeshGenerator.TreeShape { height = 8.5f, radius = 2.1f, tiers = 4, trunkRadius = 0.27f, lobes = 5 },
            };
            var s = shapes[variant % shapes.Length];
            var mesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Conifer(name, 100 + variant * 17, s));
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { m.bark, m.needles };
            mr.receiveGI = ReceiveGI.LightProbes;
            LookDevForestCornerBuilder.AddFadeable(go, new Renderer[] { mr }, new[] { m.bark, m.needles });
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = s.trunkRadius + 0.12f;
            col.height = 4f;
            col.center = Vector3.up * 2f;
            var canopy = new GameObject("Canopy");
            canopy.layer = LookDevForestCornerBuilder.EnsureLayer(11, "Canopy");
            canopy.transform.SetParent(go.transform, false);
            var cc = canopy.AddComponent<CapsuleCollider>();
            cc.isTrigger = true;
            cc.radius = s.radius * 0.7f;
            cc.height = s.height * 0.75f;
            cc.center = Vector3.up * (s.height * 0.22f + cc.height * 0.5f);
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.01f, new Renderer[] { mr }) });
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        static GameObject RoundTreePrefab(int variant, Mats m)
        {
            string name = "HollowRoundTree_" + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            float h = 5.5f + variant * 0.8f;
            var trunkMesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Log(name + "_Trunk", h * 0.62f, 0.22f + variant * 0.03f, 8));
            var crownMesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Blob(name + "_Crown", 300 + variant * 13, 2.2f + variant * 0.3f, 6, 2.6f + variant * 0.3f));
            var go = new GameObject(name);
            var trunk = new GameObject("Trunk");
            trunk.transform.SetParent(go.transform, false);
            trunk.transform.localPosition = Vector3.up * h * 0.31f;
            trunk.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            trunk.AddComponent<MeshFilter>().sharedMesh = trunkMesh;
            var tr = trunk.AddComponent<MeshRenderer>();
            tr.sharedMaterials = new[] { m.bark, m.cutWood };
            var crown = new GameObject("Crown");
            crown.transform.SetParent(go.transform, false);
            crown.transform.localPosition = Vector3.up * (h * 0.5f);
            crown.AddComponent<MeshFilter>().sharedMesh = crownMesh;
            var cr = crown.AddComponent<MeshRenderer>();
            cr.sharedMaterial = m.leaves;
            tr.receiveGI = ReceiveGI.LightProbes;
            cr.receiveGI = ReceiveGI.LightProbes;
            LookDevForestCornerBuilder.AddFadeable(go, new Renderer[] { tr, cr }, new[] { m.bark, m.leaves });
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.35f;
            col.height = 4f;
            col.center = Vector3.up * 2f;
            var canopy = new GameObject("Canopy");
            canopy.layer = 11;
            canopy.transform.SetParent(go.transform, false);
            var sc = canopy.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2f + variant * 0.25f;
            sc.center = Vector3.up * (h * 0.5f + 1.2f);
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.01f, new Renderer[] { tr, cr }) });
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        /// <summary>
        /// Arbusto escondite: trigger en TallGrass (los Lurker te ven peor) y otro en Canopy de su altura
        /// (agachado dentro, el reflector del OVNI no te alcanza; de pie la cabeza asoma).
        /// </summary>
        static GameObject BushPrefab(int variant, Mats m)
        {
            string name = "HollowBush_" + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            float r = 1.15f + variant * 0.2f;
            const float height = 1.25f;
            var mesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Blob(name, 500 + variant * 7, r, 5, height));
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.leaves;
            mr.receiveGI = ReceiveGI.LightProbes;
            mr.shadowCastingMode = ShadowCastingMode.On;

            var hide = new GameObject("Concealment");
            hide.layer = 8; // TallGrass
            hide.transform.SetParent(go.transform, false);
            var hc = hide.AddComponent<CapsuleCollider>();
            hc.isTrigger = true;
            hc.radius = r * 0.85f;
            hc.height = 2f;
            hc.center = Vector3.up * 0.5f;

            var shade = new GameObject("Canopy");
            shade.layer = 11;
            shade.transform.SetParent(go.transform, false);
            var bc = shade.AddComponent<SphereCollider>();
            bc.isTrigger = true;
            bc.radius = r * 0.9f;
            bc.center = Vector3.up * (height - r * 0.9f + 0.05f);

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        static GameObject RockPrefab(int variant, bool cliff, Mats m)
        {
            string name = (cliff ? "HollowCliff_" : "HollowRock_") + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            Vector3 size = cliff ? new Vector3(3.2f + variant * 0.4f, 2.6f + (variant % 2) * 0.8f, 2.6f + variant * 0.3f)
                                 : new Vector3(1.6f + variant * 0.35f, 1.2f + (variant % 3) * 0.35f, 1.4f + variant * 0.25f);
            var mesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Rock(name, 700 + variant * 31 + (cliff ? 1000 : 0), size, cliff ? 0.7f : 0.15f, cliff ? 0.22f : 0.3f));
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.rock;
            mr.receiveGI = ReceiveGI.LightProbes;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
            if (size.y > 1.6f) LookDevForestCornerBuilder.AddFadeable(go, new Renderer[] { mr }, new[] { m.rock });
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        internal static GameObject Spawn(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ------------------------------------------------------------------ Dispersión

        static bool Reserved(Vector3 p, List<Vector3> blockers, float trailClear)
        {
            if (DistanceToTrails(p) < trailClear) return true;
            if (Vector3.Distance(Flat(p), CabinCenter) < 9f) return true;
            if (Vector3.Distance(Flat(p), Meadow) < MeadowRadius) return true;
            if (Vector3.Distance(Flat(p), Camp) < 8f) return true;
            if (Mathf.Abs(p.x - RoadX) < RoadHalf + 2.5f) return true;
            if (Mathf.Abs(p.x - RavineX(p.z)) < 4.5f) return true;
            foreach (var b in blockers) if ((Flat(b) - Flat(p)).sqrMagnitude < 9f) return true;
            return false;
        }

        static int ScatterTrees(Transform root, Terrain t, Mats m, System.Random rng, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var parent = new GameObject("Trees").transform;
            parent.SetParent(root, false);
            var placed = new List<Vector3>();
            for (int tries = 0; tries < 30000 && placed.Count < 300; tries++)
            {
                var p = new Vector3(R(-68f, 66f), 0f, R(-44f, 44f));
                if (Reserved(p, blockers, 3.2f)) continue;
                float e = EdgeDistance(p);
                if (e < -9f) continue;
                // Más denso en el bosque del norte (ruta cubierta) y junto a los bordes; ralo en la meseta.
                float density = 0.45f;
                if (p.z > 14f && p.x > -10f && p.x < 32f) density = 0.95f;
                if (e < 6f) density = 0.9f;
                if (PlateauMask(p) > 0.5f) density = 0.55f;
                if (rng.NextDouble() > density) continue;
                float minGap = p.z > 14f ? 3.4f : 4.2f;
                bool close = false;
                foreach (var q in placed) if ((q - p).sqrMagnitude < minGap * minGap) { close = true; break; }
                if (close) continue;

                p.y = GroundY(t, p) - 0.1f;
                bool round = rng.NextDouble() < 0.18;
                var prefab = round ? RoundTreePrefab(rng.Next(3), m) : TreePrefab(rng.Next(5), m);
                Spawn(prefab, parent, p, R(0f, 360f), R(0.85f, 1.2f));
                placed.Add(p);
            }
            return placed.Count;
        }

        static int ScatterRocks(Transform root, Terrain t, Mats m, System.Random rng, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var parent = new GameObject("Rocks").transform;
            parent.SetParent(root, false);
            int count = 0;

            // Bloques de acantilado sobre las paredes empinadas (lectura de terrazas).
            for (float x = -70f; x <= 70f; x += 3.2f)
                for (float z = -46f; z <= 46f; z += 3.2f)
                {
                    var p = new Vector3(x + R(-1.2f, 1.2f), 0f, z + R(-1.2f, 1.2f));
                    float nx = (p.x + SizeX * 0.5f) / SizeX, nz = (p.z + SizeZ * 0.5f) / SizeZ;
                    if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
                    float steep = t.terrainData.GetSteepness(nx, nz);
                    if (steep < 38f) continue;
                    if (Mathf.Abs(p.x - RavineX(p.z)) < 3.6f) continue; // el barranco queda con la textura de roca
                    if (rng.NextDouble() > 0.55) continue;
                    p.y = GroundY(t, p) - R(0.6f, 1.2f);
                    Spawn(RockPrefab(rng.Next(4), true, m), parent, p, R(0f, 360f), R(0.9f, 1.4f));
                    count++;
                }

            // Cantos sueltos.
            for (int tries = 0; tries < 4000 && count < 260; tries++)
            {
                var p = new Vector3(R(-62f, 60f), 0f, R(-38f, 38f));
                if (Reserved(p, blockers, 2.5f)) continue;
                if (rng.NextDouble() > 0.35) continue;
                p.y = GroundY(t, p) - 0.15f;
                Spawn(RockPrefab(rng.Next(4), false, m), parent, p, R(0f, 360f), R(0.6f, 1.3f));
                count++;
            }

            // Cobertura deliberada en el prado del OVNI: rocas para agacharse detrás de la luz.
            var meadowCover = new[] { new Vector2(-6, 2), new Vector2(-2, -5), new Vector2(3, 4), new Vector2(7, -3), new Vector2(-8, -6), new Vector2(10, 5), new Vector2(1, -9) };
            foreach (var c in meadowCover)
            {
                var p = OnGround(t, Meadow.x + c.x, Meadow.z + c.y, -0.15f);
                Spawn(RockPrefab(rng.Next(4), false, m), parent, p, R(0f, 360f), R(1.1f, 1.35f));
                count++;
            }
            return count;
        }

        static int PlaceBushes(Transform root, Terrain t, Mats m, System.Random rng, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var parent = new GameObject("Bushes").transform;
            parent.SetParent(root, false);
            // Escondites deliberados: junto al sendero, bordes del prado, ruta norte y campamento.
            var spots = new List<Vector2>
            {
                new Vector2(-49, -10), new Vector2(-42, -1), new Vector2(-34, -12), new Vector2(-26, -2), new Vector2(-20, -10),
                new Vector2(-4, -3), new Vector2(-1, -14), new Vector2(4, -26), new Vector2(15, -24), new Vector2(19, -6), new Vector2(26, -18),
                new Vector2(-24, 10), new Vector2(-20, 20), new Vector2(-4, 28), new Vector2(3, 21), new Vector2(8, 29), new Vector2(14, 20),
                new Vector2(18, 27), new Vector2(24, 16), new Vector2(30, 16),
                new Vector2(29, 2), new Vector2(39, 9), new Vector2(38, -3), new Vector2(44, 7), new Vector2(46, -2), new Vector2(31, -11),
            };
            int count = 0;
            foreach (var s in spots)
            {
                var p = OnGround(t, s.x + R(-0.8f, 0.8f), s.y + R(-0.8f, 0.8f), -0.05f);
                Spawn(BushPrefab(rng.Next(4), m), parent, p, R(0f, 360f), R(0.95f, 1.2f));
                count++;
            }
            for (int tries = 0; tries < 3000 && count < 95; tries++)
            {
                var p = new Vector3(R(-62f, 60f), 0f, R(-38f, 38f));
                if (Reserved(p, blockers, 1.8f) && !(DistanceToTrails(p) > 1.8f && Vector3.Distance(Flat(p), Meadow) > MeadowRadius - 1f && Vector3.Distance(Flat(p), Meadow) < MeadowRadius + 2f)) continue;
                if (EdgeDistance(p) < 1f) continue;
                if (rng.NextDouble() > 0.4) continue;
                p.y = GroundY(t, p) - 0.05f;
                Spawn(BushPrefab(rng.Next(4), m), parent, p, R(0f, 360f), R(0.8f, 1.15f));
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ Pasto

        static int PlaceGrass(Transform root, Terrain t, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var mat = Simple("Hollow_Grass", ChunkyMeshGenerator.GrassTexture(5), null, Color.white, 0.08f);
            mat.SetFloat("_Cull", 0f);
            mat.enableInstancing = true;
            var tallMat = Simple("Hollow_TallGrass", mat.GetTexture("_BaseMap") as Texture2D, null, new Color(1f, 0.9f, 0.66f), 0.08f);
            tallMat.SetFloat("_Cull", 0f);
            tallMat.enableInstancing = true;
            var shortMeshes = new Mesh[4];
            for (int i = 0; i < shortMeshes.Length; i++)
                shortMeshes[i] = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.GrassTuft("GrassShort_" + i, 900 + i, 0.4f + i * 0.05f, 10, 0.2f));
            var tallMeshes = new Mesh[2];
            for (int i = 0; i < tallMeshes.Length; i++)
                tallMeshes[i] = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.GrassTuft("GrassTall_" + i, 950 + i, 1.05f + i * 0.1f, 20, 0.35f));

            var parent = new GameObject("Grass").transform;
            parent.SetParent(root, false);
            GameObject Tuft(Mesh mesh, Vector3 p, float scale, Material m)
            {
                var go = new GameObject("Tuft");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, R(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveGI = ReceiveGI.LightProbes;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                return go;
            }

            int count = 0;
            for (int tries = 0; tries < 9000 && count < 2200; tries++)
            {
                var p = new Vector3(R(-62f, 60f), 0f, R(-38f, 38f));
                bool meadow = Vector3.Distance(Flat(p), Meadow) < MeadowRadius + 2f;
                if (DistanceToTrails(p) < 1.4f || Vector3.Distance(Flat(p), CabinCenter) < 4.5f) continue;
                if (Mathf.Abs(p.x - RoadX) < RoadHalf + 0.5f || Mathf.Abs(p.x - RavineX(p.z)) < 3.8f) continue;
                if (EdgeDistance(p) < 0f) continue;
                if (!meadow && rng.NextDouble() > 0.55) continue;
                // Matas agrupadas (se leen mejor desde la cámara alta que sueltas).
                int clump = rng.Next(3, 6);
                for (int k = 0; k < clump; k++)
                {
                    var q = new Vector3(p.x + R(-0.5f, 0.5f), 0f, p.z + R(-0.5f, 0.5f));
                    q.y = GroundY(t, q) - 0.02f;
                    Tuft(shortMeshes[rng.Next(shortMeshes.Length)], q, R(1.1f, 1.6f), mat);
                    count++;
                }
            }

            // Pastizales altos: escondites (TallGrass) y, agachado, sombra frente al reflector (Canopy a su altura).
            var patches = new[] { new Vector2(-3, 6), new Vector2(6, -7), new Vector2(-9, 1), new Vector2(10, 2) };
            var extra = new[] { new Vector2(-10, 28), new Vector2(12, 30), new Vector2(22, 25), new Vector2(24, -2), new Vector2(40, -8), new Vector2(-21, -12), new Vector2(-34, 4) };
            var all = new List<Vector2>();
            foreach (var c in patches) all.Add(new Vector2(Meadow.x + c.x, Meadow.z + c.y));
            all.AddRange(extra);
            foreach (var c in all)
            {
                var center = OnGround(t, c.x, c.y);
                var patch = new GameObject("TallGrassPatch");
                patch.transform.SetParent(parent, false);
                patch.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, R(0f, 360f), 0f));
                for (int i = 0; i < 46; i++)
                {
                    Vector2 o = Random.insideUnitCircle * 2.2f;
                    var p = OnGround(t, c.x + o.x, c.y + o.y, -0.02f);
                    Tuft(tallMeshes[rng.Next(tallMeshes.Length)], p, R(0.85f, 1.15f), tallMat).transform.SetParent(patch.transform, true);
                }
                var hide = new GameObject("Concealment");
                hide.layer = 8;
                hide.transform.SetParent(patch.transform, false);
                var hc = hide.AddComponent<CapsuleCollider>();
                hc.isTrigger = true;
                hc.radius = 2.1f;
                hc.height = 2.5f;
                hc.center = Vector3.up * 0.8f;
                var shade = new GameObject("Canopy");
                shade.layer = 11;
                shade.transform.SetParent(patch.transform, false);
                var bc = shade.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(3.6f, 1.05f, 3.6f);
                bc.center = Vector3.up * 0.52f;
                count += 46;
            }
            return count;
        }

        // ------------------------------------------------------------------ Límites, luz, post

        static void BuildBounds(Transform root, Terrain t)
        {
            var parent = new GameObject("Bounds (invisible)").transform;
            parent.SetParent(root, false);
            void Wall(string n, Vector3 pos, Vector3 size, float yaw = 0f)
            {
                var go = new GameObject(n);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                go.AddComponent<BoxCollider>().size = size;
            }
            Wall("N", new Vector3(0f, 6f, 41f), new Vector3(150f, 14f, 1f));
            Wall("S", new Vector3(0f, 6f, -41f), new Vector3(150f, 14f, 1f));
            Wall("W", new Vector3(-65f, 6f, 0f), new Vector3(1f, 14f, 100f));
            Wall("E", new Vector3(62f, 6f, 0f), new Vector3(1f, 14f, 100f));

            // Orillas del arroyo: muros invisibles salvo en el puente y en el tronco caído.
            for (float z = -40f; z < 40f; z += 2f)
            {
                float zc = z + 1f;
                if (Mathf.Abs(zc - BridgeZ) < 2.2f || Mathf.Abs(zc - LogZ) < 1.4f) continue;
                float x = RavineX(zc);
                float slope = Mathf.Atan(1.5f * 0.07f * Mathf.Cos(zc * 0.07f)) * Mathf.Rad2Deg;
                Wall("BankW", new Vector3(x - 3.4f, 1.5f, zc), new Vector3(0.6f, 6f, 2.3f), -slope);
                Wall("BankE", new Vector3(x + 3.4f, 1.5f, zc), new Vector3(0.6f, 6f, 2.3f), -slope);
            }
            // Flancos de los cruces: cierran el hueco entre los muros de orilla y el ancho del puente/tronco.
            void Flank(float z0, float halfWidth, float gap)
            {
                float x = RavineX(z0);
                float len = gap - halfWidth;
                foreach (float s in new[] { -1f, 1f })
                    foreach (float side in new[] { -1f, 1f })
                        Wall("CrossingFlank", new Vector3(x + side * 3.4f, 1.5f, z0 + s * (halfWidth + len * 0.5f)), new Vector3(0.6f, 6f, len + 0.2f));
            }
            Flank(BridgeZ, 1.05f, 2.0f);
            Flank(LogZ, 0.6f, 2.0f);
        }

        static void TunePostFx()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Art/LookDev/LookDev_PostFX.asset");
            if (profile == null) return;
            if (!profile.TryGet(out DepthOfField dof)) { dof = profile.Add<DepthOfField>(true); AssetDatabase.AddObjectToAsset(dof, profile); }
            // Desenfoque gaussiano de fondo: lectura de diorama (tilt-shift suave), barato en móvil.
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(24f);
            dof.gaussianEnd.Override(48f);
            dof.gaussianMaxRadius.Override(1f);
            dof.highQualitySampling.Override(false);
            if (profile.TryGet(out ColorAdjustments ca)) { ca.postExposure.Override(1.05f); ca.saturation.Override(-6f); ca.contrast.Override(16f); }
            EditorUtility.SetDirty(profile);
        }

        static int BuildLightProbes(Terrain t)
        {
            var go = new GameObject("LightProbes");
            var group = go.AddComponent<LightProbeGroup>();
            var pts = new List<Vector3>();
            for (float x = -62f; x <= 60f; x += 8f)
                for (float z = -38f; z <= 38f; z += 8f)
                {
                    float y = GroundY(t, new Vector3(x, 0f, z));
                    pts.Add(new Vector3(x, y + 0.7f, z));
                    pts.Add(new Vector3(x, y + 4f, z));
                }
            group.probePositions = pts.ToArray();
            return pts.Count;
        }

        // ------------------------------------------------------------------ Captura para revisar

        public static string Capture(string folder, string tag, Vector3 focus, float height, float pitch, float yaw)
        {
            var cam = Camera.main;
            var go = new GameObject("TmpCaptureCam");
            var c2 = go.AddComponent<Camera>();
            c2.CopyFrom(cam);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            go.transform.SetPositionAndRotation(focus - rot * Vector3.forward * height, rot);
            c2.fieldOfView = 45f;
            LookDevForestCornerBuilder.Render(c2, folder + "/" + tag + ".png");
            Object.DestroyImmediate(go);
            return "tris=" + UnityStats.triangles + " batches=" + UnityStats.batches;
        }
    }
}
