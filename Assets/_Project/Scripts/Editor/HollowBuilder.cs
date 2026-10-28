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
            PaintedTerrainLayers(mats);
            log.AppendLine("materials ok");

            var root = new GameObject("Hollow").transform;
            var terrain = BuildTerrain(root, mats);
            log.AppendLine("terrain ok");
            log.AppendLine("cliff rocks=" + TerraceWalls(root, terrain, new[] { BushPrefab(0, mats), BushPrefab(1, mats), BushPrefab(2, mats), BushPrefab(3, mats) },
                new[] { RockPrefab(0, true, mats), RockPrefab(1, true, mats), RockPrefab(2, true, mats), RockPrefab(3, true, mats) },
                new[] { RockPrefab(0, false, mats), RockPrefab(1, false, mats), RockPrefab(2, false, mats), RockPrefab(3, false, mats) }));

            var rng = new System.Random(Seed);
            var blockers = new List<Vector3>(); // posiciones ocupadas por props construidos (no poner árboles encima)
            int built = BuildLandmarks(root, terrain, mats, blockers);
            SetupWater(root, mats);
            log.AppendLine("landmarks=" + built);
            BuildRoomLandmarks(root, terrain, blockers);
            int trees = ScatterTrees(root, terrain, mats, rng, blockers);
            log.AppendLine("trees=" + trees);
            int rocks = ScatterRocks(root, terrain, mats, rng, blockers);
            log.AppendLine("rocks=" + rocks);
            log.AppendLine("stumps=" + ScatterStumps(root, terrain, rng, blockers));
            int bushes = PlaceBushes(root, terrain, mats, rng, blockers);
            log.AppendLine("bushes=" + bushes);
            // Mapa de densidad de la vegetación escondite (junto a las rocas ya colocadas) y vegetación según el mapa.
            var rockSpots = new List<Vector3>();
            foreach (var n in new[] { "Rocks", "CliffRocks" })
            {
                var g = root.Find(n) ?? GameObject.Find(n)?.transform;
                if (g != null) foreach (Transform c in g) rockSpots.Add(c.position);
            }
            densityMap = BuildDensityMap(terrain, rockSpots);
            log.AppendLine("density map " + densityMap.width + "x" + densityMap.height);
            int grass = PlaceGrass(root, terrain, rng);
            log.AppendLine("dry branches=" + PlaceDryBranches(root, terrain));
            log.AppendLine("grass tufts=" + grass);
            log.AppendLine("ground detail=" + GroundDetail(root, terrain, mats, rng));
            log.AppendLine("debris=" + PlaceDebris(root, terrain, mats));
            log.AppendLine("flowers=" + Flowers(root, terrain));
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

        /// <summary>Cámara de diorama 3/4 (POLISH_PASS 3.6): mira al norte, 42°, cerca; lo que tape al papá se desvanece con dithering.</summary>
        static void SetupCamera()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ProjectRidge.CameraRig.IsoCameraConfig>("Assets/_Project/Data/IsoCameraConfig.asset");
            if (cfg == null) return;
            cfg.yaw = 0f;
            cfg.pitch = 42f;
            cfg.distance = 17.5f; // medido: a 12.5 m el papá de pie ocupaba el 16% del alto; a 17.5 m, ~11% (POLISH_PASS 3.6 pide 10–12%)
            cfg.threatDistance = 20f; // misma proporción que 14.5 / 12.5 del spec
            cfg.fieldOfView = 35f;
            cfg.framingOffset = 1.2f;
            cfg.positionDamping = 0.12f; // cámara pegada al papá: sin deriva flotante
            EditorUtility.SetDirty(cfg);

            // Lo que tape al papá se desvanece (dithering) y el tilt-shift enfoca su altura en pantalla.
            var cam = Camera.main.gameObject;
            var player = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerController>();
            if (!cam.TryGetComponent(out ProjectRidge.CameraRig.CameraOcclusionFader fader)) fader = cam.AddComponent<ProjectRidge.CameraRig.CameraOcclusionFader>();
            var fso = new SerializedObject(fader);
            fso.FindProperty("config").objectReferenceValue = cfg;
            fso.FindProperty("target").objectReferenceValue = player.transform;
            fso.ApplyModifiedPropertiesWithoutUndo();
            if (!cam.TryGetComponent(out ProjectRidge.CameraRig.TiltShiftFocus focus)) focus = cam.AddComponent<ProjectRidge.CameraRig.TiltShiftFocus>();
            var tso = new SerializedObject(focus);
            tso.FindProperty("target").objectReferenceValue = player.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();
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
            m.SetColor("_Color", tint); // si no, URP lo sincroniza al abrir el material y el asset cambia solo
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
                    float leaves = Mathf.Max(Smooth(0.55f, 0.72f, Mathf.PerlinNoise(w.x * 0.08f + 40f, w.z * 0.08f + 13f)), Smooth(3f, 7f, Walk(w)) * 0.7f);
                    // Suelo natural, no césped: manchas de hojarasca y de tierra húmeda dentro de las salas, a dos escalas.
                    leaves = Mathf.Max(leaves, Smooth(0.5f, 0.66f, Mathf.PerlinNoise(w.x * 0.17f + 3f, w.z * 0.17f + 71f)) * 0.8f);
                    leaves = Mathf.Max(leaves, Smooth(0.6f, 0.75f, Mathf.PerlinNoise(w.x * 0.45f + 9f, w.z * 0.45f + 2f)) * 0.55f);
                    trail = Mathf.Max(trail, Smooth(0.62f, 0.74f, Mathf.PerlinNoise(w.x * 0.23f + 51f, w.z * 0.23f + 19f)) * 0.6f);
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

        // Proporciones de diorama (Echoes of Wisdom): árboles bajos y anchos, ~3 veces el papá, copas en capas.
        static readonly ChunkyMeshGenerator.TreeShape[] ConiferShapes =
        {
            new ChunkyMeshGenerator.TreeShape { height = 5.2f, radius = 2.0f, tiers = 3, trunkRadius = 0.3f, lobes = 5 },
            new ChunkyMeshGenerator.TreeShape { height = 6.4f, radius = 2.3f, tiers = 4, trunkRadius = 0.34f, lobes = 6 },
            new ChunkyMeshGenerator.TreeShape { height = 4.4f, radius = 1.7f, tiers = 3, trunkRadius = 0.27f, lobes = 4 },
            new ChunkyMeshGenerator.TreeShape { height = 7.2f, radius = 2.5f, tiers = 4, trunkRadius = 0.38f, lobes = 7 },
            new ChunkyMeshGenerator.TreeShape { height = 5.8f, radius = 2.1f, tiers = 3, trunkRadius = 0.32f, lobes = 5 },
        };

        static float RoundTreeHeight(int variant) => 4.6f + variant * 0.5f;
        static float RoundTreeTop(int variant) => RoundTreeHeight(variant);
        static float RoundTreeRadius(int variant) => 1.9f + variant * 0.25f;

        static readonly StylizedTreeGenerator.Palette ConiferPalette = new StylizedTreeGenerator.Palette
        {
            leafDark = new Color(0.07f, 0.21f, 0.13f), leafLight = new Color(0.27f, 0.52f, 0.27f), core = new Color(0.03f, 0.09f, 0.06f),
            barkDark = new Color(0.1f, 0.05f, 0.035f), barkLight = new Color(0.36f, 0.2f, 0.12f),
        };

        static readonly StylizedTreeGenerator.Palette BroadleafPalette = new StylizedTreeGenerator.Palette
        {
            leafDark = new Color(0.05f, 0.1f, 0.06f), leafLight = new Color(0.17f, 0.25f, 0.12f), core = new Color(0.02f, 0.04f, 0.03f),
            barkDark = new Color(0.1f, 0.06f, 0.04f), barkLight = new Color(0.33f, 0.22f, 0.14f),
        };

        static Material foliageMat;

        /// <summary>Un solo material para toda la vegetación estilizada: color por vértice, viento y borde de luna.</summary>
        static Material FoliageMaterial(float sway)
        {
            string name = sway > 0.08f ? "Hollow_StylizedFoliage" : "Hollow_StylizedTrees";
            var mat = WindMaterial(name, null, null, Color.white, 0.18f, sway, 0.16f, 0.012f, true, 0.3f);
            mat.SetFloat("_UseVertexColor", 1f);
            mat.SetColor("_RimColor", new Color(0.07f, 0.09f, 0.06f)); // borde de luna discreto, sin azul (POLISH_PASS 3.9)
            mat.SetFloat("_RimPower", 3f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject FoliagePrefab(string name, StylizedTreeGenerator.Shape shape, StylizedTreeGenerator.Palette pal, int seed, float sway, float colliderRadius)
        {
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            var mesh = ChunkyMeshGenerator.Save(StylizedTreeGenerator.Build("Styl_" + name, shape, pal, seed)); // nombre propio: no pisar las mallas viejas
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = FoliageMaterial(sway);
            mr.receiveGI = ReceiveGI.LightProbes;
            mr.shadowCastingMode = ShadowCastingMode.On;
            if (colliderRadius > 0f)
            {
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = colliderRadius;
                col.height = 3f;
                col.center = Vector3.up * 1.5f;
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic); // sin static batching: el viento usa el espacio de objeto
            var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            prefabCache[name] = saved;
            return saved;
        }

        static GameObject TreePrefab(int variant, Mats m)
        {
            var s = ConiferShapes[variant % ConiferShapes.Length];
            var shape = new StylizedTreeGenerator.Shape
            {
                tiers = s.tiers, height = s.height, trunkHeight = 0.75f + s.height * 0.05f, trunkRadius = s.trunkRadius,
                baseRadius = s.radius, topRadius = s.radius * 0.38f, round = false, leafLength = 0.82f, leafWidth = 0.66f,
            };
            return FoliagePrefab("HollowConifer_" + variant, shape, ConiferPalette, 100 + variant * 17, 0.06f, s.trunkRadius + 0.15f);
        }

        static GameObject RoundTreePrefab(int variant, Mats m)
        {
            float r = RoundTreeRadius(variant);
            var shape = new StylizedTreeGenerator.Shape
            {
                tiers = 2, height = RoundTreeHeight(variant), trunkHeight = 1.5f, trunkRadius = 0.3f + variant * 0.03f,
                baseRadius = r, topRadius = r * 0.72f, round = true, leafLength = 0.74f, leafWidth = 0.62f,
            };
            return FoliagePrefab("HollowRoundTree_" + variant, shape, BroadleafPalette, 300 + variant * 13, 0.06f, 0.42f);
        }

        /// <summary>
        /// Mata de borde (fuera del área jugable): helechos Poly Haven reducidos, no "blobs" estilizados (POLISH_PASS 3.9:
        /// bordes de sala con vegetación densa).
        /// </summary>
        static GameObject BushPrefab(int variant, Mats m)
        {
            var fern = DecimatedPropPrefab(FernSources[variant % FernSources.Length], "Hollow_FernClump_" + variant, 1600, FernScale, false);
            DarkenFern(fern);
            return fern;
        }

        static readonly string[] FernSources = { "Fern_fern_02_a", "Fern_fern_02_b", "Fern_fern_02_c", "Fern_fern_02_d" };
        /// <summary>Escala de las matas de helecho (el modelo Poly Haven mide ~0.8 m).</summary>
        const float FernScale = 1.7f;

        /// <summary>El helecho fotográfico es de día: más oscuro y menos saturado de noche.</summary>
        static void DarkenFern(GameObject fern)
        {
            foreach (var r in fern.GetComponentsInChildren<MeshRenderer>())
                foreach (var mat in r.sharedMaterials)
                    if (mat != null && mat.HasProperty("_BaseColor")) { mat.SetColor("_BaseColor", new Color(0.42f, 0.48f, 0.36f)); EditorUtility.SetDirty(mat); }
        }

        static Vector3 RockSize(int variant, bool cliff) => cliff
            ? new Vector3(3.2f + variant * 0.4f, 2.6f + (variant % 2) * 0.8f, 2.6f + variant * 0.3f)
            : new Vector3(1.6f + variant * 0.35f, 1.2f + (variant % 3) * 0.35f, 1.4f + variant * 0.25f);

        static GameObject RockPrefab(int variant, bool cliff, Mats m)
        {
            string name = (cliff ? "HollowCliff_" : "HollowRock_") + variant;
            if (prefabCache.TryGetValue(name, out var cached) && cached != null) return cached;
            Vector3 size = RockSize(variant, cliff);
            // Roca Poly Haven (rock_moss_set) reducida a <= 3k triángulos y llevada al tamaño de la maqueta (POLISH_PASS 3.9).
            var sources = cliff ? CliffRockSources : LooseRockSources;
            var mesh = PolyHavenRockMesh(sources[variant % sources.Length], "PH_" + name, size);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MossyRockMaterial();
            mr.receiveGI = ReceiveGI.LightProbes;
            var mc = go.AddComponent<MeshCollider>();
            // Convexo dentro del límite de 256 polígonos, sacado de la malla de 3000: con la visual de 1600 el colisionador
            // cambiaba unos cm y la roca de cobertura de la sala 4 dejaba de tapar al papá en el checkpoint.
            var collisionSource = PolyHavenRockMesh(sources[variant % sources.Length], "PH_" + name + "_ColSrc", size, CollisionSourceTriangles);
            mc.sharedMesh = CollisionMesh(collisionSource);
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
                // Coníferas de tarjetas (POLISH_PASS 3.9): 4 variantes vivas y algún árbol muerto.
                int variant = rng.NextDouble() < 0.12 ? CardConifers.Length - 1 : rng.Next(CardConifers.Length - 1);
                var shape = CardConifers[variant];
                float scale = R(0.85f, 1.15f);
                float radius = (shape.dead ? 0.8f : shape.crownRadius) * scale;
                // Al norte (fondo) pueden pegarse al muro: la copa queda detrás y por encima, no tapa nada; así el bosque
                // entra en cuadro sobre los muros, como en un diorama.
                bool north = float.IsInfinity(SouthClearance(p, 0f));
                if (Reserved(p, blockers, north ? 0.7f + radius * 0.3f : radius + 0.6f)) continue;
                p.y = GroundY(t, p) - 0.1f;
                float top = p.y + shape.height * scale;
                if (!Fits(p, top, radius)) continue;
                float minGap = 2.4f;
                bool close = false;
                foreach (var q in placed) if ((Flat(q) - Flat(p)).sqrMagnitude < minGap * minGap) { close = true; break; }
                if (close) continue;
                var tree = Spawn(CardConiferPrefab(variant), parent, p, R(0f, 360f), scale);
                // Red de seguridad: si el encuadre (mirar adelante, amenaza) lo pone delante del papá, se desvanece con dithering.
                if (!north) MakeFadeable(tree);
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

            // Los escalones de terraza llevan muros de bloques (TerraceWalls); aquí solo cantos sueltos y cobertura.

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
            var mat = WindMaterial("Hollow_GrassWind", grassTex, null, new Color(0.95f, 1.08f, 0.82f), 0.08f, 0.06f, 2.5f, 0.03f, true, 0.4f);
            // Pasto escondite: alto y denso; las puntas captan algo más de luz, cálida y sutil (sin dorado chillón).
            var tallMat = WindMaterial("Hollow_TallGrassWind", grassTex, null, new Color(0.98f, 1.0f, 0.62f), 0.08f, 0.1f, 1.3f, 0.03f, true, 0.45f);
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

            // El pasto corto no se mete en la vegetación escondite (pad = margen extra en metros).
            bool InPatch(Vector3 p, float pad) => densityMap.Sample(p) > 0.18f || densityMap.Sample(p + new Vector3(pad, 0f, 0f)) > 0.3f || densityMap.Sample(p - new Vector3(pad, 0f, 0f)) > 0.3f;

            // Pasto corto dentro de las salas (textura del suelo) y en los bordes; nunca sobre el sendero.
            int count = 0;
            for (int tries = 0; tries < 16000 && count < 2200; tries++)
            {
                var p = new Vector3(R(TerrainMin.x + 2f, TerrainMin.x + SizeX - 2f), 0f, R(TerrainMin.y + 2f, TerrainMin.y + SizeZ - 2f));
                float d = Walk(p);
                if (d > 3f) continue;
                if (DistanceToTrails(p) < 1.3f || Vector3.Distance(Flat(p), CabinCenter) < 4.5f || InPatch(p, 0.6f)) continue;
                if (Mathf.Abs(p.x - RoadX) < RoadHalf + 0.4f && d < 0f) continue;
                // Dentro de las salas, pocas matas y bajas: lo único alto y denso del piso es el pastizal escondite.
                bool inside = d < -1.2f;
                if (inside && (rng.NextDouble() > 0.3 || InPatch(p, 1.6f))) continue;
                if (Mathf.Abs(p.x - RavineX(p.z)) < 3.8f) continue;
                int clump = rng.Next(3, 6);
                for (int k = 0; k < clump; k++)
                {
                    var q = new Vector3(p.x + R(-0.5f, 0.5f), 0f, p.z + R(-0.5f, 0.5f));
                    q.y = GroundY(t, q) - 0.02f;
                    Tuft(shortMeshes[rng.Next(shortMeshes.Length)], q, inside ? R(0.6f, 0.85f) : R(1.0f, 1.45f), mat);
                    count++;
                }
            }

            // Vegetación escondite según el mapa de densidad (POLISH_PASS 3.9): masas irregulares de pasto alto y
            // helechos, densas en el centro y sueltas en el borde. Agachado donde el mapa pasa del umbral = oculto.
            var (leafA, leafN) = ChunkyMeshGenerator.PatternTextures(ChunkyMeshGenerator.Pattern.Leaves, 23);
            var fernMat = WindMaterial("Hollow_FernWind", leafA, leafN, new Color(0.62f, 0.78f, 0.55f), 0.18f, 0.08f, 1.6f, 0.03f, true, 0.4f);
            fernMat.SetFloat("_UseVertexHeight", 1f);
            var fern = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.GrassTuft("Fern_0", 977, 0.55f, 9, 0.5f));
            count += PlantDensityVegetation(root, t, densityMap, rng, Tuft, tallMeshes, tallMat, fern, fernMat);

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

        // ------------------------------------------------------------------ Detalle del suelo

        /// <summary>
        /// Textura de diorama en el piso de las salas: piedritas, ramas caídas y helechos en el borde. Todo sin
        /// colisión y combinado por parcelas de 12 m (pocas draw calls).
        /// </summary>
        static int GroundDetail(Transform root, Terrain t, Mats m, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool InPatch(Vector3 p, float pad)
            {
                foreach (var h in HidePatches) if (new Vector2(p.x - h.x, p.z - h.y).magnitude < h.z + pad) return true;
                return false;
            }
            var pebbleMeshes = new Mesh[4];
            // Piedritas de 7–20 cm: 120 triángulos bastan (con la malla de roca de 3k, GroundDetail era el 43% de los
            // triángulos del cuadro, 752k de 1.7M).
            for (int i = 0; i < 4; i++)
            {
                var rock = RockPrefab(i, false, m).GetComponent<MeshFilter>().sharedMesh;
                pebbleMeshes[i] = ChunkyMeshGenerator.Save(MeshDecimator.Decimate(rock, 120, "PH_HollowPebble_" + i));
            }
            var twig = LogMesh(0.9f, 0.035f);
            var (leafA, leafN) = ChunkyMeshGenerator.PatternTextures(ChunkyMeshGenerator.Pattern.Leaves, 23);
            var fernMat = WindMaterial("Hollow_FernWind", leafA, leafN, new Color(0.62f, 0.78f, 0.55f), 0.18f, 0.08f, 1.6f, 0.03f, true, 0.4f);
            fernMat.SetFloat("_UseVertexHeight", 1f);
            var fern = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.GrassTuft("Fern_0", 977, 0.55f, 9, 0.5f));

            var chunks = new Dictionary<(int, int, Material), List<CombineInstance>>();
            void Add(Material mat, Mesh mesh, Matrix4x4 trs, int sub = 0)
            {
                var key = (Mathf.FloorToInt(trs.m03 / 12f), Mathf.FloorToInt(trs.m23 / 12f), mat);
                if (!chunks.TryGetValue(key, out var list)) chunks[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = trs });
            }

            int count = 0;
            for (int tries = 0; tries < 20000 && count < 1400; tries++)
            {
                var p = new Vector3(R(TerrainMin.x, TerrainMin.x + SizeX), 0f, R(TerrainMin.y, TerrainMin.y + SizeZ));
                float d = Walk(p);
                if (d > 1.2f || Mathf.Abs(p.x - RavineX(p.z)) < 3.8f || InPatch(p, 0.3f)) continue;
                p.y = GroundY(t, p);
                bool trail = DistanceToTrails(p) < 1.1f;
                double roll = rng.NextDouble();
                if (d > -0.8f && roll < 0.55)
                {
                    // Helechos donde el piso se encuentra con la pared: el borde se lee mullido, no cortado.
                    Add(fernMat, fern, Matrix4x4.TRS(p, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(0.9f, 1.5f)));
                }
                else if (roll < 0.8 && !trail)
                {
                    float s = R(0.07f, 0.2f);
                    Add(m.rock, pebbleMeshes[rng.Next(4)], Matrix4x4.TRS(p - Vector3.up * s * 0.3f, Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f)), Vector3.one * s));
                }
                else if (!trail)
                {
                    Add(m.bark, twig, Matrix4x4.TRS(p + Vector3.up * 0.03f, Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(0f, 90f, R(-8f, 8f)), new Vector3(1f, 1f, R(0.6f, 1.4f))));
                }
                count++;
            }

            var parent = new GameObject("GroundDetail").transform;
            parent.SetParent(root, false);
            int k = 0;
            foreach (var kv in chunks)
            {
                var mesh = new Mesh { name = "GroundDetail_" + k++, indexFormat = IndexFormat.UInt32 };
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

            // Contorno del escenario: muros invisibles justo en el borde caminable, a la altura del PISO de la sala (no
            // del terreno de afuera: al sur el terreno baja una terraza y un muro a esa altura quedaba bajo los pies).
            // Cubren desde bajo el escalón hasta 2.5 m sobre el piso: no se cae al sur ni se trepa al norte.
            for (float x = TerrainMin.x + 1f; x < TerrainMin.x + SizeX - 1f; x += 0.6f)
                for (float z = TerrainMin.y + 1f; z < TerrainMin.y + SizeZ - 1f; z += 0.6f)
                {
                    var p = new Vector3(x, 0f, z);
                    float d = Walk(p);
                    if (d < 0.05f || d > 0.75f) continue;
                    if (Mathf.Abs(p.x - RavineX(p.z)) < 3.4f) continue;
                    Vector3 inward = -WalkNormal(p);
                    float floor = GroundY(t, p + inward * (d + 0.4f));
                    Wall("Edge", new Vector3(x, floor + 0.5f, z), new Vector3(0.7f, 6f, 0.7f));
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
