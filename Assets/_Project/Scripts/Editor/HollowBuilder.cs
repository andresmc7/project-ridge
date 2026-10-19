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
    /// Nivel completo "The Hollow" (v3): refugio → cinco salas de sigilo (reflectores rojos y Lurkers con patrones
    /// fijos, pastizales escondite) → camioneta con la radio (clímax y final). Ver HollowLayout para el diseño.
    /// Estilo: diorama oscuro (Echoes of Wisdom + Alan Wake): geometría chunky y legible, cámara al norte, ningún
    /// objeto tapa el suelo caminable (regla de oclusión), luz roja y niebla.
    /// Regenerable: reconstruye todo lo generado a partir de la escena del look dev (jugador, cámara, HUD, Lurker).
    /// </summary>
    public static partial class HollowBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/TheHollow.unity";
        const string SourceScene = "Assets/_Project/Scenes/LookDev_ForestCorner.unity";
        const string DataDir = "Assets/_Project/Art/Hollow";
        const string PrefabDir = "Assets/_Project/Prefabs/Hollow";
        const int Seed = 4127;

        // ------------------------------------------------------------------ Entrada

        [MenuItem("Project Ridge/The Hollow/Build Level")]
        public static string Build()
        {
            var log = new System.Text.StringBuilder();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                AssetDatabase.CopyAsset(SourceScene, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var n in new[] { "LookDevEnvironment", "Items", "LurkerPatrol", "LurkerPatrol_B", "LurkerPatrol_C", "LurkerPatrol_D", "UFO", "UFOScanPoints",
                                      "LightProbes", "Moon", "PostFX", "PlayerSpawn", "Hollow", "Lurker_B", "Lurker_C", "Lurker_D", "Checkpoints", "Searchlights" })
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
            log.AppendLine(BuildSearchlights(terrain));
            SetupCamera();

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

        /// <summary>Cámara de diorama: mira al norte (como Zelda), alta y fija. Sin oclusión que desvanecer.</summary>
        static void SetupCamera()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ProjectRidge.CameraRig.IsoCameraConfig>("Assets/_Project/Data/IsoCameraConfig.asset");
            if (cfg == null) return;
            cfg.yaw = 0f;
            cfg.pitch = 55f;
            cfg.distance = 14f;
            cfg.fieldOfView = 42f;
            cfg.positionDamping = 0.25f;
            EditorUtility.SetDirty(cfg);
        }

        // ------------------------------------------------------------------ Materiales

        internal class Mats
        {
            public Material ground, bark, needles, leaves, rock, planks, metal, water, cutWood, darkMetal, fabric, glowLantern, rope, barkWind, bush, leavesTree;
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
            m.barkWind = WindMaterial("Hollow_BarkWind", bark.Albedo, bark.Normal, Color.white, 0.25f, 0.12f, 0.18f, 0f, false, 0f);
            var fabric = PolyHavenMaterialBuilder.BuildTextureSet(tex + "/denim_fabric", "denim_fabric");
            m.fabric = Tinted(PolyHavenMaterialBuilder.BuildLitMaterial("denim_fabric", fabric, 0.6f), "Hollow_TentFabric", new Color(0.55f, 0.6f, 0.45f));

            var (needleA, needleN) = ChunkyMeshGenerator.PatternTextures(ChunkyMeshGenerator.Pattern.Needles, 11);
            m.needles = WindMaterial("Hollow_NeedlesWind", needleA, needleN, new Color(1f, 1.05f, 0.95f), 0.15f, 0.12f, 0.18f, 0.02f, false, 0.25f);
            var (leafA, leafN) = ChunkyMeshGenerator.PatternTextures(ChunkyMeshGenerator.Pattern.Leaves, 23);
            m.leaves = Simple("Hollow_Leaves", leafA, leafN, new Color(1f, 1.02f, 0.92f), 0.2f);
            m.bush = WindMaterial("Hollow_BushWind", leafA, leafN, new Color(1f, 1.02f, 0.92f), 0.22f, 0.05f, 0.9f, 0.015f, false, 0.35f);
            m.leavesTree = WindMaterial("Hollow_LeavesWind", leafA, leafN, new Color(1f, 1.02f, 0.92f), 0.2f, 0.5f, 0.35f, 0.02f, false, 0.3f);
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

        static Vector3 TerrainPoint(float nx, float nz) => new Vector3(TerrainMin.x + nx * SizeX, 0f, TerrainMin.y + nz * SizeZ);

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
                    var w = TerrainPoint(x / (float)(HeightRes - 1), z / (float)(HeightRes - 1));
                    heights[z, x] = Mathf.Clamp01((HeightAt(w) + Base) / MaxHeight);
                }
            td.SetHeights(0, 0, heights);

            td.terrainLayers = new[] { m.tlGround, m.tlTrail, m.tlLeaves, m.tlRock };
            var alphas = new float[AlphaRes, AlphaRes, 4];
            for (int z = 0; z < AlphaRes; z++)
                for (int x = 0; x < AlphaRes; x++)
                {
                    float nx = x / (float)(AlphaRes - 1), nz = z / (float)(AlphaRes - 1);
                    var w = TerrainPoint(nx, nz);
                    float steep = td.GetSteepness(nx, nz);
                    float rock = Smooth(24f, 38f, steep);
                    float trail = Smooth(2.2f, 1.0f, DistanceToTrails(w));
                    trail = Mathf.Max(trail, Smooth(RoadHalf + 0.8f, RoadHalf - 0.6f, Mathf.Abs(w.x - RoadX)) * Smooth(0.5f, -0.5f, Walk(w)));
                    trail = Mathf.Max(trail, Smooth(5f, 2.5f, Vector3.Distance(Flat(w), Camp)) * 0.7f);
                    trail = Mathf.Max(trail, Smooth(1.5f, 0.6f, Mathf.Abs(w.x - RavineX(w.z))));
                    // Fuera de las salas, hojarasca oscura: el escenario jugable se lee como un claro.
                    float leaves = Mathf.Max(Smooth(0.5f, 0.68f, Mathf.PerlinNoise(w.x * 0.08f + 40f, w.z * 0.08f + 13f)), Smooth(0.5f, 2.5f, Walk(w)) * 0.85f);
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
            go.transform.position = new Vector3(TerrainMin.x, -Base, TerrainMin.y);
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

        static readonly ChunkyMeshGenerator.TreeShape[] ConiferShapes =
        {
            new ChunkyMeshGenerator.TreeShape { height = 7.5f, radius = 2.3f, tiers = 4, trunkRadius = 0.28f, lobes = 5 },
            new ChunkyMeshGenerator.TreeShape { height = 9.5f, radius = 2.6f, tiers = 5, trunkRadius = 0.32f, lobes = 6 },
            new ChunkyMeshGenerator.TreeShape { height = 6f, radius = 2.0f, tiers = 3, trunkRadius = 0.24f, lobes = 4 },
            new ChunkyMeshGenerator.TreeShape { height = 11f, radius = 2.8f, tiers = 5, trunkRadius = 0.36f, lobes = 7 },
            new ChunkyMeshGenerator.TreeShape { height = 8.5f, radius = 2.1f, tiers = 4, trunkRadius = 0.27f, lobes = 5 },
        };

        static float RoundTreeHeight(int variant) => 5.5f + variant * 0.8f;
        static float RoundTreeTop(int variant) => RoundTreeHeight(variant) * 0.5f + 2.6f + variant * 0.3f;
        static float RoundTreeRadius(int variant) => 2.2f + variant * 0.3f;

        static GameObject TreePrefab(int variant, Mats m)
        {
            string name = "HollowConifer_" + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            var s = ConiferShapes[variant % ConiferShapes.Length];
            var mesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Conifer(name, 100 + variant * 17, s));
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { m.barkWind, m.needles };
            mr.receiveGI = ReceiveGI.LightProbes;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = s.trunkRadius + 0.12f;
            col.height = 4f;
            col.center = Vector3.up * 2f;
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.01f, new Renderer[] { mr }) });
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic); // sin static batching: el viento usa el espacio de objeto
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        static GameObject RoundTreePrefab(int variant, Mats m)
        {
            string name = "HollowRoundTree_" + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            float h = RoundTreeHeight(variant);
            var trunkMesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Log(name + "_Trunk", h * 0.62f, 0.22f + variant * 0.03f, 8));
            var crownMesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Blob(name + "_Crown", 300 + variant * 13, RoundTreeRadius(variant), 6, 2.6f + variant * 0.3f));
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
            cr.sharedMaterial = m.leavesTree;
            tr.receiveGI = ReceiveGI.LightProbes;
            cr.receiveGI = ReceiveGI.LightProbes;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.35f;
            col.height = 4f;
            col.center = Vector3.up * 2f;
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.01f, new Renderer[] { tr, cr }) });
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        /// <summary>Arbusto decorativo de borde (fuera del área jugable; los escondites son los pastizales).</summary>
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
            mr.sharedMaterial = m.bush;
            mr.receiveGI = ReceiveGI.LightProbes;
            mr.shadowCastingMode = ShadowCastingMode.On;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        static Vector3 RockSize(int variant, bool cliff) => cliff
            ? new Vector3(3.2f + variant * 0.4f, 2.6f + (variant % 2) * 0.8f, 2.6f + variant * 0.3f)
            : new Vector3(1.6f + variant * 0.35f, 1.2f + (variant % 3) * 0.35f, 1.4f + variant * 0.25f);

        static GameObject RockPrefab(int variant, bool cliff, Mats m)
        {
            string name = (cliff ? "HollowCliff_" : "HollowRock_") + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            Vector3 size = RockSize(variant, cliff);
            var mesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Rock(name, 700 + variant * 31 + (cliff ? 1000 : 0), size, cliff ? 0.7f : 0.15f, cliff ? 0.22f : 0.3f));
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.rock;
            mr.receiveGI = ReceiveGI.LightProbes;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
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

        // ------------------------------------------------------------------ Dispersión (con la regla de oclusión)

        /// <summary>¿Cabe algo de esta altura total (sobre el piso de las salas) y este radio aquí sin tapar suelo caminable?</summary>
        static bool Fits(Vector3 p, float topY, float radius) => topY <= MaxHeightAt(p, radius);

        static int ScatterTrees(Transform root, Terrain t, Mats m, System.Random rng, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var parent = new GameObject("Trees").transform;
            parent.SetParent(root, false);
            var placed = new List<Vector3>();
            for (int tries = 0; tries < 60000 && placed.Count < 520; tries++)
            {
                var p = new Vector3(R(TerrainMin.x + 2f, TerrainMin.x + SizeX - 2f), 0f, R(TerrainMin.y + 2f, TerrainMin.y + SizeZ - 2f));
                bool round = rng.NextDouble() < 0.4;
                int variant = round ? rng.Next(3) : rng.Next(ConiferShapes.Length);
                float scale = R(0.85f, 1.2f);
                float radius = (round ? RoundTreeRadius(variant) : ConiferShapes[variant].radius) * scale;
                if (Reserved(p, blockers, radius + 0.6f)) continue;
                p.y = GroundY(t, p) - 0.1f;
                float top = p.y + (round ? RoundTreeTop(variant) : ConiferShapes[variant].height) * scale;
                if (!Fits(p, top, radius)) continue;
                float minGap = 2.4f;
                bool close = false;
                foreach (var q in placed) if ((Flat(q) - Flat(p)).sqrMagnitude < minGap * minGap) { close = true; break; }
                if (close) continue;
                Spawn(round ? RoundTreePrefab(variant, m) : TreePrefab(variant, m), parent, p, R(0f, 360f), scale);
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

            // Bloques de acantilado sobre las paredes empinadas (lectura de terrazas), solo donde no tapan.
            for (float x = TerrainMin.x + 1f; x <= TerrainMin.x + SizeX - 1f; x += 2.6f)
                for (float z = TerrainMin.y + 1f; z <= TerrainMin.y + SizeZ - 1f; z += 2.6f)
                {
                    var p = new Vector3(x + R(-1.1f, 1.1f), 0f, z + R(-1.1f, 1.1f));
                    float nx = (p.x - TerrainMin.x) / SizeX, nz = (p.z - TerrainMin.y) / SizeZ;
                    if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
                    if (t.terrainData.GetSteepness(nx, nz) < 36f) continue;
                    if (Mathf.Abs(p.x - RavineX(p.z)) < 3.6f) continue; // el barranco queda con la textura de roca
                    if (rng.NextDouble() > 0.6) continue;
                    int v = rng.Next(4);
                    float scale = R(0.9f, 1.4f);
                    float sink = R(0.5f, 1.1f);
                    p.y = GroundY(t, p) - sink;
                    if (!Fits(p, p.y + RockSize(v, true).y * scale, RockSize(v, true).x * 0.5f * scale)) continue;
                    Spawn(RockPrefab(v, true, m), parent, p, R(0f, 360f), scale);
                    count++;
                }

            // Cantos sueltos fuera de las salas.
            for (int tries = 0; tries < 6000 && count < 420; tries++)
            {
                var p = new Vector3(R(TerrainMin.x + 2f, TerrainMin.x + SizeX - 2f), 0f, R(TerrainMin.y + 2f, TerrainMin.y + SizeZ - 2f));
                if (Reserved(p, blockers, 0.8f)) continue;
                if (rng.NextDouble() > 0.4) continue;
                int v = rng.Next(4);
                float scale = R(0.6f, 1.3f);
                p.y = GroundY(t, p) - 0.15f;
                if (!Fits(p, p.y + RockSize(v, false).y * scale, RockSize(v, false).x * 0.5f * scale)) continue;
                Spawn(RockPrefab(v, false, m), parent, p, R(0f, 360f), scale);
                count++;
            }

            // Cobertura de diseño dentro de las salas: agachado detrás, el Lurker no te ve; de pie, sí.
            var cover = new GameObject("CoverRocks").transform;
            cover.SetParent(parent, false);
            foreach (var c in CoverRocks)
            {
                var p = OnGround(t, c.x, c.y, -0.15f);
                Spawn(RockPrefab(rng.Next(2) == 0 ? 0 : 3, false, m), cover, p, R(0f, 360f), 1.15f * c.z);
                blockers.Add(p);
                count++;
            }
            return count;
        }

        static int PlaceBushes(Transform root, Terrain t, Mats m, System.Random rng, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var parent = new GameObject("Bushes").transform;
            parent.SetParent(root, false);
            int count = 0;
            // Borde de las salas: arbustos que dibujan el contorno del escenario (como los setos de un diorama).
            for (int tries = 0; tries < 9000 && count < 260; tries++)
            {
                var p = new Vector3(R(TerrainMin.x + 2f, TerrainMin.x + SizeX - 2f), 0f, R(TerrainMin.y + 2f, TerrainMin.y + SizeZ - 2f));
                float d = Walk(p);
                if (d < 0.6f || d > 4.5f) continue;
                if (Mathf.Abs(p.x - RavineX(p.z)) < 4.5f) continue;
                int v = rng.Next(4);
                float scale = R(0.8f, 1.2f);
                p.y = GroundY(t, p) - 0.05f;
                if (!Fits(p, p.y + 1.25f * scale, (1.15f + v * 0.2f) * scale)) continue;
                Spawn(BushPrefab(v, m), parent, p, R(0f, 360f), scale);
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ Pasto

        static int PlaceGrass(Transform root, Terrain t, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var grassTex = ChunkyMeshGenerator.GrassTexture(5);
            var mat = WindMaterial("Hollow_GrassWind", grassTex, null, new Color(0.78f, 0.86f, 0.72f), 0.08f, 0.06f, 2.5f, 0.03f, true, 0.4f);
            // Pastizal escondite: dorado, alto y denso. Se reconoce de un vistazo (lectura de juego, no decorado).
            var tallMat = WindMaterial("Hollow_TallGrassWind", grassTex, null, new Color(1.6f, 1.22f, 0.48f), 0.08f, 0.1f, 1.3f, 0.03f, true, 0.45f);
            var shortMeshes = new Mesh[4];
            for (int i = 0; i < shortMeshes.Length; i++)
                shortMeshes[i] = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.GrassTuft("GrassShort_" + i, 900 + i, 0.4f + i * 0.05f, 10, 0.2f));
            var tallMeshes = new Mesh[2];
            for (int i = 0; i < tallMeshes.Length; i++)
                tallMeshes[i] = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.GrassTuft("GrassTall_" + i, 950 + i, 1.2f + i * 0.12f, 22, 0.35f));

            var parent = new GameObject("Grass").transform;
            parent.SetParent(root, false);
            mat.SetFloat("_UseVertexHeight", 1f);
            tallMat.SetFloat("_UseVertexHeight", 1f);
            // Las matas se combinan por parcela de 12 m; la altura de cada hoja viaja en UV2.x para el viento.
            var chunks = new Dictionary<(int, int, Material), List<CombineInstance>>();
            void Tuft(Mesh mesh, Vector3 p, float scale, Material m)
            {
                var key = (Mathf.FloorToInt(p.x / 12f), Mathf.FloorToInt(p.z / 12f), m);
                if (!chunks.TryGetValue(key, out var list)) chunks[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(p, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * scale) });
            }

            bool InPatch(Vector3 p, float pad)
            {
                foreach (var h in HidePatches) if ((new Vector2(p.x - h.x, p.z - h.y)).magnitude < h.z + pad) return true;
                return false;
            }

            // Pasto corto dentro de las salas (textura del suelo) y en los bordes; nunca sobre el sendero.
            int count = 0;
            for (int tries = 0; tries < 16000 && count < 2200; tries++)
            {
                var p = new Vector3(R(TerrainMin.x + 2f, TerrainMin.x + SizeX - 2f), 0f, R(TerrainMin.y + 2f, TerrainMin.y + SizeZ - 2f));
                float d = Walk(p);
                if (d > 3f) continue;
                if (DistanceToTrails(p) < 1.3f || Vector3.Distance(Flat(p), CabinCenter) < 4.5f || InPatch(p, 0.6f)) continue;
                if (Mathf.Abs(p.x - RoadX) < RoadHalf + 0.4f && d < 0f) continue;
                if (Mathf.Abs(p.x - RavineX(p.z)) < 3.8f) continue;
                int clump = rng.Next(3, 6);
                for (int k = 0; k < clump; k++)
                {
                    var q = new Vector3(p.x + R(-0.5f, 0.5f), 0f, p.z + R(-0.5f, 0.5f));
                    q.y = GroundY(t, q) - 0.02f;
                    Tuft(shortMeshes[rng.Next(shortMeshes.Length)], q, R(1.0f, 1.45f), mat);
                    count++;
                }
            }

            // Pastizales escondite (diseño): agachado dentro, ni reflectores ni Lurkers te ven.
            foreach (var h in HidePatches)
            {
                var center = OnGround(t, h.x, h.y);
                var patch = new GameObject("HidingGrass");
                patch.transform.SetParent(parent, false);
                patch.transform.SetPositionAndRotation(center, Quaternion.identity);
                int n = Mathf.RoundToInt(22f * h.z * h.z);
                for (int i = 0; i < n; i++)
                {
                    Vector2 o = new Vector2(R(-1f, 1f), R(-1f, 1f));
                    if (o.sqrMagnitude > 1f) { i--; continue; }
                    o *= h.z;
                    var p = OnGround(t, h.x + o.x, h.y + o.y, -0.02f);
                    // Más bajo en el borde: el contorno se lee redondo y limpio desde arriba.
                    float edge = 1f - o.magnitude / h.z;
                    Tuft(tallMeshes[rng.Next(tallMeshes.Length)], p, R(0.8f, 1.05f) * (0.75f + 0.35f * edge), tallMat);
                }
                var hide = new GameObject("Concealment");
                hide.layer = 8; // TallGrass
                hide.transform.SetParent(patch.transform, false);
                var hc = hide.AddComponent<CapsuleCollider>();
                hc.isTrigger = true;
                hc.radius = h.z * 0.92f;
                hc.height = 2.5f;
                hc.center = Vector3.up * 0.8f;
                count += n;
            }

            int chunkIndex = 0;
            foreach (var kv in chunks)
            {
                var mesh = new Mesh { name = "GrassChunk_" + chunkIndex++, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                mesh = ChunkyMeshGenerator.Save(mesh);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key.Item3;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveGI = ReceiveGI.LightProbes;
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
            float cx = TerrainMin.x + SizeX * 0.5f, cz = TerrainMin.y + SizeZ * 0.5f;
            Wall("N", new Vector3(cx, 6f, TerrainMin.y + SizeZ), new Vector3(SizeX, 20f, 1f));
            Wall("S", new Vector3(cx, 6f, TerrainMin.y), new Vector3(SizeX, 20f, 1f));
            Wall("W", new Vector3(TerrainMin.x, 6f, cz), new Vector3(1f, 20f, SizeZ));
            Wall("E", new Vector3(TerrainMin.x + SizeX, 6f, cz), new Vector3(1f, 20f, SizeZ));

            // Contorno del escenario: muros bajos invisibles justo fuera del borde caminable, para que ni el papá ni
            // los Lurker suban por las cornisas (el terreno empinado ya frena, esto lo garantiza).
            for (float x = TerrainMin.x + 1f; x < TerrainMin.x + SizeX - 1f; x += 0.8f)
                for (float z = TerrainMin.y + 1f; z < TerrainMin.y + SizeZ - 1f; z += 0.8f)
                {
                    var p = new Vector3(x, 0f, z);
                    float d = Walk(p);
                    if (d < 0.35f || d > 1.15f) continue;
                    if (Mathf.Abs(p.x - RavineX(p.z)) < 3.4f) continue;
                    Wall("Edge", new Vector3(x, GroundY(t, p) + 1f, z), new Vector3(0.8f, 2.5f, 0.8f));
                }

            // Orillas del arroyo: muros invisibles salvo en el puente.
            for (float z = -16f; z < 16f; z += 2f)
            {
                float zc = z + 1f;
                if (Mathf.Abs(zc - BridgeZ) < 2.2f) continue;
                float x = RavineX(zc);
                float slope = Mathf.Atan(0.6f * 0.2f * Mathf.Cos(zc * 0.2f)) * Mathf.Rad2Deg;
                Wall("BankW", new Vector3(x - 3.4f, 1.5f, zc), new Vector3(0.6f, 6f, 2.3f), -slope);
                Wall("BankE", new Vector3(x + 3.4f, 1.5f, zc), new Vector3(0.6f, 6f, 2.3f), -slope);
            }
            // Flancos del puente: cierran el hueco entre los muros de orilla y el ancho del tablero.
            float bx = RavineX(BridgeZ);
            const float halfWidth = 1.05f, gap = 2.0f, len = gap - halfWidth;
            foreach (float s in new[] { -1f, 1f })
                foreach (float side in new[] { -1f, 1f })
                    Wall("CrossingFlank", new Vector3(bx + side * 3.4f, 1.5f, BridgeZ + s * (halfWidth + len * 0.5f)), new Vector3(0.6f, 6f, len + 0.2f));
        }

        static void TunePostFx()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Art/LookDev/LookDev_PostFX.asset");
            if (profile == null) return;
            if (!profile.TryGet(out DepthOfField dof)) { dof = profile.Add<DepthOfField>(true); AssetDatabase.AddObjectToAsset(dof, profile); }
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.highQualitySampling.Override(false);
            EditorUtility.SetDirty(profile);
        }

        static int BuildLightProbes(Terrain t)
        {
            var go = new GameObject("LightProbes");
            var group = go.AddComponent<LightProbeGroup>();
            var pts = new List<Vector3>();
            for (float x = TerrainMin.x + 3f; x <= TerrainMin.x + SizeX - 3f; x += 4f)
                for (float z = TerrainMin.y + 3f; z <= TerrainMin.y + SizeZ - 3f; z += 4f)
                {
                    var p = new Vector3(x, 0f, z);
                    if (Walk(p) > 6f) continue;
                    float y = GroundY(t, p);
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
