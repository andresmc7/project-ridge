using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Personajes con el cuerpo base de Quaternius (CC0) y clips de la Universal Animation Library:
    /// - El papá: ropa generada desde la malla del cuerpo (camisa de franela, chaleco acolchado, jeans, botas) por
    ///   regiones de huesos, inflada y suavizada; pelo, barba, cejas; garrote, linterna, pistola de bengalas, mochila.
    /// - El Lurker: el mismo esqueleto con extremidades, cuello y dedos alargados, cuerpo adelgazado y cráneo
    ///   estirado (malla re-horneada en la nueva pose + avatar humanoide propio), piel oscura y húmeda, ojos rojos.
    /// Los Animator Controllers se generan aquí. Reemplaza el "Visual" del jugador y de cada Lurker en la escena activa.
    /// </summary>
    public static class CharacterBuilder
    {
        const string OutDir = "Assets/_Project/Art/Characters";
        const string HairDir = QuaterniusImport.Root + "/BaseCharacters/Hair";
        const string TexDir = QuaterniusImport.Root + "/BaseCharacters/Textures";

        [MenuItem("Project Ridge/Characters/Rebuild Characters in Active Scene")]
        public static string Build()
        {
            PolyHavenMaterialBuilder.EnsureFolder(OutDir);
            var log = new System.Text.StringBuilder();
            var fatherCtrl = FatherController();
            var lurkerCtrl = LurkerController();

            var player = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerController>();
            BuildFather(player.gameObject, fatherCtrl);
            log.AppendLine("father ok");
            var lurkerAsset = LurkerAssets();
            foreach (var enemy in Object.FindObjectsByType<ProjectRidge.AI.EnemyController>(FindObjectsSortMode.None))
            {
                BuildLurker(enemy.gameObject, lurkerCtrl, lurkerAsset);
                log.AppendLine("lurker ok: " + enemy.name);
            }
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ------------------------------------------------------------------ Utilidades de malla

        static GameObject InstantiateBody(Transform parent, int layer, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusImport.Body);
            var go = (GameObject)Object.Instantiate(prefab, parent, false);
            go.name = name;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return go;
        }

        static SkinnedMeshRenderer BodyRenderer(GameObject go) =>
            go.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name == "SuperHero_Male");

        static Mesh SaveMesh(Mesh m, string name)
        {
            m.name = name;
            string path = OutDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        enum Region { Skin, Shirt, Pants, Boots, Hands }

        static Region RegionOf(string bone)
        {
            if (bone.StartsWith("foot") || bone.StartsWith("ball")) return Region.Boots;
            if (bone.StartsWith("thigh") || bone.StartsWith("calf") || bone == "pelvis") return Region.Pants;
            if (bone.StartsWith("spine") || bone.StartsWith("clavicle") || bone.StartsWith("upperarm") || bone.StartsWith("lowerarm")) return Region.Shirt;
            if (bone.StartsWith("hand") || bone.Contains("thumb") || bone.Contains("index") || bone.Contains("middle") || bone.Contains("ring") || bone.Contains("pinky")) return Region.Hands;
            return Region.Skin;
        }

        static int Dominant(BoneWeight w)
        {
            int b = w.boneIndex0; float best = w.weight0;
            if (w.weight1 > best) { best = w.weight1; b = w.boneIndex1; }
            if (w.weight2 > best) { best = w.weight2; b = w.boneIndex2; }
            if (w.weight3 > best) { b = w.boneIndex3; }
            return b;
        }

        /// <summary>Vecinos por posición (soldando costuras de UV) para suavizar sin abrir grietas.</summary>
        static (int[] weld, List<int>[] neighbors) Adjacency(Vector3[] v, int[] tris)
        {
            var map = new Dictionary<Vector3Int, int>();
            var weld = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var key = Vector3Int.RoundToInt(v[i] * 10000f);
                if (!map.TryGetValue(key, out int id)) { id = map.Count; map[key] = id; }
                weld[i] = id;
            }
            var nb = new List<int>[map.Count];
            for (int i = 0; i < nb.Length; i++) nb[i] = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = weld[tris[t + k]], b = weld[tris[t + (k + 1) % 3]];
                    if (!nb[a].Contains(b)) nb[a].Add(b);
                    if (!nb[b].Contains(a)) nb[b].Add(a);
                }
            return (weld, nb);
        }

        /// <summary>Suaviza (Laplaciano) solo los vértices marcados, por posición soldada.</summary>
        static void Smooth(Vector3[] v, int[] tris, bool[] mask, int iterations, float lambda)
        {
            var (weld, nb) = Adjacency(v, tris);
            var pos = new Vector3[nb.Length];
            var m = new bool[nb.Length];
            for (int i = 0; i < v.Length; i++) { pos[weld[i]] = v[i]; m[weld[i]] |= mask[i]; }
            for (int it = 0; it < iterations; it++)
            {
                var next = (Vector3[])pos.Clone();
                for (int i = 0; i < pos.Length; i++)
                {
                    if (!m[i] || nb[i].Count == 0) continue;
                    Vector3 avg = Vector3.zero;
                    foreach (int j in nb[i]) avg += pos[j];
                    avg /= nb[i].Count;
                    next[i] = Vector3.Lerp(pos[i], avg, lambda);
                }
                pos = next;
            }
            for (int i = 0; i < v.Length; i++) v[i] = pos[weld[i]];
        }

        // ------------------------------------------------------------------ Materiales

        static Material Mat(string name, Texture2D albedo, Texture2D normal, Color color, float smoothness, float tiling = 1f)
        {
            string path = OutDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", color);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); } else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D Tex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + file);
        static Texture2D PH(string id, string map) => AssetDatabase.LoadAssetAtPath<Texture2D>(PolyHavenMaterialBuilder.Find(PolyHavenMaterialBuilder.TexturesRoot + "/" + id, id + "_" + map));

        // ------------------------------------------------------------------ El papá

        static void BuildFather(GameObject player, AnimatorController ctrl)
        {
            var pc = player.GetComponent<ProjectRidge.Player.PlayerController>();
            var pcso = new SerializedObject(pc);
            pcso.FindProperty("visualRoot").objectReferenceValue = null;
            pcso.ApplyModifiedPropertiesWithoutUndo();
            var old = player.transform.Find("Visual");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var body = InstantiateBody(player.transform, 9, "Visual");
            var smr = BodyRenderer(body);
            var src = smr.sharedMesh;
            var bones = smr.bones;
            var v = src.vertices;
            var n = src.normals;
            var bw = src.boneWeights;
            var tris = src.triangles;

            // Región por vértice según el hueso dominante; la región de cada triángulo, por mayoría.
            var region = new Region[v.Length];
            for (int i = 0; i < v.Length; i++) region[i] = RegionOf(bones[Dominant(bw[i])].name);
            var lists = new Dictionary<Region, List<int>>();
            foreach (Region r in System.Enum.GetValues(typeof(Region))) lists[r] = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Region a = region[tris[t]], b = region[tris[t + 1]], c = region[tris[t + 2]];
                Region r = a == b || a == c ? a : b == c ? b : a;
                lists[r == Region.Hands ? Region.Skin : r].AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
            }

            // Inflar la ropa y suavizar la musculatura bajo la tela.
            var nv = (Vector3[])v.Clone();
            var clothMask = new bool[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float push = region[i] == Region.Shirt ? 0.009f : region[i] == Region.Pants ? 0.007f : region[i] == Region.Boots ? 0.022f : 0f;
                nv[i] += n[i] * push;
                clothMask[i] = region[i] == Region.Shirt || region[i] == Region.Pants;
            }
            Smooth(nv, tris, clothMask, 6, 0.55f);

            var mesh = Object.Instantiate(src);
            mesh.vertices = nv;
            mesh.subMeshCount = 4;
            mesh.SetTriangles(lists[Region.Skin], 0);
            mesh.SetTriangles(lists[Region.Shirt], 1);
            mesh.SetTriangles(lists[Region.Pants], 2);
            mesh.SetTriangles(lists[Region.Boots], 3);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh = SaveMesh(mesh, "Father_Body");

            // Chaleco: cáscara del torso (sin brazos), más separada y lisa.
            var vestTris = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                bool torso = true;
                for (int k = 0; k < 3; k++)
                {
                    string bn = bones[Dominant(bw[tris[t + k]])].name;
                    torso &= bn.StartsWith("spine") || bn.StartsWith("clavicle");
                }
                if (torso) vestTris.AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
            }
            var vv = (Vector3[])nv.Clone();
            var vestMask = new bool[v.Length];
            foreach (int i in vestTris) vestMask[i] = true;
            for (int i = 0; i < vv.Length; i++) if (vestMask[i]) vv[i] += n[i] * 0.022f;
            Smooth(vv, tris, vestMask, 10, 0.6f);
            var vest = Object.Instantiate(src);
            vest.vertices = vv;
            vest.subMeshCount = 1;
            vest.SetTriangles(vestTris, 0);
            vest.RecalculateNormals();
            vest.RecalculateTangents();
            vest = SaveMesh(vest, "Father_Vest");

            var skin = Mat("Father_Skin", Tex("T_Superhero_Male_Ligh.png"), Tex("T_Superhero_Male_Normal.png"), new Color(0.92f, 0.86f, 0.82f), 0.32f);
            var flannel = Mat("Father_Flannel", PH("fabric_pattern_07", "diff"), PH("fabric_pattern_07", "nor_gl"), new Color(0.8f, 0.7f, 0.66f), 0.15f, 9f);
            var jeans = Mat("Father_Jeans", PH("denim_fabric", "diff"), PH("denim_fabric", "nor_gl"), new Color(0.55f, 0.6f, 0.72f), 0.15f, 5f);
            var boots = Mat("Father_Boots", PH("leather_red_02", "diff"), PH("leather_red_02", "nor_gl"), new Color(0.45f, 0.31f, 0.21f), 0.4f, 6f);
            var vestMat = Mat("Father_Vest", PH("denim_fabric", "diff"), PH("denim_fabric", "nor_gl"), new Color(0.42f, 0.44f, 0.3f), 0.1f, 4f);
            vestMat.SetFloat("_Cull", 0f);
            smr.sharedMesh = mesh;
            smr.sharedMaterials = new[] { skin, flannel, jeans, boots };
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = ShadowCastingMode.On;

            var vestGo = Object.Instantiate(smr.gameObject, smr.transform.parent);
            vestGo.name = "Vest";
            var vsmr = vestGo.GetComponent<SkinnedMeshRenderer>();
            vsmr.sharedMesh = vest;
            vsmr.sharedMaterials = new[] { vestMat };

            // Ojos y cejas del propio modelo; pelo y barba colgados del hueso de la cabeza.
            var hair = Mat("Father_Hair", Tex("T_Hair_1_BaseColor.png"), Tex("T_Hair_1_Normal.png"), new Color(0.42f, 0.32f, 0.26f), 0.25f);
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.name == "Eyes") r.sharedMaterial = Mat("Father_Eyes", Tex("T_Eye_Brown.png"), Tex("T_Eye_Normal.png"), Color.white, 0.8f);
                if (r.name == "Eyebrows") r.sharedMaterial = hair;
            }
            if (!body.TryGetComponent(out Animator anim)) anim = body.AddComponent<Animator>();
            anim.avatar = AssetDatabase.LoadAllAssetsAtPath(QuaterniusImport.Body).OfType<Avatar>().First();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            AttachHair(headBone, "Hair_SimpleParted", hair);
            AttachHair(headBone, "Hair_Beard", hair);

            // Props.
            var bark = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_pine_bark.mat");
            var handR = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var handL = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var spine3 = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            var club = Prop(handR, "Club", ChunkyMeshGenerator.Limb("P_Club", 0.72f, 0.028f, 0.052f, 8), bark);
            Orient(club.transform, handR, anim, new Vector3(0f, -0.02f, 0.03f));
            var gunMat = Mat("Father_FlareGun", null, null, new Color(0.85f, 0.3f, 0.06f), 0.45f);
            var gun = Prop(handR, "FlareGun", ChunkyMeshGenerator.Box("P_FlareGun", new Vector3(0.05f, 0.07f, 0.22f)), gunMat);
            Orient(gun.transform, handR, anim, new Vector3(0f, 0.02f, 0.06f), forwardAlongFingers: true);
            gun.SetActive(false);
            var torchMat = Mat("Father_Torch", null, null, new Color(0.12f, 0.12f, 0.13f), 0.6f);
            var torch = Prop(handL, "Torch", ChunkyMeshGenerator.Log("P_Torch", 0.22f, 0.025f, 10), torchMat);
            Orient(torch.transform, handL, anim, new Vector3(0f, 0.01f, 0.05f), forwardAlongFingers: true);
            var pack = Mat("Father_Backpack", PH("denim_fabric", "diff"), PH("denim_fabric", "nor_gl"), new Color(0.4f, 0.32f, 0.24f), 0.1f, 3f);
            var backpack = Prop(spine3, "Backpack", ChunkyMeshGenerator.Rock("P_Backpack", 3, new Vector3(0.34f, 0.44f, 0.18f), 0.85f, 0.02f), pack);
            // En la pose de enlace el modelo mira hacia -Z del Visual: la espalda queda hacia +Z.
            backpack.transform.position = spine3.position + body.transform.forward * 0.19f - Vector3.up * 0.2f;
            backpack.transform.rotation = body.transform.rotation;
            // Funda del garrote: cruzado sobre la mochila, el mango asoma por encima del hombro derecho (se alcanza
            // por encima del hombro) y la punta baja hacia la cadera izquierda. El modelo mira a -Z: su derecha es -X.
            Vector3 back = body.transform.forward, right = -body.transform.right;
            var sheath = new GameObject("ClubSheath").transform;
            sheath.SetParent(spine3, false);
            sheath.position = spine3.position + back * 0.3f + right * 0.16f + Vector3.up * 0.16f;
            sheath.rotation = Quaternion.LookRotation(back, Vector3.up) * Quaternion.FromToRotation(Vector3.down, new Vector3(-0.45f, -1f, 0f).normalized);
            sheath.gameObject.layer = spine3.gameObject.layer;

            // Linterna: la luz existente pasa a seguir la mano.
            var flash = player.transform.Find("Flashlight");
            var fa = player.GetComponent<ProjectRidge.Player.FatherAnimator>();
            if (fa != null) Object.DestroyImmediate(fa);
            fa = body.AddComponent<ProjectRidge.Player.FatherAnimator>();
            var so = new SerializedObject(fa);
            so.FindProperty("controller").objectReferenceValue = pc;
            so.FindProperty("combat").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerCombat>();
            so.FindProperty("health").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerHealth>();
            so.FindProperty("interactor").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerInteractor>();
            so.FindProperty("healing").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerHealing>();
            so.FindProperty("flashlight").objectReferenceValue = player.GetComponent<ProjectRidge.Player.FlashlightController>();
            so.FindProperty("flashlightLight").objectReferenceValue = flash;
            so.FindProperty("club").objectReferenceValue = club;
            so.FindProperty("clubSheath").objectReferenceValue = sheath;
            so.FindProperty("flareGun").objectReferenceValue = gun;
            so.FindProperty("torchProp").objectReferenceValue = torch;
            so.ApplyModifiedPropertiesWithoutUndo();

            // La bengala sale de la pistola.
            var cso = new SerializedObject(player.GetComponent<ProjectRidge.Player.PlayerCombat>());
            cso.FindProperty("muzzle").objectReferenceValue = gun.transform;
            cso.ApplyModifiedPropertiesWithoutUndo();

            // Lanzar piedras (distracción).
            if (!player.TryGetComponent(out ProjectRidge.Player.PlayerThrow thrower)) thrower = player.AddComponent<ProjectRidge.Player.PlayerThrow>();
            var tso = new SerializedObject(thrower);
            tso.FindProperty("controller").objectReferenceValue = pc;
            tso.FindProperty("combat").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerCombat>();
            tso.FindProperty("health").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerHealth>();
            tso.FindProperty("inventory").objectReferenceValue = player.GetComponent<ProjectRidge.Items.PlayerInventory>();
            tso.FindProperty("interactor").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerInteractor>();
            tso.FindProperty("animator").objectReferenceValue = fa;
            tso.FindProperty("stoneItem").objectReferenceValue = ItemsSetupBuilder.EnsureItem("Stone", "Stone", "STONE", 4);
            tso.FindProperty("stonePrefab").objectReferenceValue = ItemsSetupBuilder.StonePrefab();
            tso.FindProperty("hand").objectReferenceValue = handR;
            tso.ApplyModifiedPropertiesWithoutUndo();
            var iso = new SerializedObject(player.GetComponent<ProjectRidge.Player.PlayerInputHandler>());
            iso.FindProperty("cycleObject").objectReferenceValue = ItemsSetupBuilder.ActionRef("CycleObject");
            iso.ApplyModifiedPropertiesWithoutUndo();

            // Capa de render 2: solo el papá recibe la luz de relleno que lo mantiene legible en la oscuridad.
            foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = 1u | 2u;

            // El audio escucha los pasos del nuevo animador.
            var audio = Object.FindFirstObjectByType<ProjectRidge.Core.AudioDirector>();
            if (audio != null)
            {
                var aso = new SerializedObject(audio);
                aso.FindProperty("fatherAnimator").objectReferenceValue = fa;
                aso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void AttachHair(Transform head, string file, Material mat)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HairDir + "/" + file + ".fbx");
            var tmp = (GameObject)Object.Instantiate(prefab);
            var fbxHead = tmp.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
            var renderer = tmp.GetComponentsInChildren<Renderer>().First();
            var mf = renderer.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : (renderer as SkinnedMeshRenderer).sharedMesh;
            // Transformación de la malla relativa al hueso de la cabeza en el FBX (misma pose de enlace que el cuerpo).
            Matrix4x4 rel = fbxHead.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Object.DestroyImmediate(tmp);
            var go = new GameObject(file);
            go.layer = head.gameObject.layer;
            go.transform.SetParent(head, false);
            go.transform.localPosition = rel.GetColumn(3);
            go.transform.localRotation = rel.rotation;
            go.transform.localScale = rel.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        static GameObject Prop(Transform parent, string name, Mesh mesh, Material mat)
        {
            mesh = SaveMesh(mesh, "Prop_" + name);
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// Coloca un objeto en la mano: el eje -Y de las mallas (miembros) o +Z (cajas) apunta a lo largo del puño
        /// (perpendicular a los dedos) para un garrote, o a lo largo de los dedos para pistola/linterna.
        /// </summary>
        static void Orient(Transform prop, Transform hand, Animator anim, Vector3 offset, bool forwardAlongFingers = false)
        {
            var middle = hand.Find(hand.name.Replace("hand", "middle_01"));
            var thumb = hand.Find(hand.name.Replace("hand", "thumb_01"));
            Vector3 fingers = (middle != null ? middle.position - hand.position : hand.up).normalized;
            Vector3 toThumb = (thumb != null ? thumb.position - hand.position : hand.right).normalized;
            Vector3 palmAxis = Vector3.Cross(fingers, toThumb).normalized; // atraviesa el puño
            prop.position = hand.position + fingers * 0.08f + hand.TransformVector(offset);
            if (forwardAlongFingers)
                prop.rotation = Quaternion.LookRotation(fingers, palmAxis);
            else
                // Garrote: la malla cuelga hacia -Y; la punta debe salir por el lado del pulgar.
                prop.rotation = Quaternion.FromToRotation(Vector3.down, toThumb);
        }

        // ------------------------------------------------------------------ El Lurker

        class LurkerAsset { public Mesh mesh; public Avatar avatar; public Vector3[] bonePositions; public string[] boneNames; }

        static readonly Dictionary<string, float> Lengthen = new Dictionary<string, float>
        {
            { "spine_02", 1.12f }, { "spine_03", 1.12f }, { "neck_01", 1.1f }, { "Head", 2.1f },
            { "upperarm_l", 1.25f }, { "lowerarm_l", 1.45f }, { "hand_l", 1.55f },
            { "upperarm_r", 1.2f }, { "lowerarm_r", 1.35f }, { "hand_r", 1.45f },
            { "calf_l", 1.18f }, { "foot_l", 1.22f }, { "calf_r", 1.18f }, { "foot_r", 1.22f },
        };

        static readonly Dictionary<string, float> Thin = new Dictionary<string, float>
        {
            { "upperarm", 0.52f }, { "lowerarm", 0.55f }, { "thigh", 0.58f }, { "calf", 0.6f },
            { "spine_01", 0.72f }, { "spine_02", 0.7f }, { "spine_03", 0.74f }, { "pelvis", 0.78f }, { "neck_01", 0.62f }, { "clavicle", 0.8f },
        };

        static float ThinFor(string bone)
        {
            foreach (var kv in Thin) if (bone.StartsWith(kv.Key)) return kv.Value;
            return 1f;
        }

        static LurkerAsset LurkerAssets()
        {
            var tmp = InstantiateBody(null, 10, "LurkerBake");
            var smr = BodyRenderer(tmp);
            var bones = smr.bones;
            Transform B(string name) => bones.FirstOrDefault(b => b.name == name);

            // 1) Alargar: la posición local de cada hueso hijo se multiplica (piel interpola entre huesos).
            foreach (var kv in Lengthen) { var b = B(kv.Key); if (b != null) b.localPosition *= kv.Value; }
            foreach (var b in bones)
                if (b.name.Contains("_02_") || b.name.Contains("_03_") || b.name.Contains("_04_")) b.localPosition *= b.name.EndsWith("_l") ? 2.1f : 1.8f;
            // Cráneo estirado (solo para hornear; luego se restaura la escala).
            var head = B("Head");
            head.localScale = new Vector3(0.9f, 1.25f, 1.35f);

            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            head.localScale = Vector3.one;

            // 2) Adelgazar: cada vértice se acerca al eje de sus huesos (ponderado por los pesos).
            var v = baked.vertices;
            var bw = smr.sharedMesh.boneWeights;
            Matrix4x4 toWorld = smr.transform.localToWorldMatrix, toLocal = smr.transform.worldToLocalMatrix;
            Vector3 Pull(Vector3 pw, int bi)
            {
                var b = bones[bi];
                float f = ThinFor(b.name);
                if (f >= 0.999f || b.childCount == 0) return pw;
                Transform child = null;
                foreach (Transform c in b) if (!c.name.Contains("thigh") && (child == null || c.name.StartsWith("spine") || c.name.StartsWith("lowerarm") || c.name.StartsWith("calf") || c.name.StartsWith("neck"))) child = c;
                if (b.name == "pelvis") child = B("spine_01");
                Vector3 a = b.position, c2 = child.position, ab = c2 - a;
                float t = Mathf.Clamp01(Vector3.Dot(pw - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                Vector3 axis = a + ab * t;
                return axis + (pw - axis) * f;
            }
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 pw = toWorld.MultiplyPoint3x4(v[i]);
                var w = bw[i];
                Vector3 acc = Pull(pw, w.boneIndex0) * w.weight0 + Pull(pw, w.boneIndex1) * w.weight1 + Pull(pw, w.boneIndex2) * w.weight2 + Pull(pw, w.boneIndex3) * w.weight3;
                float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                v[i] = toLocal.MultiplyPoint3x4(sum > 0f ? acc / sum : pw);
            }

            // 3) Nueva malla enlazada a la nueva pose.
            var mesh = Object.Instantiate(smr.sharedMesh);
            mesh.vertices = v;
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * toWorld;
            mesh.bindposes = bind;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, "Lurker_Body");

            // 4) Avatar humanoide del esqueleto alargado.
            var srcAvatar = AssetDatabase.LoadAllAssetsAtPath(QuaterniusImport.Body).OfType<Avatar>().First();
            // Se conserva la pose de referencia (T-pose) que Unity calculó al importar; solo cambian las longitudes.
            var hd = srcAvatar.humanDescription;
            var byName = tmp.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var skel = hd.skeleton;
            for (int i = 0; i < skel.Length; i++)
                if (byName.TryGetValue(skel[i].name, out var t) && t != tmp.transform) skel[i].position = t.localPosition;
            hd.skeleton = skel;
            tmp.name = skel[0].name;
            var avatar = AvatarBuilder.BuildHumanAvatar(tmp, hd);
            avatar.name = "Lurker_Avatar";
            string avPath = OutDir + "/Lurker_Avatar.asset";
            if (AssetDatabase.LoadAssetAtPath<Avatar>(avPath) != null) AssetDatabase.DeleteAsset(avPath);
            AssetDatabase.CreateAsset(avatar, avPath);

            var asset = new LurkerAsset
            {
                mesh = mesh,
                avatar = avatar,
                boneNames = tmp.GetComponentsInChildren<Transform>().Select(t => t.name).ToArray(),
                bonePositions = tmp.GetComponentsInChildren<Transform>().Select(t => t.localPosition).ToArray(),
            };
            Object.DestroyImmediate(tmp);
            return asset;
        }

        static void BuildLurker(GameObject lurker, AnimatorController ctrl, LurkerAsset asset)
        {
            var old = lurker.transform.Find("Visual");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var body = InstantiateBody(lurker.transform, 10, "Visual");
            // Misma pose alargada que la malla horneada.
            var all = body.GetComponentsInChildren<Transform>();
            for (int i = 0; i < all.Length && i < asset.boneNames.Length; i++)
                if (all[i].name == asset.boneNames[i]) all[i].localPosition = asset.bonePositions[i];

            var (skinA, skinN) = ChunkyMeshGenerator.PatternTextures(ChunkyMeshGenerator.Pattern.Skin, 66);
            var skin = Mat("Lurker_Skin", skinA, skinN, new Color(0.55f, 0.55f, 0.6f), 0.55f, 3f);
            var eye = Mat("Lurker_Eye", null, null, new Color(0.3f, 0.02f, 0.02f), 0.9f);
            eye.EnableKeyword("_EMISSION");
            eye.SetColor("_EmissionColor", new Color(2f, 0.1f, 0.05f));
            Renderer eyesRenderer = null;
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.name == "SuperHero_Male") { r.sharedMesh = asset.mesh; r.sharedMaterial = skin; }
                else if (r.name == "Eyes") { r.sharedMaterial = eye; r.shadowCastingMode = ShadowCastingMode.Off; eyesRenderer = r; }
                else r.gameObject.SetActive(false); // sin cejas
            }
            if (!body.TryGetComponent(out Animator anim)) anim = body.AddComponent<Animator>();
            anim.avatar = asset.avatar;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            // Siempre anima: fuera de cámara sus pasos y tics siguen sonando (clave para el terror).
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Alto: ~2.2 m encorvado, ~2.6 m erguido.
            body.transform.localScale = Vector3.one * 1.3f;
            if (lurker.TryGetComponent(out CapsuleCollider capsule)) { capsule.height = 2.3f; capsule.center = Vector3.up * 1.15f; capsule.radius = 0.32f; }
            if (lurker.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) { agent.height = 2.3f; agent.radius = 0.35f; }

            var old2 = lurker.GetComponent<ProjectRidge.AI.LurkerAnimator>();
            if (old2 != null) Object.DestroyImmediate(old2);
            var la = body.AddComponent<ProjectRidge.AI.LurkerAnimator>();
            var enemy = lurker.GetComponent<ProjectRidge.AI.EnemyController>();
            var so = new SerializedObject(la);
            so.FindProperty("enemy").objectReferenceValue = enemy;
            so.FindProperty("agent").objectReferenceValue = lurker.GetComponent<UnityEngine.AI.NavMeshAgent>();
            so.FindProperty("config").objectReferenceValue = enemy.Config;
            so.ApplyModifiedPropertiesWithoutUndo();

            var fb = lurker.GetComponent<ProjectRidge.AI.EnemyStateFeedback>();
            var fso = new SerializedObject(fb);
            var eyes = fso.FindProperty("eyes");
            eyes.arraySize = 1;
            eyes.GetArrayElementAtIndex(0).objectReferenceValue = eyesRenderer;
            // Haz leve desde los ojos durante la caza (POLISH_PASS 3.5): un cono aditivo pegado a la cabeza.
            var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            var beamGo = new GameObject("EyeBeam");
            beamGo.layer = 10;
            beamGo.transform.SetParent(headBone, false);
            beamGo.transform.position = eyesRenderer != null ? eyesRenderer.bounds.center : headBone.position;
            beamGo.transform.rotation = Quaternion.LookRotation(lurker.transform.forward + Vector3.down * 0.25f);
            float s = 1f / Mathf.Max(0.001f, headBone.lossyScale.x);
            beamGo.transform.localScale = new Vector3(0.45f, 0.45f, 5f) * s;
            beamGo.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Meshes/UFO_BeamCone.asset");
            var beamR = beamGo.AddComponent<MeshRenderer>();
            beamR.sharedMaterial = EyeBeamMaterial();
            beamR.shadowCastingMode = ShadowCastingMode.Off;
            beamR.receiveShadows = false;
            beamR.enabled = false;
            fso.FindProperty("eyeBeam").objectReferenceValue = beamR;
            fso.ApplyModifiedPropertiesWithoutUndo();

            // El "ojo" de la percepción sigue la cabeza real.
            var eyePoint = lurker.transform.Find("EyePoint");
            if (eyePoint != null) eyePoint.position = anim.GetBoneTransform(HumanBodyBones.Head).position;
        }

        /// <summary>Material del haz de los ojos: el mismo volumen aditivo de los reflectores, rojo y tenue.</summary>
        static Material EyeBeamMaterial()
        {
            string path = OutDir + "/Lurker_EyeBeam.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("ProjectRidge/VolumetricBeam")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_Color", new Color(1f, 0.08f, 0.04f));
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", 0.6f);
            if (m.HasProperty("_CameraFade")) m.SetFloat("_CameraFade", 2f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ Avistamiento con guion

        /// <summary>
        /// Un Lurker sin IA que cruza la otra orilla del arroyo cuando el papá pisa el sendero (copia del visual de
        /// un Lurker de la escena, con su Animator). Trigger en el sendero y ruta por puntos.
        /// </summary>
        public static string BuildApparition(Vector3 triggerPos, Vector3[] path)
        {
            var old = GameObject.Find("Apparition");
            if (old != null) Object.DestroyImmediate(old);
            var source = Object.FindObjectsByType<ProjectRidge.AI.EnemyController>(FindObjectsSortMode.None).FirstOrDefault();
            if (source == null) return "no lurker to copy";
            var root = new GameObject("Apparition");
            root.transform.position = triggerPos;
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 3f, 7f);
            box.center = Vector3.up * 1.2f;
            var body = Object.Instantiate(source.transform.Find("Visual").gameObject, root.transform);
            body.name = "ApparitionBody";
            var la = body.GetComponent<ProjectRidge.AI.LurkerAnimator>();
            if (la != null) Object.DestroyImmediate(la);
            var points = new Transform[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                var p = new GameObject("P" + i).transform;
                p.SetParent(root.transform, false);
                p.position = path[i];
                points[i] = p;
            }
            var ap = root.AddComponent<ProjectRidge.AI.ScriptedApparition>();
            var so = new SerializedObject(ap);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("animator").objectReferenceValue = body.GetComponent<Animator>();
            var arr = so.FindProperty("path");
            arr.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            body.SetActive(false);
            return "apparition ok";
        }

        // ------------------------------------------------------------------ Animator Controllers

        static AnimatorController NewController(string name)
        {
            string path = OutDir + "/" + name + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        static AnimatorState Tree(AnimatorController c, string name, string param, (string clip, float threshold, float timeScale)[] children, Dictionary<string, AnimationClip> clips)
        {
            var state = c.CreateBlendTreeInController(name, out BlendTree tree, 0);
            tree.blendParameter = param;
            tree.useAutomaticThresholds = false;
            foreach (var ch in children) tree.AddChild(clips[ch.clip], ch.threshold);
            var arr = tree.children;
            for (int i = 0; i < arr.Length; i++) arr[i].timeScale = children[i].timeScale;
            tree.children = arr;
            return state;
        }

        static AnimatorState Action(AnimatorStateMachine sm, string name, AnimationClip clip, float speed)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            s.speed = speed;
            return s;
        }

        static void FromAny(AnimatorStateMachine sm, AnimatorState to, string param, AnimatorConditionMode mode = AnimatorConditionMode.If, float duration = 0.08f)
        {
            var t = sm.AddAnyStateTransition(to);
            t.AddCondition(mode, 0f, param);
            t.duration = duration;
            t.canTransitionToSelf = false;
            t.hasExitTime = false;
        }

        static void Back(AnimatorState from, AnimatorState to, float exitTime, float duration = 0.2f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
        }

        /// <summary>
        /// Velocidad natural (m/s) de los clips de locomoción del cuerpo base (1.80 m), medida por la zancada de los
        /// pies con MeasureNaturalSpeed (2026-10-20): los clips de Quaternius vienen "in place", sin raíz que leer.
        /// </summary>
        public static readonly Dictionary<string, float> NaturalSpeed = new Dictionary<string, float>
        {
            { "Walk_Loop", 0.93f }, { "Jog_Fwd_Loop", 2.6f }, { "Sprint_Loop", 3.62f }, { "Crouch_Fwd_Loop", 0.69f },
            // Lurker (cuerpo a escala 1.3, misma medición).
            { "Lurker:Zombie_Walk_Fwd_Loop", 1.15f }, { "Lurker:Jog_Fwd_Loop", 3.34f }, { "Lurker:Sprint_Loop", 4.6f },
        };

        /// <summary>Límites de escala de tiempo de un clip de locomoción (POLISH_PASS 3.7).</summary>
        public const float MinClipRate = 0.85f, MaxClipRate = 1.1f;

        /// <summary>
        /// Mide la velocidad natural de un clip "in place": mientras un pie está apoyado (el más bajo), se desliza hacia
        /// atrás a la velocidad a la que el cuerpo avanzaría. Se promedia esa velocidad en todo el ciclo. QA de P0.
        /// </summary>
        public static float MeasureNaturalSpeed(GameObject body, AnimationClip clip, int samples = 120)
        {
            var copy = Object.Instantiate(body);
            copy.transform.SetPositionAndRotation(new Vector3(0f, 400f, 0f), Quaternion.identity);
            foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            var anim = copy.GetComponent<Animator>();
            // La punta (ball) es el punto que apoya más tiempo: el tobillo baja más en el vuelo del trote que al pisar.
            Transform fl = anim.GetBoneTransform(HumanBodyBones.LeftToes) ?? anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform fr = anim.GetBoneTransform(HumanBodyBones.RightToes) ?? anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var graph = UnityEngine.Playables.PlayableGraph.Create("Measure");
            var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "out", anim);
            var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
            UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
            float dt = clip.length / samples, sum = 0f;
            int count = 0;
            var ls = new Vector3[samples + 1];
            var rs = new Vector3[samples + 1];
            float floorL = float.MaxValue, floorR = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                UnityEngine.Playables.PlayableExtensions.SetTime(playable, i * dt);
                graph.Evaluate();
                ls[i] = copy.transform.InverseTransformPoint(fl.position);
                rs[i] = copy.transform.InverseTransformPoint(fr.position);
                floorL = Mathf.Min(floorL, ls[i].y);
                floorR = Mathf.Min(floorR, rs[i].y);
            }
            // Un pie apoyado se desliza hacia atrás (+Z: el modelo mira a -Z) a la velocidad del cuerpo; en el vuelo vuelve
            // hacia adelante. Velocidad = recorrido hacia atrás / tiempo yendo hacia atrás (robusto en el sprint).
            // Por si el eje está invertido: la fase de apoyo es siempre la más lenta de las dos direcciones.
            float back = 0f, backT = 0f, fwd = 0f, fwdT = 0f;
            for (int i = 1; i <= samples; i++)
                foreach (float d in new[] { ls[i].z - ls[i - 1].z, rs[i].z - rs[i - 1].z })
                {
                    if (d > 0f) { back += d; backT += dt; }
                    else { fwd -= d; fwdT += dt; }
                }
            count = backT > 0f && fwdT > 0f ? 1 : 0;
            sum = count > 0 ? Mathf.Min(back / backT, fwd / fwdT) : 0f;
            float scale = body.transform.lossyScale.x;
            graph.Destroy();
            Object.DestroyImmediate(copy);
            return count > 0 ? sum / count * scale : 0f;
        }

        static float ClipRate(string clip, float speed)
        {
            float rate = speed / NaturalSpeed[clip];
            if (rate < MinClipRate || rate > MaxClipRate)
                Debug.LogError(clip + " a " + speed + " m/s necesita " + rate.ToString("0.00") + "x (fuera de " + MinClipRate + "–" + MaxClipRate + "): ajusta PlayerMovementConfig");
            return rate;
        }

        /// <summary>Velocidad del clip de recoger del suelo (Farm_Harvest dura 2.5 s: queda en ~1.6 s).</summary>
        public const float PickUpClipSpeed = 1.6f;

        static AnimatorController FatherController()
        {
            var clips = QuaterniusImport.Clips();
            var c = NewController("Father");
            foreach (var p in new[] { "Speed" }) c.AddParameter(p, AnimatorControllerParameterType.Float);
            foreach (var p in new[] { "Crouch", "Dead" }) c.AddParameter(p, AnimatorControllerParameterType.Bool);
            foreach (var p in new[] { "Attack", "Fire", "Throw", "Hit", "PickUp", "Search", "Interact", "Heal", "Revive", "Roll" }) c.AddParameter(p, AnimatorControllerParameterType.Trigger);
            var layers = c.layers;
            layers[0].iKPass = true;
            c.layers = layers;
            var sm = c.layers[0].stateMachine;

            // Las velocidades del papá salen de los clips (POLISH_PASS 3.7): el tiempo de cada clip se escala solo por
            // velocidad de config / velocidad natural, y eso tiene que quedar entre 0.85x y 1.1x (si no, patina).
            var mv = AssetDatabase.LoadAssetAtPath<ProjectRidge.Player.PlayerMovementConfig>("Assets/_Project/Data/PlayerMovementConfig.asset");
            var loco = Tree(c, "Locomotion", "Speed", new[]
            {
                ("Idle_Loop", 0f, 1f),
                ("Walk_Loop", mv.walkSpeed, ClipRate("Walk_Loop", mv.walkSpeed)),
                ("Jog_Fwd_Loop", mv.jogSpeed, ClipRate("Jog_Fwd_Loop", mv.jogSpeed)),
                ("Sprint_Loop", mv.sprintSpeed, ClipRate("Sprint_Loop", mv.sprintSpeed)),
            }, clips);
            var crouch = Tree(c, "Crouch", "Speed", new[] { ("Crouch_Idle_Loop", 0f, 1f), ("Crouch_Fwd_Loop", mv.crouchSpeed, ClipRate("Crouch_Fwd_Loop", mv.crouchSpeed)) }, clips);
            sm.defaultState = loco;
            var toCrouch = loco.AddTransition(crouch); toCrouch.AddCondition(AnimatorConditionMode.If, 0, "Crouch"); toCrouch.duration = 0.25f; toCrouch.hasExitTime = false;
            var toStand = crouch.AddTransition(loco); toStand.AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch"); toStand.duration = 0.25f; toStand.hasExitTime = false;

            var attack = Action(sm, "Attack", clips["Sword_Regular_A"], 1.35f);
            var attackRec = Action(sm, "AttackRecover", clips["Sword_Regular_A_Rec"], 2.2f);
            FromAny(sm, attack, "Attack", duration: 0.05f);
            Back(attack, attackRec, 0.95f, 0.05f);
            Back(attackRec, loco, 0.7f, 0.2f);
            var fire = Action(sm, "Fire", clips["Pistol_Shoot"], 1f); FromAny(sm, fire, "Fire"); Back(fire, loco, 0.9f);
            var thr = Action(sm, "Throw", clips["OverhandThrow"], 1.3f); FromAny(sm, thr, "Throw"); Back(thr, loco, 0.85f);
            var hit = Action(sm, "Hit", clips["Hit_Chest"], 1f); FromAny(sm, hit, "Hit", duration: 0.04f); Back(hit, loco, 0.9f, 0.15f);
            // Recoger del suelo: se agacha flexionando las rodillas hasta el suelo y vuelve (no el de recoger de una mesa).
            var pick = Action(sm, "PickUp", clips["Farm_Harvest"], PickUpClipSpeed); FromAny(sm, pick, "PickUp", duration: 0.12f); Back(pick, loco, 0.86f, 0.25f);
            var search = Action(sm, "Search", clips["Chest_Open"], 1.2f); FromAny(sm, search, "Search", duration: 0.15f); Back(search, loco, 0.9f);
            var inter = Action(sm, "Interact", clips["Interact"], 2f); FromAny(sm, inter, "Interact", duration: 0.15f); Back(inter, loco, 0.9f);
            var heal = Action(sm, "Heal", clips["Consume"], 1.2f); FromAny(sm, heal, "Heal", duration: 0.15f); Back(heal, loco, 0.9f);
            // Se suelta del haz del OVNI: tirón y rueda por el suelo (POLISH_PASS 3.4).
            var roll = Action(sm, "Roll", clips["Roll"], 1.15f); FromAny(sm, roll, "Roll", duration: 0.06f); Back(roll, loco, 0.88f, 0.2f);
            var death = Action(sm, "Death", clips["Death01"], 1f); FromAny(sm, death, "Dead", duration: 0.1f);
            var revive = Action(sm, "Revive", clips["LayToIdle"], 1f);
            var rv = death.AddTransition(revive); rv.AddCondition(AnimatorConditionMode.If, 0, "Revive"); rv.hasExitTime = false; rv.duration = 0.1f;
            Back(revive, loco, 0.9f);

            // Capa del brazo izquierdo: sostener la linterna (peso desde el script).
            var mask = new AvatarMask();
            foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
                if (part != AvatarMaskBodyPart.LastBodyPart) mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.LeftFingers);
            string maskPath = OutDir + "/Mask_LeftArm.mask";
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath) != null) AssetDatabase.DeleteAsset(maskPath);
            AssetDatabase.CreateAsset(mask, maskPath);
            c.AddLayer("Torch");
            layers = c.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            c.layers = layers;
            var torch = c.layers[1].stateMachine.AddState("Torch");
            torch.motion = clips["Idle_Torch_Loop"];
            EditorUtility.SetDirty(c);
            return c;
        }

        static AnimatorController LurkerController()
        {
            var clips = QuaterniusImport.Clips();
            var c = NewController("Lurker");
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (var p in new[] { "Hunting", "Stunned", "Dead", "Down" }) c.AddParameter(p, AnimatorControllerParameterType.Bool);
            c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var sm = c.layers[0].stateMachine;
            // Locomoción a su velocidad natural (cuerpo a escala 1.3): los umbrales son las velocidades medidas, sin escalar.
            var prowl = Tree(c, "Prowl", "Speed", new[] { ("Zombie_Idle_Loop", 0f, 0.8f), ("Zombie_Walk_Fwd_Loop", NaturalSpeed["Lurker:Zombie_Walk_Fwd_Loop"], 1f), ("Zombie_Walk_Fwd_Loop", NaturalSpeed["Lurker:Zombie_Walk_Fwd_Loop"] * MaxClipRate, MaxClipRate) }, clips);
            var hunt = Tree(c, "Hunt", "Speed", new[] { ("Zombie_Idle_Loop", 0f, 1.6f), ("Jog_Fwd_Loop", NaturalSpeed["Lurker:Jog_Fwd_Loop"], 1f), ("Sprint_Loop", NaturalSpeed["Lurker:Sprint_Loop"], 1f) }, clips);
            sm.defaultState = prowl;
            var toHunt = prowl.AddTransition(hunt); toHunt.AddCondition(AnimatorConditionMode.If, 0, "Hunting"); toHunt.duration = 0.2f; toHunt.hasExitTime = false;
            var toProwl = hunt.AddTransition(prowl); toProwl.AddCondition(AnimatorConditionMode.IfNot, 0, "Hunting"); toProwl.duration = 0.35f; toProwl.hasExitTime = false;
            var attack = Action(sm, "Attack", clips["Zombie_Scratch"], 1.12f);
            FromAny(sm, attack, "Attack", duration: 0.06f);
            Back(attack, hunt, 0.92f, 0.15f);
            var knock = Action(sm, "Knockback", clips["Hit_Knockback"], 1f);
            FromAny(sm, knock, "Stunned", duration: 0.05f);
            var dazed = Action(sm, "Dazed", clips["Zombie_Idle_Loop"], 0.5f);
            Back(knock, dazed, 0.95f, 0.1f);
            var outD = dazed.AddTransition(hunt); outD.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned"); outD.duration = 0.15f; outD.hasExitTime = false;
            var outK = knock.AddTransition(hunt); outK.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned"); outK.duration = 0.15f; outK.hasExitTime = false;
            var death = Action(sm, "Death", clips["Death01"], 0.85f);
            FromAny(sm, death, "Dead", duration: 0.08f);
            // Derribo por la espalda: cae (el clip de muerte, que queda en su último cuadro) y, al pasar el tiempo, se
            // levanta. Encima, la capa procedural convulsiona.
            var down = Action(sm, "Down", clips["Death01"], 1f);
            FromAny(sm, down, "Down", duration: 0.08f);
            var getUp = Action(sm, "GetUp", clips["LayToIdle"], 0.9f);
            var up = down.AddTransition(getUp); up.AddCondition(AnimatorConditionMode.IfNot, 0, "Down"); up.duration = 0.15f; up.hasExitTime = false;
            Back(getUp, prowl, 0.9f, 0.25f);
            EditorUtility.SetDirty(c);
            return c;
        }
    }
}
