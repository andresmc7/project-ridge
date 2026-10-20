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
            new Spot("Medkit", -6f, -14f),
            new Spot("Flare", 7.5f, 9f),
            new Spot("Medkit", 13f, -6f),
        };

        [MenuItem("Project Ridge/Items/Setup Items in Active Scene")]
        public static string Build()
        {
            PolyHavenMaterialBuilder.EnsureFolder(ItemsDir);
            PolyHavenMaterialBuilder.EnsureFolder(MatDir);
            var config = Asset<InteractionConfig>(DataDir + "/InteractionConfig.asset", c => { });
            var medkit = Item("Medkit", "Medkit", "MED", 3);
            var flare = Item("Flare", "Flare", "FLARE", 3);

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

            var matWood = Mat("Item_CrateWood", new Color(0.36f, 0.26f, 0.17f), Color.black);
            int i = 0;
            foreach (var s in Pickups)
            {
                i++;
                PickupObject(root, s.item, Ground(s.pos) + Vector3.up * 0.02f, (i * 73f) % 360f);
            }

            Crate(root, "Crate_Start", Ground(new Vector3(-12f, 0f, -7.5f)), 25f, config, matWood,
                new SearchableContainer.Entry { item = medkit, amount = 1 });
            Crate(root, "Crate_Clearing", Ground(new Vector3(1.5f, 0f, 9.5f)), -40f, config, matWood,
                new SearchableContainer.Entry { item = flare, amount = 1 });

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Items set up: " + Pickups.Length + " pickups, 2 crates in " + scene.path;
        }

        internal static ItemDefinition LoadItem(string name) => AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsDir + "/" + name + ".asset");
        internal static InteractionConfig LoadConfig() => AssetDatabase.LoadAssetAtPath<InteractionConfig>(DataDir + "/InteractionConfig.asset");
        internal static Material CrateWood() => Mat("Item_CrateWood", new Color(0.36f, 0.26f, 0.17f), Color.black);

        /// <summary>Objeto recogible ("Medkit", "Flare", "Stone") en una posición del mundo.</summary>
        internal static GameObject PickupObject(Transform root, string itemName, Vector3 position, float yaw)
        {
            var config = LoadConfig();
            var def = LoadItem(itemName);
            var matMed = Mat("Item_Medkit", new Color(0.75f, 0.72f, 0.66f), new Color(0.35f, 0.05f, 0.04f));
            var matCross = Mat("Item_MedkitCross", new Color(0.55f, 0.06f, 0.05f), new Color(0.4f, 0.02f, 0.02f));
            var matFlare = Mat("Item_Flare", new Color(0.8f, 0.3f, 0.06f), new Color(0.5f, 0.12f, 0.02f));
            {
                var s = new Spot(itemName, position.x, position.z);
                var go = new GameObject("Pickup_" + itemName);
                go.transform.SetParent(root, false);
                go.transform.position = position;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
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
                else if (s.item == "Stone")
                {
                    // Dos piedras de río, una sobre otra: se leen como "algo para lanzar".
                    var rock = StoneMesh();
                    var rm = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_mossy_rock.mat");
                    MeshPart(go.transform, rock, rm, new Vector3(0f, 0.04f, 0f), Vector3.one * 0.9f);
                    MeshPart(go.transform, rock, rm, new Vector3(0.08f, 0.03f, 0.05f), Vector3.one * 0.65f);
                }
                else Debug.LogError("Objeto sin modelo: " + itemName);
                var p = go.AddComponent<Pickup>();
                var pso = new SerializedObject(p);
                pso.FindProperty("config").objectReferenceValue = config;
                pso.FindProperty("item").objectReferenceValue = def;
                pso.ApplyModifiedPropertiesWithoutUndo();
                return go;
            }
        }

        const string PrefabDir = "Assets/_Project/Prefabs";

        [MenuItem("Project Ridge/Items/Setup Combat in Active Scene")]
        public static string BuildCombat()
        {
            PolyHavenMaterialBuilder.EnsureFolder(PrefabDir);
            PolyHavenMaterialBuilder.EnsureFolder(MatDir);
            var combatConfig = Asset<CombatConfig>(DataDir + "/CombatConfig.asset", c => { });
            var flare = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsDir + "/Flare.asset");
            var prefab = BuildFlarePrefab(combatConfig);

            var health = Object.FindFirstObjectByType<PlayerHealth>();
            var player = health.gameObject;
            var input = player.GetComponent<PlayerInputHandler>();
            var so = new SerializedObject(input);
            so.FindProperty("fire").objectReferenceValue = ActionRef("Fire");
            so.ApplyModifiedPropertiesWithoutUndo();

            var muzzle = player.transform.Find("Muzzle");
            if (muzzle == null)
            {
                muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(player.transform, false);
                muzzle.localPosition = new Vector3(0.25f, 1.3f, 0.45f);
            }

            var combat = Ensure<PlayerCombat>(player);
            so = new SerializedObject(combat);
            so.FindProperty("config").objectReferenceValue = combatConfig;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("controller").objectReferenceValue = player.GetComponent<PlayerController>();
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("inventory").objectReferenceValue = player.GetComponent<PlayerInventory>();
            so.FindProperty("interactor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
            so.FindProperty("flareItem").objectReferenceValue = flare;
            so.FindProperty("flarePrefab").objectReferenceValue = prefab;
            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.ApplyModifiedPropertiesWithoutUndo();

            int enemies = 0;
            foreach (var enemy in Object.FindObjectsByType<ProjectRidge.AI.EnemyController>(FindObjectsSortMode.None))
            {
                var eh = Ensure<ProjectRidge.AI.EnemyHealth>(enemy.gameObject);
                var eso = new SerializedObject(eh);
                eso.FindProperty("config").objectReferenceValue = enemy.Config;
                eso.FindProperty("enemy").objectReferenceValue = enemy;
                eso.ApplyModifiedPropertiesWithoutUndo();
                enemies++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Combat set up on player + " + enemies + " enemies";
        }

        /// <summary>
        /// Bengala de mano (reemplaza a la pistola): prefab con mecha, luz roja, chispas, humo rojo y sonido, y la
        /// herramienta en el papá (Fire = dejar una bengala en el suelo). Desactiva la pistola en CombatConfig.
        /// </summary>
        public static string BuildHandFlare()
        {
            PolyHavenMaterialBuilder.EnsureFolder(PrefabDir);
            PolyHavenMaterialBuilder.EnsureFolder(MatDir);
            var config = AssetDatabase.LoadAssetAtPath<CombatConfig>(DataDir + "/CombatConfig.asset");
            config.flareGun = false;
            EditorUtility.SetDirty(config);
            var bank = AssetDatabase.LoadAssetAtPath<ProjectRidge.Core.AudioBank>(DataDir + "/AudioBank.asset");

            var root = new GameObject("HandFlare");
            var body = Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.045f, 0.13f, 0.045f),
                Mat("Item_Flare", new Color(0.8f, 0.3f, 0.06f), new Color(0.5f, 0.12f, 0.02f)));
            body.localRotation = Quaternion.Euler(0f, 0f, 72f); // tumbada, con la punta algo levantada
            var tip = Part(root.transform, PrimitiveType.Sphere, new Vector3(-0.12f, 0.09f, 0f), Vector3.one * 0.055f,
                Mat("Item_FlareTip", new Color(1f, 0.35f, 0.25f), new Color(0.3f, 0.05f, 0.02f)));

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None; // solo la luna y la linterna proyectan sombra
            light.enabled = false;

            var fuse = SparkSystem(root.transform, "FuseSparks", new Vector3(-0.12f, 0.09f, 0f), 14f, 0.6f, 0.12f, 0.35f, 20);
            var sparks = SparkSystem(root.transform, "Sparks", new Vector3(-0.12f, 0.09f, 0f), 55f, 2f, 0.35f, 0.8f, 90);
            var smoke = SmokeSystem(root.transform);

            AudioSource Source(string n, AudioClip clip, float volume, float pitch)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root.transform, false);
                var a = go.AddComponent<AudioSource>();
                a.clip = clip; a.loop = true; a.playOnAwake = false; a.volume = volume; a.pitch = pitch;
                a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 2f; a.maxDistance = 22f;
                return a;
            }
            var fuseAudio = Source("FuseAudio", bank != null ? bank.flareBurnLoop : null, 0.35f, 1.6f);
            var burnAudio = Source("BurnAudio", bank != null ? bank.flareBurnLoop : null, 0.9f, 1f);

            var hf = root.AddComponent<HandFlare>();
            var so = new SerializedObject(hf);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("glow").objectReferenceValue = light;
            so.FindProperty("fuseSparks").objectReferenceValue = fuse;
            so.FindProperty("sparks").objectReferenceValue = sparks;
            so.FindProperty("smoke").objectReferenceValue = smoke;
            so.FindProperty("fuseAudio").objectReferenceValue = fuseAudio;
            so.FindProperty("burnAudio").objectReferenceValue = burnAudio;
            so.FindProperty("tip").objectReferenceValue = tip.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/HandFlare.prefab");
            Object.DestroyImmediate(root);

            var health = Object.FindFirstObjectByType<PlayerHealth>();
            var player = health.gameObject;
            var tool = Ensure<PlayerFlareTool>(player);
            so = new SerializedObject(tool);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("input").objectReferenceValue = player.GetComponent<PlayerInputHandler>();
            so.FindProperty("controller").objectReferenceValue = player.GetComponent<PlayerController>();
            so.FindProperty("combat").objectReferenceValue = player.GetComponent<PlayerCombat>();
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("inventory").objectReferenceValue = player.GetComponent<PlayerInventory>();
            so.FindProperty("interactor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
            so.FindProperty("animator").objectReferenceValue = player.GetComponentInChildren<FatherAnimator>();
            so.FindProperty("flareItem").objectReferenceValue = LoadItem("Flare");
            so.FindProperty("prefab").objectReferenceValue = saved.GetComponent<HandFlare>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return "hand flare ok";
        }

        static ParticleSystem SparkSystem(Transform parent, string name, Vector3 pos, float rate, float speed, float minLife, float maxLife, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.45f), new Color(1f, 0.3f, 0.15f));
            main.gravityModifier = 0.7f;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.01f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = SparkMaterial();
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>Humo rojo que sube despacio, se abre y se deriva con el viento (lo ilumina la propia bengala).</summary>
        static ParticleSystem SmokeSystem(Transform parent)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(-0.1f, 0.2f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 70;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 11f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 4.2f));
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.3f, 0.22f), 0f), new GradientColorKey(new Color(0.75f, 0.2f, 0.2f), 0.35f), new GradientColorKey(new Color(0.32f, 0.26f, 0.3f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.12f), new GradientAlphaKey(0.3f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            string path = MatDir + "/FX_FlareSmoke.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Hollow/FX/Tex_SoftDot.png"));
            m.SetFloat("_Surface", 1f); // transparente
            m.SetFloat("_Blend", 0f);   // alpha
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(1.6f, 1.1f, 1.1f, 1f));
            EditorUtility.SetDirty(m);
            psr.sharedMaterial = m;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.sortingFudge = -2f;
            return ps;
        }

        static FlareProjectile BuildFlarePrefab(CombatConfig config)
        {
            var root = new GameObject("FlareProjectile");
            var stick = Part(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.05f, 0.09f, 0.05f),
                Mat("Item_FlareBurning", new Color(0.9f, 0.25f, 0.1f), new Color(4f, 0.9f, 0.4f)));
            stick.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None; // solo la luna y la linterna proyectan sombra
            light.color = config.lightColor;
            light.range = config.lightRange;
            light.intensity = config.lightIntensity;
            light.enabled = false;

            var fxGo = new GameObject("Sparks");
            fxGo.transform.SetParent(root.transform, false);
            var ps = fxGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f), new Color(1f, 0.3f, 0.1f));
            main.gravityModifier = 0.6f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 45f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var psr = fxGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = SparkMaterial();
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var fp = root.AddComponent<FlareProjectile>();
            var so = new SerializedObject(fp);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("glow").objectReferenceValue = light;
            so.FindProperty("sparks").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/FlareProjectile.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<FlareProjectile>();
        }

        static Material SparkMaterial()
        {
            string path = MatDir + "/FX_Spark.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);   // transparente
            m.SetFloat("_Blend", 2f);     // aditivo
            m.SetColor("_BaseColor", new Color(1f, 0.6f, 0.3f, 1f));
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        internal static GameObject Crate(Transform root, string name, Vector3 pos, float yaw, InteractionConfig config, Material wood, params SearchableContainer.Entry[] contents)
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
            return go;
        }

        internal static Transform Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
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

        internal static ItemDefinition EnsureItem(string file, string displayName, string label, int maxCarry) => Item(file, displayName, label, maxCarry);

        static Mesh StoneMesh() => ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Rock("Stone_Throwable", 42, new Vector3(0.12f, 0.08f, 0.1f), 0.1f, 0.25f));

        static void MeshPart(Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject("Stone");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Random.rotation;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Prefab de la piedra lanzada.</summary>
        internal static StoneProjectile StonePrefab()
        {
            PolyHavenMaterialBuilder.EnsureFolder(PrefabDir);
            var root = new GameObject("StoneProjectile");
            MeshPart(root.transform, StoneMesh(), AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_mossy_rock.mat"), Vector3.zero, Vector3.one);
            root.AddComponent<StoneProjectile>();
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/StoneProjectile.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<StoneProjectile>();
        }

        internal static InputActionReference ActionRef(string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Data/Input/ProjectRidgeInput.inputactions"))
                if (o is InputActionReference r && r.action != null && r.action.name == name) return r;
            return null;
        }
    }
}
