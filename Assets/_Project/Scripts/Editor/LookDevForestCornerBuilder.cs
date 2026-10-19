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
    /// Carril de look dev: una esquina del bosque de The Hollow con arte e iluminación finales.
    /// Paso 1 genera assets (materiales, capas de terreno, prefabs). Paso 2 arma la escena a partir del sandbox
    /// (reutiliza jugador, cámara, HUD y Lurker con sus sistemas de M1/M2). Paso 3 hornea la iluminación.
    /// Todo es regenerable: correr los pasos de nuevo reemplaza lo generado.
    /// </summary>
    public static class LookDevForestCornerBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/LookDev_ForestCorner.unity";
        const string SourceScene = "Assets/_Project/Scenes/Sandbox_Greybox.unity";
        const string PrefabDir = "Assets/_Project/Prefabs/Environment";
        const string DataDir = "Assets/_Project/Art/LookDev";
        const string MeshesDir = ConiferMeshGenerator.OutMeshes;

        const float Size = 60f;
        const float Half = Size * 0.5f;
        const float MaxHeight = 20f;
        const float BaseHeight = 1f;         // el suelo base queda en y=0 (terreno en y=-BaseHeight)
        const int HeightRes = 129;
        const int AlphaRes = 128;
        const int Seed = 1734;

        static readonly Vector3 ClearingCenter = new Vector3(4f, 0f, 5f);
        const float ClearingRadius = 7f;
        static readonly Vector3[] Trail =
        {
            new Vector3(-27f, 0f, -24f), new Vector3(-18f, 0f, -18f), new Vector3(-9f, 0f, -7f),
            new Vector3(-1f, 0f, 1f), new Vector3(8f, 0f, 9f), new Vector3(16f, 0f, 19f), new Vector3(26f, 0f, 27f)
        };
        static readonly Vector3 PlayerStart = new Vector3(-13.5f, 0f, -12.5f);

        // ------------------------------------------------------------------ Paso 1: assets

        [MenuItem("Project Ridge/Look Dev/1 - Build Assets")]
        public static string BuildAssets()
        {
            var log = new System.Text.StringBuilder();
            PolyHavenMaterialBuilder.EnsureFolder(PrefabDir);
            PolyHavenMaterialBuilder.EnsureFolder(DataDir);

            // Capas de terreno
            string T(string n) => PolyHavenMaterialBuilder.TexturesRoot + "/" + n;
            PolyHavenMaterialBuilder.BuildTerrainLayer("ForestGround", PolyHavenMaterialBuilder.BuildTextureSet(T("forest_ground_04"), "forest_ground_04"), 3f);
            PolyHavenMaterialBuilder.BuildTerrainLayer("Trail", PolyHavenMaterialBuilder.BuildTextureSet(T("mud_forest"), "mud_forest"), 2.5f);
            PolyHavenMaterialBuilder.BuildTerrainLayer("Leaves", PolyHavenMaterialBuilder.BuildTextureSet(T("forest_leaves_04"), "forest_leaves_04"), 2.5f);
            PolyHavenMaterialBuilder.BuildTerrainLayer("Rocky", PolyHavenMaterialBuilder.BuildTextureSet(T("rocky_trail"), "rocky_trail"), 3f);
            log.AppendLine("terrain layers: 4");

            // Coníferas
            var bark = PolyHavenMaterialBuilder.BuildLitMaterial("pine_bark", PolyHavenMaterialBuilder.BuildTextureSet(T("pine_bark"), "pine_bark"), 0.8f);
            // Ramas: atlas fotográfico de ramitas de fir_tree_01 (CC0) sobre las tarjetas procedurales.
            string twigFolder = PolyHavenMaterialBuilder.ModelsRoot + "/fir_tree_01/textures";
            var twigSet = PolyHavenMaterialBuilder.BuildTextureSet(twigFolder, "fir_tree_01_twig");
            twigSet.Albedo = PolyHavenMaterialBuilder.CombineAlpha("fir_tree_01_twig", twigSet.Albedo, PolyHavenMaterialBuilder.Find(twigFolder, "fir_tree_01_twig_alpha"));
            var needles = PolyHavenMaterialBuilder.BuildLitMaterial("fir_twig", twigSet, 0.45f, doubleSided: true, alphaClip: twigSet.Albedo);
            needles.SetColor("_BaseColor", new Color(0.62f, 0.68f, 0.6f)); // la foto es de día: bajar brillo y saturación
            EditorUtility.SetDirty(needles);

            var variants = new (string name, ConiferMeshGenerator.Settings s, int seed)[]
            {
                ("Pine_A", Pine(12f, 2.6f, 8), 11), ("Pine_B", Pine(14f, 2.9f, 9), 12),
                ("Pine_C", Pine(10f, 2.3f, 7), 13), ("Pine_D", Pine(16f, 3.1f, 10), 14),
                ("Fir_A", Fir(9f, 2.4f), 21), ("Fir_B", Fir(7f, 2.0f), 22),
                ("Dead_A", Dead(9f), 31), ("Dead_B", Dead(6.5f), 32)
            };
            foreach (var v in variants)
            {
                var mesh = ConiferMeshGenerator.SaveMesh(ConiferMeshGenerator.Build("Conifer_" + v.name, v.s, v.seed, TwigSprites));
                SaveConiferPrefab(v.name, mesh, v.s, bark, needles);
                log.AppendLine(v.name + " tris=" + mesh.triangles.Length / 3);
            }

            // Modelos Poly Haven → prefabs con material URP propio
            string M(string n) => PolyHavenMaterialBuilder.ModelsRoot + "/" + n;
            string Tx(string n) => M(n) + "/textures";

            var rockMat1 = PolyHavenMaterialBuilder.BuildLitMaterial("rock_moss_set_01", PolyHavenMaterialBuilder.BuildTextureSet(Tx("rock_moss_set_01"), "rock_moss_set_01"), 0.9f);
            var rockMat2 = PolyHavenMaterialBuilder.BuildLitMaterial("rock_moss_set_02", PolyHavenMaterialBuilder.BuildTextureSet(Tx("rock_moss_set_02"), "rock_moss_set_02"), 0.9f);
            var boulderMat = PolyHavenMaterialBuilder.BuildLitMaterial("boulder_01", PolyHavenMaterialBuilder.BuildTextureSet(Tx("boulder_01"), "boulder_01"), 0.9f);
            var stumpMat = PolyHavenMaterialBuilder.BuildLitMaterial("tree_stump_01", PolyHavenMaterialBuilder.BuildTextureSet(Tx("tree_stump_01"), "tree_stump_01"), 0.8f);
            var trunkMat = PolyHavenMaterialBuilder.BuildLitMaterial("dead_tree_trunk", PolyHavenMaterialBuilder.BuildTextureSet(Tx("dead_tree_trunk"), "dead_tree_trunk"), 0.8f);
            var branchMat = PolyHavenMaterialBuilder.BuildLitMaterial("dry_branches_medium_01", PolyHavenMaterialBuilder.BuildTextureSet(Tx("dry_branches_medium_01"), "dry_branches_medium_01"), 0.6f, doubleSided: true);
            var fernSet = PolyHavenMaterialBuilder.BuildTextureSet(Tx("fern_02"), "fern_02");
            fernSet.Albedo = PolyHavenMaterialBuilder.CombineAlpha("fern_02", fernSet.Albedo, PolyHavenMaterialBuilder.Find(Tx("fern_02"), "fern_02_alpha"));
            var fernMat = PolyHavenMaterialBuilder.BuildLitMaterial("fern_02", fernSet, 0.5f, doubleSided: true, alphaClip: fernSet.Albedo);

            int rocks = 0;
            rocks += SplitChildrenToPrefabs(M("rock_moss_set_01") + "/rock_moss_set_01_1k.fbx", "Rock", rockMat1, collider: true);
            rocks += SplitChildrenToPrefabs(M("rock_moss_set_02") + "/rock_moss_set_02_1k.fbx", "Rock", rockMat2, collider: true);
            SaveLodPrefab(M("boulder_01") + "/boulder_01_1k.fbx", "Boulder_01", boulderMat);
            SaveModelPrefab(M("tree_stump_01") + "/tree_stump_01_1k.fbx", "Stump_01", stumpMat, collider: true);
            SaveModelPrefab(M("dead_tree_trunk") + "/dead_tree_trunk_1k.fbx", "DeadTrunk_01", trunkMat, collider: true);
            int branches = SplitChildrenToPrefabs(M("dry_branches_medium_01") + "/dry_branches_medium_01_1k.fbx", "Branches", branchMat, collider: false);
            int ferns = SplitChildrenToPrefabs(M("fern_02") + "/fern_02_1k.fbx", "Fern", fernMat, collider: false);
            log.AppendLine("rocks=" + rocks + " branches=" + branches + " ferns=" + ferns + " + boulder, stump, trunk");

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>Las 4 ramitas grandes del atlas fir_tree_01_twig (uMin, vBase, uMax, vPunta).</summary>
        static readonly Vector4[] TwigSprites =
        {
            new Vector4(0.293f, 0.219f, 0.645f, 0.609f),
            new Vector4(0.625f, 0.160f, 0.967f, 0.560f),
            new Vector4(0.650f, 0.590f, 0.940f, 0.970f),
            new Vector4(0.190f, 0.630f, 0.420f, 0.960f),
        };

        static ConiferMeshGenerator.Settings Pine(float h, float r, int tiers) => new ConiferMeshGenerator.Settings
        { height = h, trunkRadius = 0.22f, crownBase = 0.3f, crownRadius = r, tiers = tiers + 3, segments = 7, droop = 0.38f, jitter = 0.18f };
        static ConiferMeshGenerator.Settings Fir(float h, float r) => new ConiferMeshGenerator.Settings
        { height = h, trunkRadius = 0.16f, crownBase = 0.07f, crownRadius = r, tiers = 13, segments = 8, droop = 0.3f, jitter = 0.15f };
        static ConiferMeshGenerator.Settings Dead(float h) => new ConiferMeshGenerator.Settings
        { height = h, trunkRadius = 0.2f, dead = true };

        static Material LitMaterial(string relPath, Texture2D albedo, Texture2D normal, float smoothness, bool doubleSided)
        {
            string path = "Assets/_Project/" + relPath + ".mat";
            PolyHavenMaterialBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", albedo);
            m.SetTexture("_BumpMap", normal);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", smoothness);
            if (doubleSided) m.SetFloat("_Cull", 0f);
            // Agujas recortadas por alfa (borde dentado, también en la sombra).
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Copia transparente de un material URP Lit, para el desvanecido por oclusión de cámara.</summary>
        internal static Material FadedVariant(Material src)
        {
            string path = AssetDatabase.GetAssetPath(src).Replace(".mat", "_Faded.mat");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static void AddFadeable(GameObject go, Renderer[] renderers, Material[] opaque)
        {
            var faded = new Material[opaque.Length];
            for (int i = 0; i < opaque.Length; i++) faded[i] = FadedVariant(opaque[i]);
            var f = go.AddComponent<ProjectRidge.CameraRig.OcclusionFadeable>();
            var so = new SerializedObject(f);
            void Fill(string prop, Object[] items)
            {
                var arr = so.FindProperty(prop);
                arr.arraySize = items.Length;
                for (int i = 0; i < items.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            Fill("renderers", renderers);
            Fill("opaqueMaterials", opaque);
            Fill("fadedMaterials", faded);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SaveConiferPrefab(string name, Mesh mesh, ConiferMeshGenerator.Settings s, Material bark, Material needles)
        {
            var go = new GameObject("Conifer_" + name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = s.dead ? new[] { bark, bark } : new[] { bark, needles };
            mr.receiveGI = ReceiveGI.LightProbes;
            AddFadeable(go, new Renderer[] { mr }, s.dead ? new[] { bark } : new[] { bark, needles });
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = s.trunkRadius + 0.08f;
            col.height = Mathf.Min(4f, s.height);
            col.center = Vector3.up * col.height * 0.5f;
            if (!s.dead)
            {
                // Copa: trigger en la capa Canopy. No bloquea el movimiento; sí la luz del OVNI.
                var canopy = new GameObject("Canopy");
                canopy.layer = EnsureLayer(11, "Canopy");
                canopy.transform.SetParent(go.transform, false);
                var cc = canopy.AddComponent<CapsuleCollider>();
                cc.isTrigger = true;
                float crownH = s.height * (1f - s.crownBase);
                cc.radius = s.crownRadius * 0.6f;
                cc.height = Mathf.Max(crownH, cc.radius * 2f);
                cc.center = Vector3.up * (s.height * s.crownBase + crownH * 0.5f);
            }
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.012f, new Renderer[] { mr }) });
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Conifer_" + name + ".prefab");
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// lightmapUV: solo para rocas (horneadas). Props densos y vegetación usan sondas de luz: el unwrap de mallas
        /// de 40–100k triángulos tarda minutos y puede fallar ("Could not pack uvs").
        /// </summary>
        internal static int EnsureLayer(int index, string name)
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tm.FindProperty("layers").GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                tm.ApplyModifiedPropertiesWithoutUndo();
            }
            return index;
        }

        static void ConfigureModel(string fbxPath, bool lightmapUV)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            bool changed = imp.generateSecondaryUV != lightmapUV || imp.materialImportMode != ModelImporterMaterialImportMode.None;
            imp.generateSecondaryUV = lightmapUV;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            if (changed) imp.SaveAndReimport();
        }

        /// <summary>Un prefab por cada hijo del FBX, con el pivote en la base del objeto.</summary>
        static int SplitChildrenToPrefabs(string fbxPath, string prefix, Material mat, bool collider)
        {
            ConfigureModel(fbxPath, lightmapUV: collider);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            int count = 0;
            foreach (Transform child in model.transform)
            {
                var mf = child.GetComponent<MeshFilter>();
                if (mf == null) continue;
                var root = new GameObject(prefix + "_" + child.name);
                var part = new GameObject("Mesh");
                part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                part.transform.localRotation = child.localRotation;
                part.transform.localScale = child.localScale;
                // Centrar: la base de los bounds en el origen del prefab.
                Bounds b = mr.bounds;
                part.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                if (collider) part.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                else mr.receiveGI = ReceiveGI.LightProbes;
                GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | (collider ? StaticEditorFlags.OccluderStatic : 0));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + root.name + ".prefab");
                Object.DestroyImmediate(root);
                count++;
            }
            return count;
        }

        static void SaveModelPrefab(string fbxPath, string name, Material mat, bool collider)
        {
            ConfigureModel(fbxPath, lightmapUV: false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = new GameObject(name);
            var inst = (GameObject)Object.Instantiate(model, root.transform);
            inst.name = "Mesh";
            foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>())
            {
                mr.sharedMaterial = mat;
                mr.receiveGI = ReceiveGI.LightProbes;
                if (collider) mr.gameObject.AddComponent<MeshCollider>().sharedMesh = mr.GetComponent<MeshFilter>().sharedMesh;
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
            Bounds b = BoundsOf(inst);
            inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        static void SaveLodPrefab(string fbxPath, string name, Material mat)
        {
            ConfigureModel(fbxPath, lightmapUV: true);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = new GameObject(name);
            var inst = (GameObject)Object.Instantiate(model, root.transform);
            inst.name = "Mesh";
            // El FBX ya trae su LODGroup (hijos _LOD0.._LOD3); se reutiliza y solo se ajustan las transiciones.
            var lodGroup = inst.GetComponent<LODGroup>();
            var lods = lodGroup.GetLODs();
            float[] heights = { 0.45f, 0.2f, 0.07f, 0.015f };
            for (int i = 0; i < lods.Length; i++)
            {
                lods[i].screenRelativeTransitionHeight = heights[Mathf.Min(i, heights.Length - 1)];
                foreach (var r in lods[i].renderers)
                {
                    r.sharedMaterial = mat;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
                    if (i == 0) r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            lodGroup.SetLODs(lods);
            AddFadeable(root, inst.GetComponentsInChildren<Renderer>(), new[] { mat });
            Bounds b = BoundsOf(inst);
            inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ Paso 2: escena

        [MenuItem("Project Ridge/Look Dev/2 - Build Scene")]
        public static string BuildScene()
        {
            var log = new System.Text.StringBuilder();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                AssetDatabase.CopyAsset(SourceScene, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var n in new[] { "Environment", "LurkerPatrol", "Moon", "LookDevEnvironment", "PostFX", "LightProbes", "UFO", "UFOScanPoints" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            var envRoot = new GameObject("LookDevEnvironment").transform;
            var terrain = BuildTerrain(envRoot);
            log.AppendLine("terrain ok");

            var rng = new System.Random(Seed);
            var trees = ScatterTrees(envRoot, terrain, rng);
            log.AppendLine("trees=" + trees.Count);
            int props = PlaceProps(envRoot, terrain, rng, trees);
            log.AppendLine("props=" + props);
            BuildBounds(envRoot);

            SetupLighting(log);
            SetupPostFx();
            int probes = BuildLightProbes(terrain);
            log.AppendLine("light probes=" + probes);

            // Jugador, Lurker y debug
            var player = GameObject.Find("Player");
            PlaceOnTerrain(player.transform, terrain, PlayerStart);
            player.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 0f, 1f));
            var flash = player.GetComponent<ProjectRidge.Player.FlashlightController>();
            var so = new SerializedObject(flash);
            so.FindProperty("startOn").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Respawn mínimo (M4 lo reemplaza por checkpoints).
            var oldSpawn = GameObject.Find("PlayerSpawn");
            if (oldSpawn != null) Object.DestroyImmediate(oldSpawn);
            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            if (!player.TryGetComponent(out ProjectRidge.Player.PlayerRespawn respawn)) respawn = player.AddComponent<ProjectRidge.Player.PlayerRespawn>();
            var rso = new SerializedObject(respawn);
            rso.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ProjectRidge.Player.PlayerHealthConfig>("Assets/_Project/Data/PlayerHealthConfig.asset");
            rso.FindProperty("health").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerHealth>();
            rso.FindProperty("body").objectReferenceValue = player.GetComponent<CharacterController>();
            rso.FindProperty("spawnPoint").objectReferenceValue = spawn;
            rso.ApplyModifiedPropertiesWithoutUndo();

            var lurker = GameObject.Find("Lurker");
            PlaceOnTerrain(lurker.transform, terrain, ClearingCenter + new Vector3(3f, 0f, 2f));
            var route = new GameObject("LurkerPatrol").transform;
            // Patrulla dentro y alrededor del claro, lejos del inicio del jugador.
            var pts = new[] { ClearingCenter + new Vector3(5f, 0f, 3f), ClearingCenter + new Vector3(-3f, 0f, 6f), ClearingCenter + new Vector3(-4f, 0f, -2f), ClearingCenter + new Vector3(3f, 0f, -5f) };
            var ptT = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var p = new GameObject("P" + i).transform;
                p.SetParent(route, false);
                PlaceOnTerrain(p, terrain, pts[i]);
                ptT[i] = p;
            }
            var enemy = lurker.GetComponent<ProjectRidge.AI.EnemyController>();
            var eso = new SerializedObject(enemy);
            var arr = eso.FindProperty("patrolPoints");
            arr.arraySize = ptT.Length;
            for (int i = 0; i < ptT.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = ptT[i];
            eso.ApplyModifiedPropertiesWithoutUndo();

            BuildUFO(terrain, player);
            log.AppendLine("ufo ok");

            var cam = Camera.main.gameObject;
            if (!cam.TryGetComponent(out ProjectRidge.CameraRig.CameraOcclusionFader fader)) fader = cam.AddComponent<ProjectRidge.CameraRig.CameraOcclusionFader>();
            var fso = new SerializedObject(fader);
            fso.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ProjectRidge.CameraRig.IsoCameraConfig>("Assets/_Project/Data/IsoCameraConfig.asset");
            fso.FindProperty("target").objectReferenceValue = player.transform;
            fso.ApplyModifiedPropertiesWithoutUndo();

            var debugOverlay = Object.FindFirstObjectByType<ProjectRidge.UI.DebugOverlay>(FindObjectsInactive.Include);
            if (debugOverlay != null) debugOverlay.gameObject.SetActive(false);
            var noiseView = GameObject.Find("NoiseDebugView");
            if (noiseView != null) noiseView.SetActive(false);

            // NavMesh sobre el terreno y los obstáculos
            var surface = envRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1;
            surface.BuildNavMesh();
            string navDir = "Assets/_Project/Scenes/LookDev_ForestCorner";
            PolyHavenMaterialBuilder.EnsureFolder(navDir);
            string navPath = navDir + "/NavMesh-LookDev.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath) != null) AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            log.AppendLine("navmesh vertices=" + NavMesh.CalculateTriangulation().vertices.Length);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
            return log.ToString();
        }

        static Terrain BuildTerrain(Transform parent)
        {
            string tdPath = DataDir + "/LookDev_TerrainData.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
            if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, tdPath); }
            td.heightmapResolution = HeightRes;
            td.alphamapResolution = AlphaRes;
            td.size = new Vector3(Size, MaxHeight, Size);

            var heights = new float[HeightRes, HeightRes];
            for (int z = 0; z < HeightRes; z++)
                for (int x = 0; x < HeightRes; x++)
                {
                    var world = new Vector3(x / (float)(HeightRes - 1) * Size - Half, 0f, z / (float)(HeightRes - 1) * Size - Half);
                    heights[z, x] = HeightAt(world) / MaxHeight;
                }
            td.SetHeights(0, 0, heights);

            td.terrainLayers = new[]
            {
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(PolyHavenMaterialBuilder.OutTerrainLayers + "/TL_ForestGround.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(PolyHavenMaterialBuilder.OutTerrainLayers + "/TL_Trail.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(PolyHavenMaterialBuilder.OutTerrainLayers + "/TL_Leaves.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(PolyHavenMaterialBuilder.OutTerrainLayers + "/TL_Rocky.terrainlayer"),
            };
            var alphas = new float[AlphaRes, AlphaRes, 4];
            for (int z = 0; z < AlphaRes; z++)
                for (int x = 0; x < AlphaRes; x++)
                {
                    float nx = x / (float)(AlphaRes - 1), nz = z / (float)(AlphaRes - 1);
                    var world = new Vector3(nx * Size - Half, 0f, nz * Size - Half);
                    float steep = td.GetSteepness(nx, nz);
                    float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 30f, steep));
                    float trail = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, 2.6f, DistanceToTrail(world)));
                    float leaves = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.7f, Mathf.PerlinNoise(world.x * 0.09f + 40f, world.z * 0.09f + 13f)));
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
            go.transform.position = new Vector3(-Half, -BaseHeight, -Half);
            var terrain = go.GetComponent<Terrain>();
            string matPath = DataDir + "/Terrain_Lit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            terrain.materialTemplate = mat;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 45f;
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return terrain;
        }

        /// <summary>Altura del mundo (m, sobre el terreno base) en una posición XZ.</summary>
        static float HeightAt(Vector3 w)
        {
            float rolling = (Mathf.PerlinNoise(w.x * 0.045f + 7f, w.z * 0.045f + 3f) - 0.5f) * 2.4f
                          + (Mathf.PerlinNoise(w.x * 0.13f + 1f, w.z * 0.13f + 9f) - 0.5f) * 0.6f;
            float edge = Mathf.Min(Half - Mathf.Abs(w.x), Half - Mathf.Abs(w.z));
            float berm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 1f, edge)) * 6f;

            float flat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ClearingRadius * 0.6f, ClearingRadius * 1.3f, Vector3.Distance(Flat(w), ClearingCenter)));
            rolling = Mathf.Lerp(rolling, -0.2f, flat * 0.85f);

            float trailMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.2f, 3.2f, DistanceToTrail(w)));
            float h = BaseHeight + rolling + berm;
            h = Mathf.Lerp(h, BaseHeight - 0.15f + berm * 0.5f, trailMask * 0.8f);
            return Mathf.Max(0.05f, h);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static float DistanceToTrail(Vector3 w)
        {
            w = Flat(w);
            float best = float.MaxValue;
            for (int i = 1; i < Trail.Length; i++)
            {
                Vector3 a = Trail[i - 1], b = Trail[i];
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(w - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector3.Distance(w, a + ab * t));
            }
            return best;
        }

        static float GroundY(Terrain t, Vector3 w) => t.SampleHeight(w) + t.transform.position.y;

        static void PlaceOnTerrain(Transform tr, Terrain t, Vector3 xz)
        {
            var p = Flat(xz);
            p.y = GroundY(t, p) + 0.02f;
            tr.position = p;
        }

        static List<Vector3> ScatterTrees(Transform parent, Terrain terrain, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var root = new GameObject("Trees").transform;
            root.SetParent(parent, false);
            string[] pines = { "Pine_A", "Pine_B", "Pine_C", "Pine_D" };
            string[] firs = { "Fir_A", "Fir_B" };
            string[] dead = { "Dead_A", "Dead_B" };
            var placed = new List<Vector3>();
            for (int tries = 0; tries < 6000 && placed.Count < 130; tries++)
            {
                var p = new Vector3(R(-Half + 1f, Half - 1f), 0f, R(-Half + 1f, Half - 1f));
                if (DistanceToTrail(p) < 3f) continue;
                if (Vector3.Distance(p, ClearingCenter) < ClearingRadius) continue;
                if (Vector3.Distance(p, PlayerStart) < 5f) continue;
                float fromCenter = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
                float density = 0.3f + 0.7f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, 26f, fromCenter));
                if (rng.NextDouble() > density) continue;
                bool tooClose = false;
                foreach (var q in placed) if ((q - p).sqrMagnitude < 2.6f * 2.6f) { tooClose = true; break; }
                if (tooClose) continue;

                double roll = rng.NextDouble();
                string variant = roll < 0.68 ? pines[rng.Next(pines.Length)] : roll < 0.9 ? firs[rng.Next(firs.Length)] : dead[rng.Next(dead.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Conifer_" + variant + ".prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                p.y = GroundY(terrain, p) - 0.15f;
                go.transform.position = p;
                go.transform.rotation = Quaternion.Euler(R(-2f, 2f), R(0f, 360f), R(-2f, 2f));
                go.transform.localScale = Vector3.one * R(0.85f, 1.2f);
                placed.Add(p);
            }
            return placed;
        }

        static int PlaceProps(Transform parent, Terrain terrain, System.Random rng, List<Vector3> trees)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var root = new GameObject("Props").transform;
            root.SetParent(parent, false);
            int count = 0;

            GameObject Put(string prefabName, Vector3 xz, float yaw, float scale, float sink)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + prefabName + ".prefab");
                if (prefab == null) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                var p = Flat(xz);
                p.y = GroundY(terrain, p) - sink;
                go.transform.position = p;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * scale;
                count++;
                return go;
            }

            var rockNames = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("Rock_ t:Prefab", new[] { PrefabDir })) rockNames.Add(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));

            // Rocas a los lados del sendero (cobertura) y alrededor del claro.
            for (int i = 1; i < Trail.Length - 1; i++)
            {
                Vector3 a = Trail[i], dir = (Trail[i + 1] - Trail[i]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                for (int k = 0; k < 2; k++)
                {
                    float s = rng.NextDouble() < 0.5 ? -1f : 1f;
                    var pos = a + dir * R(1f, 6f) + side * s * R(3.2f, 5.5f);
                    Put(rockNames[rng.Next(rockNames.Count)], pos, R(0f, 360f), R(0.9f, 1.7f), 0.15f);
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * Mathf.PI * 2f + R(-0.3f, 0.3f);
                var pos = ClearingCenter + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * R(ClearingRadius * 0.8f, ClearingRadius * 1.1f);
                if (DistanceToTrail(pos) < 2.5f) continue;
                Put(rockNames[rng.Next(rockNames.Count)], pos, R(0f, 360f), R(1.1f, 2.0f), 0.2f);
            }
            // Rocas grandes como escondites junto al claro.
            Put("Boulder_01", ClearingCenter + new Vector3(-6.5f, 0f, -2.5f), 40f, 1.9f, 0.25f);
            Put("Boulder_01", ClearingCenter + new Vector3(6f, 0f, -4.5f), 200f, 1.5f, 0.25f);
            Put("Boulder_01", new Vector3(-12f, 0f, -9f), 120f, 1.3f, 0.2f);

            // Tronco caído junto al sendero, tocones, ramas.
            Put("DeadTrunk_01", new Vector3(-6.5f, 0f, -8.5f), 35f, 1.1f, 0.12f);
            Put("Stump_01", new Vector3(-14f, 0f, -16f), 0f, 1f, 0.05f);
            Put("Stump_01", ClearingCenter + new Vector3(-3f, 0f, 6f), 140f, 0.8f, 0.05f);
            var branchNames = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("Branches_ t:Prefab", new[] { PrefabDir })) branchNames.Add(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
            for (int i = 0; i < 10; i++)
            {
                var t = trees[rng.Next(trees.Count)];
                Put(branchNames[rng.Next(branchNames.Count)], t + new Vector3(R(-2f, 2f), 0f, R(-2f, 2f)), R(0f, 360f), R(0.8f, 1.3f), 0.02f);
            }

            // Helechos al pie de los árboles, fuera del sendero.
            var fernNames = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("Fern_ t:Prefab", new[] { PrefabDir })) fernNames.Add(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
            int ferns = 0;
            for (int tries = 0; tries < 500 && ferns < 70; tries++)
            {
                var t = trees[rng.Next(trees.Count)];
                var pos = t + new Vector3(R(-3f, 3f), 0f, R(-3f, 3f));
                if (DistanceToTrail(pos) < 1.6f || Vector3.Distance(Flat(pos), PlayerStart) < 2.5f) continue;
                Put(fernNames[rng.Next(fernNames.Count)], pos, R(0f, 360f), R(1.3f, 2.1f), 0.02f);
                ferns++;
            }
            return count;
        }

        internal static void BuildUFO(Terrain terrain, GameObject player)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ProjectRidge.AI.UFOConfig>("Assets/_Project/Data/UFOConfig.asset");
            if (cfg == null) { cfg = ScriptableObject.CreateInstance<ProjectRidge.AI.UFOConfig>(); AssetDatabase.CreateAsset(cfg, "Assets/_Project/Data/UFOConfig.asset"); }

            // El OVNI no se ve: solo su luz (haz + foco) y su zumbido.
            var root = new GameObject("UFO");
            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(root.transform, false);
            beamGo.transform.localPosition = new Vector3(0f, -0.7f, 0f);
            var spot = beamGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.shadows = LightShadows.Soft;
            spot.lightmapBakeType = LightmapBakeType.Realtime;

            var visual = new GameObject("BeamVisual");
            visual.transform.SetParent(beamGo.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = BeamConeMesh();
            var vr = visual.AddComponent<MeshRenderer>();
            vr.sharedMaterial = BeamMaterial();
            vr.shadowCastingMode = ShadowCastingMode.Off;
            vr.receiveShadows = false;

            var pointsRoot = new GameObject("UFOScanPoints").transform;
            var offsets = new[] { new Vector3(-7f, 0f, -3f), new Vector3(-2f, 0f, 6f), new Vector3(6f, 0f, 2f), new Vector3(2f, 0f, -6f), new Vector3(-10f, 0f, -11f) };
            var pts = new Transform[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var p = new GameObject("S" + i).transform;
                p.SetParent(pointsRoot, false);
                PlaceOnTerrain(p, terrain, ClearingCenter + offsets[i]);
                pts[i] = p;
            }

            var ufo = root.AddComponent<ProjectRidge.AI.UFOController>();
            var so = new SerializedObject(ufo);
            so.FindProperty("config").objectReferenceValue = cfg;
            so.FindProperty("beam").objectReferenceValue = spot;
            so.FindProperty("beamVisual").objectReferenceValue = visual.transform;
            so.FindProperty("groundMask").intValue = 1;
            var arr = so.FindProperty("scanPoints");
            arr.arraySize = pts.Length;
            for (int i = 0; i < pts.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = pts[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<AudioSource>().playOnAwake = false;
            var hum = root.AddComponent<ProjectRidge.AI.UFOHum>();
            var hso = new SerializedObject(hum);
            hso.FindProperty("config").objectReferenceValue = cfg;
            hso.FindProperty("ufo").objectReferenceValue = ufo;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var exposure = root.AddComponent<ProjectRidge.AI.UFOExposure>();
            var eso = new SerializedObject(exposure);
            eso.FindProperty("config").objectReferenceValue = cfg;
            eso.FindProperty("ufo").objectReferenceValue = ufo;
            eso.FindProperty("target").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerVisibility>();
            eso.FindProperty("player").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerController>();
            eso.FindProperty("occluders").intValue = (1 << 0) | (1 << EnsureLayer(11, "Canopy"));
            eso.ApplyModifiedPropertiesWithoutUndo();

            // Posición inicial del OVNI sobre el primer punto (luego la actualiza el controlador en play).
            Vector3 offsetDir = Quaternion.Euler(0f, cfg.offsetBearing, 0f) * Vector3.forward;
            root.transform.position = pts[0].position + Vector3.up * cfg.altitude + offsetDir * cfg.horizontalOffset;
            beamGo.transform.rotation = Quaternion.LookRotation(pts[0].position - beamGo.transform.position);
        }

        /// <summary>Cono abierto, ápice en el origen, base de radio 1 en z=1. Alfa por vértice: más denso cerca del OVNI.</summary>
        static Mesh BeamConeMesh()
        {
            string path = MeshesDir + "/../UFO_BeamCone.asset";
            path = "Assets/_Project/Art/Meshes/UFO_BeamCone.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            const int seg = 32;
            var v = new Vector3[seg * 2];
            var c = new Color[seg * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v[i * 2] = new Vector3(Mathf.Cos(a) * 0.04f, Mathf.Sin(a) * 0.04f, 0f);
                v[i * 2 + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f);
                c[i * 2] = new Color(1f, 1f, 1f, 0.22f);
                c[i * 2 + 1] = new Color(1f, 1f, 1f, 0.03f);
                int n = (i + 1) % seg;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = n * 2; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2 + 1; tris[t + 4] = n * 2; tris[t + 5] = n * 2 + 1;
            }
            mesh = new Mesh { name = "UFO_BeamCone", vertices = v, colors = c, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            PolyHavenMaterialBuilder.EnsureFolder("Assets/_Project/Art/Meshes");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material BeamMaterial()
        {
            string path = "Assets/_Project/Art/Materials/LookDev/UFO_Beam.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f); // aditivo
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetColor("_BaseColor", new Color(0.65f, 0.9f, 1f, 1f));
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildBounds(Transform parent)
        {
            var root = new GameObject("Bounds (invisible)").transform;
            root.SetParent(parent, false);
            const float h = 12f, t = 1f, inset = 3f;
            void Wall(string n, Vector3 pos, Vector3 size)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                go.AddComponent<BoxCollider>().size = size;
            }
            float e = Half - inset;
            Wall("N", new Vector3(0f, h * 0.5f, e), new Vector3(Size, h, t));
            Wall("S", new Vector3(0f, h * 0.5f, -e), new Vector3(Size, h, t));
            Wall("E", new Vector3(e, h * 0.5f, 0f), new Vector3(t, h, Size));
            Wall("W", new Vector3(-e, h * 0.5f, 0f), new Vector3(t, h, Size));
        }

        internal static void SetupLighting(System.Text.StringBuilder log)
        {
            var moonGo = new GameObject("Moon");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.lightmapBakeType = LightmapBakeType.Mixed;
            moon.color = new Color(0.70f, 0.78f, 0.93f);
            moon.intensity = 1.1f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.85f;
            moonGo.transform.rotation = Quaternion.Euler(32f, -110f, 0f);
            RenderSettings.sun = moon;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.17f, 0.2f, 0.27f);
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.115f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.035f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.07f, 0.09f, 0.12f);
            RenderSettings.fogDensity = 0.028f;

            var cam = Camera.main;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.allowHDR = true;
            if (!cam.TryGetComponent(out UniversalAdditionalCameraData data)) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // Sombras de la luna: distancia corta (CLAUDE.md: ~25–30 m).
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                asset.shadowDistance = 30f;
                // Sombras de luces adicionales (linterna y reflector del OVNI), por píxel.
                var aso = new SerializedObject(asset);
                aso.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1;
                aso.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                aso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                log.AppendLine("URP asset " + asset.name + ": shadowDistance=30 hdr=" + asset.supportsHDR);
            }

            string lsPath = DataDir + "/LookDev_LightingSettings.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, lsPath); }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.lightmapResolution = 6f;
            ls.lightmapPadding = 2;
            ls.lightmapMaxSize = 1024;
            ls.directSampleCount = 16;
            ls.indirectSampleCount = 128;
            ls.environmentSampleCount = 128;
            ls.maxBounces = 2;
            ls.ao = true;
            ls.aoMaxDistance = 1.2f;
            ls.compressLightmaps = true;
            Lightmapping.lightingSettings = ls;
        }

        internal static void SetupPostFx()
        {
            string path = DataDir + "/LookDev_PostFX.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T c)) { c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); }
                return c;
            }
            var tm = Get<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
            var ca = Get<ColorAdjustments>();
            ca.postExposure.Override(0.9f); ca.contrast.Override(14f); ca.saturation.Override(-12f);
            ca.colorFilter.Override(Color.white);
            var wb = Get<WhiteBalance>(); wb.temperature.Override(-6f); wb.tint.Override(3f);
            var bloom = Get<Bloom>(); bloom.threshold.Override(0.95f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.65f);
            var vig = Get<Vignette>(); vig.intensity.Override(0.32f); vig.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostFX");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        static int BuildLightProbes(Terrain terrain)
        {
            var go = new GameObject("LightProbes");
            var group = go.AddComponent<LightProbeGroup>();
            var pts = new List<Vector3>();
            for (float x = -Half + 4f; x <= Half - 4f; x += 7f)
                for (float z = -Half + 4f; z <= Half - 4f; z += 7f)
                {
                    float y = GroundY(terrain, new Vector3(x, 0f, z));
                    pts.Add(new Vector3(x, y + 0.6f, z));
                    pts.Add(new Vector3(x, y + 3.5f, z));
                }
            group.probePositions = pts.ToArray();
            return pts.Count;
        }

        static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == ScenePath)) return;
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ Capturas para revisar el look

        /// <summary>Renderiza la cámara de juego y una vista general a PNG en la carpeta indicada. Devuelve stats.</summary>
        public static string Capture(string folder, string tag)
        {
            var cam = Camera.main;
            if (!Application.isPlaying)
            {
                var player = GameObject.Find("Player").transform;
                var cfg = AssetDatabase.LoadAssetAtPath<ProjectRidge.CameraRig.IsoCameraConfig>("Assets/_Project/Data/IsoCameraConfig.asset");
                var rot = Quaternion.Euler(cfg.pitch, cfg.yaw, 0f);
                cam.transform.SetPositionAndRotation(player.position + Vector3.up * cfg.targetHeight + rot * new Vector3(0f, 0f, -cfg.distance), rot);
                cam.fieldOfView = cfg.fieldOfView;
            }
            Render(cam, folder + "/" + tag + "_player.png");

            var go = new GameObject("TmpOverviewCam");
            var c2 = go.AddComponent<Camera>();
            c2.CopyFrom(cam);
            go.transform.SetPositionAndRotation(new Vector3(-16f, 15f, -16f), Quaternion.Euler(35f, 45f, 0f));
            c2.fieldOfView = 55f;
            Render(c2, folder + "/" + tag + "_overview.png");
            Object.DestroyImmediate(go);
            return "tris=" + UnityStats.triangles + " batches=" + UnityStats.batches + " setpass=" + UnityStats.setPassCalls;
        }

        public static void Render(Camera cam, string path)
        {
            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        // ------------------------------------------------------------------ Paso 3: iluminación

        [MenuItem("Project Ridge/Look Dev/3 - Bake Lighting")]
        public static bool StartBake()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath) return false;
            return Lightmapping.BakeAsync();
        }
    }
}
