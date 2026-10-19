using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Personajes del PoC hechos en el proyecto (sin modelos de terceros): el papá y el Lurker como rigs de
    /// articulaciones con piezas redondeadas (torno, elipsoides) y texturas fotográficas (Poly Haven CC0) o
    /// procedurales. Los animan FatherAnimator y LurkerAnimator. Reemplaza el "Visual" de cada uno en la escena activa.
    /// </summary>
    public static class CharacterBuilder
    {
        const string MatDir = "Assets/_Project/Art/Characters";

        [MenuItem("Project Ridge/Characters/Rebuild Characters in Active Scene")]
        public static string Build()
        {
            PolyHavenMaterialBuilder.EnsureFolder(MatDir);
            var log = new System.Text.StringBuilder();
            var player = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerController>();
            BuildFather(player.gameObject);
            log.AppendLine("father ok");
            foreach (var enemy in Object.FindObjectsByType<ProjectRidge.AI.EnemyController>(FindObjectsSortMode.None))
            {
                BuildLurker(enemy.gameObject);
                log.AppendLine("lurker ok: " + enemy.name);
            }
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ------------------------------------------------------------------ Materiales

        static Material Mat(string name, Color color, float smoothness, Material from = null, float tiling = 1f)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = from != null ? new Material(from) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (from != null) m.CopyPropertiesFromMaterial(from);
            m.SetColor("_BaseColor", color);
            if (from == null) m.SetFloat("_Smoothness", smoothness);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material PolyHaven(string id, float smooth)
        {
            var set = PolyHavenMaterialBuilder.BuildTextureSet(PolyHavenMaterialBuilder.TexturesRoot + "/" + id, id);
            return PolyHavenMaterialBuilder.BuildLitMaterial(id, set, smooth);
        }

        // ------------------------------------------------------------------ Piezas

        static Transform Joint(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.gameObject.layer = parent.gameObject.layer;
            return t;
        }

        static GameObject Part(Transform joint, string name, Mesh mesh, Material mat, Vector3 localPos, Vector3 localEuler = default)
        {
            var go = new GameObject(name);
            go.layer = joint.gameObject.layer;
            go.transform.SetParent(joint, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveGI = ReceiveGI.LightProbes;
            return go;
        }

        static Mesh S(Mesh m) => ChunkyMeshGenerator.Save(m);
        static Mesh Limb(string n, float len, float r0, float r1, float cz = 1f) => S(ChunkyMeshGenerator.Limb(n, len, r0, r1, 10, new Vector2(1f, cz)));
        static Mesh Ell(string n, Vector3 size) => S(ChunkyMeshGenerator.Ellipsoid(n, size));

        static Transform ReplaceVisual(GameObject owner, int layer)
        {
            var old = owner.transform.Find("Visual");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Visual").transform;
            root.gameObject.layer = layer;
            root.SetParent(owner.transform, false);
            return root;
        }

        // ------------------------------------------------------------------ El papá

        static void BuildFather(GameObject player)
        {
            var skin = Mat("Father_Skin", new Color(0.72f, 0.53f, 0.42f), 0.32f);
            var hair = Mat("Father_Hair", new Color(0.13f, 0.09f, 0.07f), 0.12f);
            var jacket = Mat("Father_Jacket", new Color(0.46f, 0.47f, 0.32f), 0f, PolyHaven("denim_fabric", 0.4f), 2.5f);
            var flannel = Mat("Father_Flannel", new Color(0.85f, 0.75f, 0.72f), 0f, PolyHaven("fabric_pattern_07", 0.5f), 3.5f);
            var jeans = Mat("Father_Jeans", new Color(0.62f, 0.66f, 0.78f), 0f, PolyHaven("denim_fabric", 0.4f), 2.5f);
            var boots = Mat("Father_Boots", new Color(0.33f, 0.22f, 0.16f), 0f, PolyHaven("leather_red_02", 0.5f), 3f);
            var beanie = Mat("Father_Beanie", new Color(0.24f, 0.25f, 0.28f), 0f, PolyHaven("denim_fabric", 0.4f), 5f);
            var pack = Mat("Father_Backpack", new Color(0.42f, 0.33f, 0.24f), 0f, PolyHaven("denim_fabric", 0.4f), 2f);
            var bark = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_pine_bark.mat");
            var gunMat = Mat("Father_FlareGun", new Color(0.85f, 0.32f, 0.07f), 0.45f);

            var pc = player.GetComponent<ProjectRidge.Player.PlayerController>();
            var so = new SerializedObject(pc);
            so.FindProperty("visualRoot").objectReferenceValue = null; // el rig se agacha solo, sin escalar
            so.ApplyModifiedPropertiesWithoutUndo();

            var root = ReplaceVisual(player, 9);
            var hips = Joint(root, "Hips", new Vector3(0f, 0.95f, 0f));
            Part(hips, "Pelvis", Ell("F_Pelvis", new Vector3(0.36f, 0.24f, 0.25f)), jeans, new Vector3(0f, 0.02f, 0f));
            Part(hips, "Belt", S(ChunkyMeshGenerator.Lathe("F_Belt", new[] { new Vector2(0.172f, 0.04f), new Vector2(0.178f, 0.0f), new Vector2(0.172f, -0.03f) }, 14, new Vector2(1f, 0.72f))), boots, new Vector3(0f, 0.11f, 0f));
            var spine = Joint(hips, "Spine", new Vector3(0f, 0.1f, 0f));
            var chest = Joint(spine, "Chest", new Vector3(0f, 0.22f, 0f));
            var torso = new[] { new Vector2(0f, 0.27f), new Vector2(0.11f, 0.265f), new Vector2(0.19f, 0.225f), new Vector2(0.215f, 0.15f), new Vector2(0.21f, 0.02f), new Vector2(0.195f, -0.12f), new Vector2(0.185f, -0.22f), new Vector2(0.178f, -0.3f), new Vector2(0f, -0.31f) };
            Part(chest, "Torso", S(ChunkyMeshGenerator.Lathe("F_Torso", torso, 16, new Vector2(1f, 0.62f), y => y > -0.05f && y < 0.2f ? 0.015f : 0f)), jacket, Vector3.zero);
            Part(chest, "Collar", S(ChunkyMeshGenerator.Lathe("F_Collar", new[] { new Vector2(0.075f, 0.05f), new Vector2(0.09f, 0.02f), new Vector2(0.1f, -0.01f) }, 12, new Vector2(1f, 0.9f))), flannel, new Vector3(0f, 0.25f, 0f));
            // Mochila con saco de dormir enrollado.
            Part(chest, "Backpack", S(ChunkyMeshGenerator.Rock("F_Backpack", 3, new Vector3(0.34f, 0.42f, 0.17f), 0.85f, 0.02f)), pack, new Vector3(0f, -0.16f, -0.2f));
            Part(chest, "Bedroll", S(ChunkyMeshGenerator.Log("F_Bedroll", 0.38f, 0.065f, 10)), Mat("Father_Bedroll", new Color(0.3f, 0.38f, 0.48f), 0.2f), new Vector3(0f, 0.17f, -0.2f), new Vector3(0f, 90f, 0f));
            Part(chest, "StrapL", Limb("F_Strap", 0.42f, 0.022f, 0.022f, 0.4f), pack, new Vector3(-0.1f, 0.23f, 0.11f), new Vector3(8f, 0f, 0f));
            Part(chest, "StrapR", Limb("F_Strap", 0.42f, 0.022f, 0.022f, 0.4f), pack, new Vector3(0.1f, 0.23f, 0.11f), new Vector3(8f, 0f, 0f));

            var neck = Joint(chest, "Neck", new Vector3(0f, 0.24f, 0f));
            Part(neck, "NeckMesh", Ell("F_Neck", new Vector3(0.11f, 0.14f, 0.11f)), skin, new Vector3(0f, 0.04f, 0f));
            var head = Joint(neck, "Head", new Vector3(0f, 0.13f, 0f));
            Part(head, "Skull", Ell("F_Head", new Vector3(0.2f, 0.25f, 0.22f)), skin, new Vector3(0f, 0.04f, 0.01f));
            Part(head, "Nose", Ell("F_Nose", new Vector3(0.035f, 0.055f, 0.05f)), skin, new Vector3(0f, 0.03f, 0.115f));
            Part(head, "EarL", Ell("F_Ear", new Vector3(0.025f, 0.06f, 0.04f)), skin, new Vector3(-0.1f, 0.03f, 0f));
            Part(head, "EarR", Ell("F_Ear", new Vector3(0.025f, 0.06f, 0.04f)), skin, new Vector3(0.1f, 0.03f, 0f));
            Part(head, "Beard", Ell("F_Beard", new Vector3(0.18f, 0.13f, 0.15f)), hair, new Vector3(0f, -0.045f, 0.05f));
            var cap = new[] { new Vector2(0f, 0.115f), new Vector2(0.06f, 0.105f), new Vector2(0.095f, 0.075f), new Vector2(0.11f, 0.03f), new Vector2(0.113f, -0.01f), new Vector2(0.118f, -0.015f), new Vector2(0.118f, -0.045f), new Vector2(0.108f, -0.05f) };
            Part(head, "Beanie", S(ChunkyMeshGenerator.Lathe("F_Beanie", cap, 14, new Vector2(1f, 1.06f))), beanie, new Vector3(0f, 0.11f, 0f));

            Transform Arm(string side, float x, out Transform elbow, out Transform hand)
            {
                var shoulder = Joint(chest, "Shoulder" + side, new Vector3(x, 0.18f, 0f));
                Part(shoulder, "Deltoid", Ell("F_Deltoid", new Vector3(0.13f, 0.12f, 0.13f)), jacket, new Vector3(0f, -0.02f, 0f));
                Part(shoulder, "UpperArm", Limb("F_UpperArm", 0.28f, 0.06f, 0.05f), flannel, Vector3.zero);
                elbow = Joint(shoulder, "Elbow" + side, new Vector3(0f, -0.29f, 0f));
                Part(elbow, "Forearm", Limb("F_Forearm", 0.25f, 0.05f, 0.04f), flannel, Vector3.zero);
                hand = Joint(elbow, "Hand" + side, new Vector3(0f, -0.27f, 0f));
                Part(hand, "Palm", Ell("F_Hand", new Vector3(0.08f, 0.1f, 0.05f)), skin, new Vector3(0f, -0.04f, 0f));
                return shoulder;
            }
            var shoulderL = Arm("L", -0.215f, out var elbowL, out var handL);
            var shoulderR = Arm("R", 0.215f, out var elbowR, out var handR);

            // Garrote en la mano derecha (apunta hacia adelante y abajo desde el puño).
            var club = new GameObject("Club");
            club.layer = 9;
            club.transform.SetParent(handR, false);
            club.transform.localPosition = new Vector3(0f, -0.06f, 0.02f);
            club.transform.localRotation = Quaternion.Euler(105f, 0f, 0f);
            Part(club.transform, "Stick", S(ChunkyMeshGenerator.Limb("F_Club", 0.72f, 0.03f, 0.05f, 8)), bark, new Vector3(0f, -0.1f, 0f), new Vector3(180f, 0f, 0f));
            // Pistola de bengalas en la izquierda (solo visible al disparar).
            var gun = new GameObject("FlareGun");
            gun.layer = 9;
            gun.transform.SetParent(handL, false);
            gun.transform.localPosition = new Vector3(0f, -0.08f, 0.04f);
            Part(gun.transform, "Body", ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Box("F_GunBody", new Vector3(0.05f, 0.06f, 0.2f))), gunMat, new Vector3(0f, 0f, 0.07f));
            Part(gun.transform, "Barrel", S(ChunkyMeshGenerator.Log("F_GunBarrel", 0.14f, 0.025f, 10)), gunMat, new Vector3(0f, 0.015f, 0.2f));
            Part(gun.transform, "Grip", ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Box("F_GunGrip", new Vector3(0.04f, 0.1f, 0.05f))), gunMat, new Vector3(0f, -0.05f, -0.01f), new Vector3(-15f, 0f, 0f));
            gun.SetActive(false);

            Transform Leg(string side, float x, out Transform knee, out Transform ankle)
            {
                var hip = Joint(hips, "Hip" + side, new Vector3(x, 0f, 0f));
                Part(hip, "Thigh", Limb("F_Thigh", 0.43f, 0.085f, 0.064f), jeans, Vector3.zero);
                knee = Joint(hip, "Knee" + side, new Vector3(0f, -0.44f, 0f));
                Part(knee, "Shin", Limb("F_Shin", 0.4f, 0.064f, 0.054f), jeans, Vector3.zero);
                ankle = Joint(knee, "Ankle" + side, new Vector3(0f, -0.42f, 0f));
                Part(ankle, "Boot", S(ChunkyMeshGenerator.Rock("F_Boot", 5, new Vector3(0.12f, 0.12f, 0.28f), 0.7f, 0.02f)), boots, new Vector3(0f, -0.11f, 0.05f));
                Part(ankle, "BootCuff", Limb("F_BootCuff", 0.08f, 0.062f, 0.06f), boots, new Vector3(0f, 0.02f, 0f));
                return hip;
            }
            var hipL = Leg("L", -0.1f, out var kneeL, out var ankleL);
            var hipR = Leg("R", 0.1f, out var kneeR, out var ankleR);

            if (!player.TryGetComponent(out ProjectRidge.Player.FatherAnimator anim)) anim = player.AddComponent<ProjectRidge.Player.FatherAnimator>();
            var aso = new SerializedObject(anim);
            aso.FindProperty("controller").objectReferenceValue = pc;
            aso.FindProperty("combat").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerCombat>();
            aso.FindProperty("health").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerHealth>();
            aso.FindProperty("club").objectReferenceValue = club;
            aso.FindProperty("flareGun").objectReferenceValue = gun;
            var jp = aso.FindProperty("j");
            void Set(string n, Object o) => jp.FindPropertyRelative(n).objectReferenceValue = o;
            Set("root", root); Set("hips", hips); Set("spine", spine); Set("chest", chest); Set("neck", neck); Set("head", head);
            Set("shoulderL", shoulderL); Set("elbowL", elbowL); Set("handL", handL);
            Set("shoulderR", shoulderR); Set("elbowR", elbowR); Set("handR", handR);
            Set("hipL", hipL); Set("kneeL", kneeL); Set("ankleL", ankleL);
            Set("hipR", hipR); Set("kneeR", kneeR); Set("ankleR", ankleR);
            aso.FindProperty("thighLength").floatValue = 0.44f;
            aso.FindProperty("shinLength").floatValue = 0.42f;
            aso.FindProperty("footHeight").floatValue = 0.12f;
            aso.ApplyModifiedPropertiesWithoutUndo();

            // La bengala sale de la mano izquierda.
            var combat = player.GetComponent<ProjectRidge.Player.PlayerCombat>();
            var cso = new SerializedObject(combat);
            cso.FindProperty("muzzle").objectReferenceValue = gun.transform;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ El Lurker

        static void BuildLurker(GameObject lurker)
        {
            var (skinA, skinN) = ChunkyMeshGenerator.PatternTextures(ChunkyMeshGenerator.Pattern.Skin, 66);
            var skin = Mat("Lurker_Skin", new Color(0.62f, 0.62f, 0.68f), 0.48f);
            skin.SetTexture("_BaseMap", skinA);
            skin.SetTexture("_BumpMap", skinN);
            skin.EnableKeyword("_NORMALMAP");
            skin.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
            var mouth = Mat("Lurker_Mouth", new Color(0.16f, 0.01f, 0.02f), 0.8f);
            var teeth = Mat("Lurker_Teeth", new Color(0.62f, 0.58f, 0.48f), 0.5f);
            var claw = Mat("Lurker_Claw", new Color(0.08f, 0.07f, 0.07f), 0.7f);
            var eye = Mat("Lurker_Eye", new Color(0.3f, 0.02f, 0.02f), 0.9f);
            eye.EnableKeyword("_EMISSION");
            eye.SetColor("_EmissionColor", new Color(2f, 0.1f, 0.05f));
            eye.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            var root = ReplaceVisual(lurker, 10);
            var hips = Joint(root, "Hips", new Vector3(0f, 1.25f, 0f));
            Part(hips, "Pelvis", Ell("L_Pelvis", new Vector3(0.22f, 0.15f, 0.15f)), skin, Vector3.zero);
            var spine = Joint(hips, "Spine", new Vector3(0f, 0.07f, 0f));
            Part(spine, "Abdomen", S(ChunkyMeshGenerator.Lathe("L_Abdomen", new[] { new Vector2(0f, 0.42f), new Vector2(0.1f, 0.4f), new Vector2(0.075f, 0.25f), new Vector2(0.085f, 0.08f), new Vector2(0.1f, 0f), new Vector2(0f, -0.02f) }, 12, new Vector2(1f, 0.75f))), skin, Vector3.zero);
            var chest = Joint(spine, "Chest", new Vector3(0f, 0.36f, 0f));
            var ribs = new[] { new Vector2(0f, 0.56f), new Vector2(0.08f, 0.54f), new Vector2(0.135f, 0.47f), new Vector2(0.145f, 0.34f), new Vector2(0.125f, 0.18f), new Vector2(0.08f, 0.05f), new Vector2(0f, 0f) };
            Part(chest, "Ribcage", S(ChunkyMeshGenerator.Lathe("L_Ribcage", ribs, 14, new Vector2(1f, 0.68f), y => Mathf.Sin(y * 40f) * 0.008f)), skin, Vector3.zero);
            // Omóplatos salientes: rompen la silueta de la espalda.
            Part(chest, "ScapulaL", Ell("L_Scapula", new Vector3(0.1f, 0.2f, 0.05f)), skin, new Vector3(-0.08f, 0.38f, -0.1f), new Vector3(-15f, 20f, 12f));
            Part(chest, "ScapulaR", Ell("L_Scapula", new Vector3(0.1f, 0.2f, 0.05f)), skin, new Vector3(0.09f, 0.4f, -0.1f), new Vector3(-15f, -20f, -12f));
            for (int i = 0; i < 7; i++)
                Part(chest, "Vertebra", Ell("L_Vertebra", new Vector3(0.045f, 0.05f, 0.05f)), skin, new Vector3(0f, 0.05f + i * 0.075f, -0.115f - Mathf.Sin(i * 0.45f) * 0.015f));

            var neck = Joint(chest, "Neck", new Vector3(0f, 0.52f, 0.03f));
            Part(neck, "NeckMesh", S(ChunkyMeshGenerator.Lathe("L_Neck", new[] { new Vector2(0f, 0.42f), new Vector2(0.045f, 0.4f), new Vector2(0.042f, 0.2f), new Vector2(0.06f, 0.02f), new Vector2(0f, -0.02f) }, 10, Vector2.one)), skin, Vector3.zero);
            var head = Joint(neck, "Head", new Vector3(0f, 0.4f, 0f));
            Part(head, "Skull", Ell("L_Skull", new Vector3(0.17f, 0.19f, 0.4f)), skin, new Vector3(0f, 0.02f, 0.1f));
            Part(head, "Brow", Ell("L_Brow", new Vector3(0.18f, 0.06f, 0.12f)), skin, new Vector3(0f, 0.07f, 0.2f), new Vector3(-12f, 0f, 0f));
            Part(head, "MouthInside", Ell("L_Mouth", new Vector3(0.11f, 0.05f, 0.24f)), mouth, new Vector3(0f, -0.05f, 0.14f));
            Part(head, "UpperTeeth", ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Box("L_TeethTop", new Vector3(0.1f, 0.025f, 0.2f), 4f)), teeth, new Vector3(0f, -0.065f, 0.17f));
            var eyeL = Part(head, "Eye_L", Ell("L_Eye", new Vector3(0.035f, 0.022f, 0.03f)), eye, new Vector3(-0.05f, 0.045f, 0.245f), new Vector3(0f, 0f, -20f));
            var eyeR = Part(head, "Eye_R", Ell("L_Eye", new Vector3(0.035f, 0.022f, 0.03f)), eye, new Vector3(0.05f, 0.04f, 0.24f), new Vector3(0f, 0f, 25f));
            eyeL.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            eyeR.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var jaw = Joint(head, "Jaw", new Vector3(0f, -0.05f, 0.0f));
            Part(jaw, "JawMesh", Ell("L_Jaw", new Vector3(0.13f, 0.06f, 0.32f)), skin, new Vector3(0f, -0.035f, 0.13f));
            Part(jaw, "LowerTeeth", ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Box("L_TeethBottom", new Vector3(0.09f, 0.025f, 0.2f), 4f)), teeth, new Vector3(0f, -0.005f, 0.15f));

            Transform Arm(string side, float x, float y, float upper, float fore, out Transform elbow, out Transform hand, out Transform[] fingers)
            {
                var shoulder = Joint(chest, "Shoulder" + side, new Vector3(x, y, 0f));
                Part(shoulder, "ShoulderKnob", Ell("L_ShoulderKnob", new Vector3(0.09f, 0.08f, 0.09f)), skin, Vector3.zero);
                Part(shoulder, "UpperArm", Limb("L_UpperArm" + side, upper, 0.048f, 0.032f), skin, Vector3.zero);
                elbow = Joint(shoulder, "Elbow" + side, new Vector3(0f, -upper, 0f));
                Part(elbow, "Forearm", Limb("L_Forearm" + side, fore, 0.038f, 0.026f), skin, Vector3.zero);
                hand = Joint(elbow, "Hand" + side, new Vector3(0f, -fore, 0f));
                Part(hand, "Palm", Ell("L_Palm", new Vector3(0.07f, 0.11f, 0.035f)), skin, new Vector3(0f, -0.04f, 0f));
                fingers = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    var f = Joint(hand, "Finger" + i, new Vector3((i - 1) * 0.024f, -0.085f, 0f));
                    Part(f, "Digit", Limb("L_Finger" + (i == 1 ? "Long" : ""), 0.22f + (i == 1 ? 0.06f : 0f), 0.012f, 0.008f), skin, Vector3.zero);
                    Part(f, "Claw", Limb("L_Claw", 0.1f, 0.008f, 0.002f), claw, new Vector3(0f, -0.24f - (i == 1 ? 0.06f : 0f), 0f));
                    fingers[i] = f;
                }
                return shoulder;
            }
            // Asimétrico: el brazo izquierdo es más largo y el hombro derecho más alto.
            var shoulderL = Arm("L", -0.19f, 0.45f, 0.64f, 0.68f, out var elbowL, out var handL, out var fingersL);
            var shoulderR = Arm("R", 0.2f, 0.5f, 0.55f, 0.6f, out var elbowR, out var handR, out var fingersR);

            Transform Leg(string side, float x, out Transform knee, out Transform ankle)
            {
                var hip = Joint(hips, "Hip" + side, new Vector3(x, 0f, 0f));
                Part(hip, "Thigh", Limb("L_Thigh", 0.56f, 0.07f, 0.042f), skin, Vector3.zero);
                knee = Joint(hip, "Knee" + side, new Vector3(0f, -0.58f, 0f));
                Part(knee, "Shin", Limb("L_Shin", 0.6f, 0.042f, 0.028f), skin, Vector3.zero);
                ankle = Joint(knee, "Ankle" + side, new Vector3(0f, -0.62f, 0f));
                Part(ankle, "Metatarsal", Limb("L_Metatarsal", 0.3f, 0.032f, 0.024f), skin, Vector3.zero);
                for (int i = 0; i < 3; i++)
                    Part(ankle, "Toe", Limb("L_Toe", 0.15f, 0.016f, 0.004f), claw, new Vector3((i - 1) * 0.025f, -0.31f, 0.01f), new Vector3(-80f, (i - 1) * 18f, 0f));
                return hip;
            }
            var hipL = Leg("L", -0.12f, out var kneeL, out var ankleL);
            var hipR = Leg("R", 0.12f, out var kneeR, out var ankleR);

            if (!lurker.TryGetComponent(out ProjectRidge.AI.LurkerAnimator anim)) anim = lurker.AddComponent<ProjectRidge.AI.LurkerAnimator>();
            var enemy = lurker.GetComponent<ProjectRidge.AI.EnemyController>();
            var aso = new SerializedObject(anim);
            aso.FindProperty("enemy").objectReferenceValue = enemy;
            aso.FindProperty("agent").objectReferenceValue = lurker.GetComponent<UnityEngine.AI.NavMeshAgent>();
            aso.FindProperty("config").objectReferenceValue = enemy.Config;
            var jp = aso.FindProperty("j");
            void Set(string n, Object o) => jp.FindPropertyRelative(n).objectReferenceValue = o;
            void SetArr(string n, Transform[] arr)
            {
                var p = jp.FindPropertyRelative(n);
                p.arraySize = arr.Length;
                for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
            }
            Set("root", root); Set("hips", hips); Set("spine", spine); Set("chest", chest); Set("neck", neck); Set("head", head); Set("jaw", jaw);
            Set("shoulderL", shoulderL); Set("elbowL", elbowL); Set("handL", handL);
            Set("shoulderR", shoulderR); Set("elbowR", elbowR); Set("handR", handR);
            SetArr("fingersL", fingersL); SetArr("fingersR", fingersR);
            Set("hipL", hipL); Set("kneeL", kneeL); Set("ankleL", ankleL);
            Set("hipR", hipR); Set("kneeR", kneeR); Set("ankleR", ankleR);
            aso.FindProperty("thighLength").floatValue = 0.58f;
            aso.FindProperty("shinLength").floatValue = 0.62f;
            aso.FindProperty("footLength").floatValue = 0.32f;
            aso.ApplyModifiedPropertiesWithoutUndo();

            // Ojos para el feedback de estado.
            var fb = lurker.GetComponent<ProjectRidge.AI.EnemyStateFeedback>();
            var fso = new SerializedObject(fb);
            var eyes = fso.FindProperty("eyes");
            eyes.arraySize = 2;
            eyes.GetArrayElementAtIndex(0).objectReferenceValue = eyeL.GetComponent<MeshRenderer>();
            eyes.GetArrayElementAtIndex(1).objectReferenceValue = eyeR.GetComponent<MeshRenderer>();
            fso.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
