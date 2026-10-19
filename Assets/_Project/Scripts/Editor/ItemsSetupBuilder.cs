using ProjectRidge.Items;
using ProjectRidge.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Recursos del PoC en la escena activa: crea los ItemDefinition e InteractionConfig, conecta inventario,
    /// interacción y curación al jugador, y coloca botiquines, bengalas, chatarra y cajas.
    /// </summary>
    public static class ItemsSetupBuilder
    {
        const string DataDir = "Assets/_Project/Data";
        const string ItemsDir = DataDir + "/Items";
        const string MatDir = "Assets/_Project/Art/Materials/Items";

        struct Spot
        {
            public string item;
            public Vector3 pos;
            public Spot(string item, float x, float z) { this.item = item; pos = new Vector3(x, 0f, z); }
        }

        // Repartidos para premiar la exploración: el claro del Lurker tiene lo mejor.
        static readonly Spot[] Pickups =
        {
            new Spot("Scrap", -10.5f, -9.5f),
            new Spot("Medkit", -6f, -14f),
            new Spot("Scrap", -2f, -4f),
            new Spot("Flare", 7.5f, 9f),
            new Spot("Medkit", 13f, -6f),
            new Spot("Scrap", 11f, 14f),
        };

        [MenuItem("Project Ridge/Items/Setup Items in Active Scene")]
        public static string Build()
        {
            PolyHavenMaterialBuilder.EnsureFolder(ItemsDir);
            PolyHavenMaterialBuilder.EnsureFolder(MatDir);
            var config = Asset<InteractionConfig>(DataDir + "/InteractionConfig.asset", c => { });
            var medkit = Item("Medkit", "Medkit", "MED", 3);
            var flare = Item("Flare", "Flare", "FLARE", 3);
            var scrap = Item("Scrap", "Scrap", "SCRAP", 9);

            // --- Jugador
            var health = Object.FindFirstObjectByType<PlayerHealth>();
            var player = health.gameObject;
            var input = player.GetComponent<PlayerInputHandler>();
            var inventory = Ensure<PlayerInventory>(player);
            var interactor = Ensure<PlayerInteractor>(player);
            var healing = Ensure<PlayerHealing>(player);

            var so = new SerializedObject(input);
            so.FindProperty("heal").objectReferenceValue = ActionRef("Heal");
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(interactor);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(healing);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("interactor").objectReferenceValue = interactor;
            so.FindProperty("medkit").objectReferenceValue = medkit;
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- Objetos en el mapa
            var old = GameObject.Find("Items");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Items").transform;

            var matMed = Mat("Item_Medkit", new Color(0.75f, 0.72f, 0.66f), new Color(0.35f, 0.05f, 0.04f));
            var matCross = Mat("Item_MedkitCross", new Color(0.55f, 0.06f, 0.05f), new Color(0.4f, 0.02f, 0.02f));
            var matFlare = Mat("Item_Flare", new Color(0.8f, 0.3f, 0.06f), new Color(0.5f, 0.12f, 0.02f));
            var matScrap = Mat("Item_Scrap", new Color(0.42f, 0.4f, 0.37f), new Color(0.08f, 0.08f, 0.07f));
            var matWood = Mat("Item_CrateWood", new Color(0.36f, 0.26f, 0.17f), Color.black);

            int i = 0;
            foreach (var s in Pickups)
            {
                var def = s.item == "Medkit" ? medkit : s.item == "Flare" ? flare : scrap;
                var go = new GameObject("Pickup_" + s.item + "_" + i++);
                go.transform.SetParent(root, false);
                go.transform.position = Ground(s.pos) + Vector3.up * 0.02f;
                go.transform.rotation = Quaternion.Euler(0f, (i * 73f) % 360f, 0f);
                if (s.item == "Medkit")
                {
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.09f, 0f), new Vector3(0.36f, 0.18f, 0.26f), matMed);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.185f, 0f), new Vector3(0.2f, 0.01f, 0.06f), matCross);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.185f, 0f), new Vector3(0.06f, 0.01f, 0.2f), matCross);
                }
                else if (s.item == "Flare")
                {
                    var c = Part(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.07f, 0.16f, 0.07f), matFlare);
                    c.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var c2 = Part(go.transform, PrimitiveType.Cylinder, new Vector3(0.05f, 0.04f, 0.09f), new Vector3(0.07f, 0.16f, 0.07f), matFlare);
                    c2.localRotation = Quaternion.Euler(0f, 30f, 90f);
                }
                else
                {
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.34f, 0.04f, 0.16f), matScrap).localRotation = Quaternion.Euler(0f, 20f, 8f);
                    Part(go.transform, PrimitiveType.Cylinder, new Vector3(0.08f, 0.05f, 0.1f), new Vector3(0.05f, 0.18f, 0.05f), matScrap).localRotation = Quaternion.Euler(80f, 0f, 0f);
                }
                var p = go.AddComponent<Pickup>();
                var pso = new SerializedObject(p);
                pso.FindProperty("config").objectReferenceValue = config;
                pso.FindProperty("item").objectReferenceValue = def;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }

            Crate(root, "Crate_Start", Ground(new Vector3(-12f, 0f, -7.5f)), 25f, config, matWood,
                new SearchableContainer.Entry { item = medkit, amount = 1 }, new SearchableContainer.Entry { item = scrap, amount = 2 });
            Crate(root, "Crate_Clearing", Ground(new Vector3(1.5f, 0f, 9.5f)), -40f, config, matWood,
                new SearchableContainer.Entry { item = flare, amount = 1 }, new SearchableContainer.Entry { item = scrap, amount = 1 });

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Items set up: " + Pickups.Length + " pickups, 2 crates in " + scene.path;
        }

        static void Crate(Transform root, string name, Vector3 pos, float yaw, InteractionConfig config, Material wood, params SearchableContainer.Entry[] contents)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var body = Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.6f), wood);
            body.gameObject.AddComponent<BoxCollider>();
            var hinge = new GameObject("LidHinge").transform;
            hinge.SetParent(go.transform, false);
            hinge.localPosition = new Vector3(0f, 0.5f, 0.3f);
            Part(hinge, PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.3f), new Vector3(0.94f, 0.06f, 0.64f), wood);

            var c = go.AddComponent<SearchableContainer>();
            var so = new SerializedObject(c);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("displayName").stringValue = "Crate";
            so.FindProperty("lid").objectReferenceValue = hinge;
            var arr = so.FindProperty("contents");
            arr.arraySize = contents.Length;
            for (int i = 0; i < contents.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = contents[i].item;
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = contents[i].amount;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }

        static Material Mat(string name, Color baseColor, Color emission)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", 0.25f);
            // Un brillo tenue para que se lean en la oscuridad sin ser luces dinámicas.
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static ItemDefinition Item(string file, string displayName, string label, int maxCarry) =>
            Asset<ItemDefinition>(ItemsDir + "/" + file + ".asset", d =>
            {
                d.displayName = displayName;
                d.shortLabel = label;
                d.maxCarry = maxCarry;
            });

        static T Asset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

        static InputActionReference ActionRef(string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Data/Input/ProjectRidgeInput.inputactions"))
                if (o is InputActionReference r && r.action != null && r.action.name == name) return r;
            return null;
        }
    }
}
