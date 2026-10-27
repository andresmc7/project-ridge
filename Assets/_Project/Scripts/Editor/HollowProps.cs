using System.Collections.Generic;
using ProjectRidge.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.EditorTools
{
    /// <summary>Hitos construidos de The Hollow: refugio sin techo, puente, tronco caído, arroyo, campamento, camioneta y camino.</summary>
    public static partial class HollowBuilder
    {
        static readonly Dictionary<string, Mesh> boxCache = new Dictionary<string, Mesh>();

        static Mesh BoxMesh(Vector3 size, float uvPerMeter = 0.6f)
        {
            string key = "Box_" + Mathf.RoundToInt(size.x * 100) + "x" + Mathf.RoundToInt(size.y * 100) + "x" + Mathf.RoundToInt(size.z * 100) + "_" + Mathf.RoundToInt(uvPerMeter * 100);
            if (boxCache.TryGetValue(key, out var m) && m != null) return m;
            m = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Box(key, size, uvPerMeter));
            boxCache[key] = m;
            return m;
        }

        static Mesh LogMesh(float length, float radius)
        {
            string key = "Log_" + Mathf.RoundToInt(length * 100) + "_" + Mathf.RoundToInt(radius * 100);
            if (boxCache.TryGetValue(key, out var m) && m != null) return m;
            m = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Log(key, length, radius));
            boxCache[key] = m;
            return m;
        }

        /// <summary>Pieza con malla y material. collider: caja ajustada a la malla.</summary>
        static GameObject Piece(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 localPos, Vector3 localEuler, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.receiveGI = ReceiveGI.LightProbes;
            if (collider) go.AddComponent<BoxCollider>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        static GameObject Board(Transform parent, string name, Vector3 size, Material mat, Vector3 pos, Vector3 euler, bool collider = true) =>
            Piece(parent, name, BoxMesh(size), new[] { mat }, pos, euler, collider);

        static GameObject LogPiece(Transform parent, string name, float length, float radius, Mats m, Vector3 pos, Vector3 euler, bool collider = true)
        {
            var go = Piece(parent, name, LogMesh(length, radius), new[] { m.bark, m.cutWood }, pos, euler, false);
            if (collider)
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.direction = 2;
                c.radius = radius;
                c.height = length;
            }
            return go;
        }

        static Transform Group(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static void InvisibleWall(Transform parent, string name, Vector3 localPos, Vector3 size, Vector3 euler)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<BoxCollider>().size = size;
        }

        static void Sign(Transform parent, string text, Vector3 localPos, float yaw, Mats m, float size = 0.22f)
        {
            var post = Group(parent, "Sign", parent.TransformPoint(localPos), parent.eulerAngles.y + yaw);
            LogPiece(post, "Post", 1.6f, 0.07f, m, new Vector3(0f, 0.8f, 0f), new Vector3(-90f, 0f, 0f));
            Board(post, "Board", new Vector3(1.3f, 0.45f, 0.06f), m.planks, new Vector3(0f, 1.35f, -0.08f), Vector3.zero, false);
            var label = new GameObject("Text");
            label.transform.SetParent(post, false);
            label.transform.localPosition = new Vector3(0f, 1.35f, -0.115f);
            var tm = label.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = size * 0.1f;
            tm.fontSize = 60;
            tm.color = new Color(0.85f, 0.8f, 0.65f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        }

        /// <summary>El objeto se vuelve semitransparente cuando tapa al papá (paredes del refugio, camioneta).</summary>
        /// <summary>Polvo suspendido dentro del haz de un reflector (POLISH_PASS 3.10): motas lentas que lo hacen físico.</summary>
        static ParticleSystem BeamDust(Transform parent, float radius)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 3f;
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 13f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 1.3f, 3f, radius * 1.3f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FxMaterial("FX_BeamDust", "Universal Render Pipeline/Particles/Unlit", SoftDot(), Color.white, true);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void MakeFadeable(GameObject go)
        {
            var rs = go.GetComponentsInChildren<MeshRenderer>();
            if (rs.Length == 0) return;
            var mats = new System.Collections.Generic.List<Material>();
            foreach (var r in rs) foreach (var m in r.sharedMaterials) if (m != null && !mats.Contains(m)) mats.Add(m);
            LookDevForestCornerBuilder.AddFadeable(go, rs, mats.ToArray());
        }

        // ------------------------------------------------------------------ Hitos

        static int BuildLandmarks(Transform root, Terrain t, Mats m, List<Vector3> blockers)
        {
            var parent = new GameObject("Landmarks").transform;
            parent.SetParent(root, false);
            int n = 0;
            n += BuildCabin(parent, t, m, blockers);
            n += BuildBridge(parent, t, m);
            n += BuildStream(parent, t, m);
            n += BuildCamp(parent, t, m, blockers);
            n += BuildRoadAndTruck(parent, t, m, blockers);
            n += BuildTrailDressing(parent, t, m, blockers);
            return n;
        }

        // ---------- Refugio del guardabosques: paredes rotas, sin techo.

        struct Panel { public float start, width, height; public Panel(float s, float w, float h) { start = s; width = w; height = h; } }

        static int BuildCabin(Transform parent, Terrain t, Mats m, List<Vector3> blockers)
        {
            var c = Group(parent, "RangerShelter", OnGround(t, CabinCenter.x, CabinCenter.z, 0.02f), CabinYaw);
            const float W = 6.2f, D = 5.2f, T = 0.2f;
            Board(c, "Floor", new Vector3(W, 0.16f, D), m.planks, new Vector3(0f, 0.06f, 0f), Vector3.zero);

            // Paredes por paneles de alturas distintas (borde superior roto). Huecos: puerta al este, ventana al sur.
            void Wall(string name, Vector3 origin, float yaw, float length, Panel[] panels)
            {
                var w = Group(c, name, c.TransformPoint(origin), CabinYaw + yaw);
                foreach (var p in panels)
                    Board(w, "Panel", new Vector3(p.width, p.height, T), m.planks, new Vector3(p.start + p.width * 0.5f - length * 0.5f, p.height * 0.5f + 0.1f, 0f), Vector3.zero);
            }
            Wall("Wall_N", new Vector3(0f, 0f, D * 0.5f), 0f, W, new[] { new Panel(0f, 1.4f, 2.3f), new Panel(1.4f, 1.1f, 1.7f), new Panel(2.5f, 0.9f, 0.9f), new Panel(4.6f, 1.6f, 2.1f) });
            // Pared sur en ruinas y baja (corte de diorama): la cámara mira al norte y ve el interior entero.
            Wall("Wall_S", new Vector3(0f, 0f, -D * 0.5f), 180f, W, new[] { new Panel(0f, 1.7f, 0.7f), new Panel(1.7f, 1.2f, 0.45f), new Panel(2.9f, 1.3f, 0.8f), new Panel(4.2f, 2.0f, 0.6f) });
            Wall("Wall_W", new Vector3(-W * 0.5f, 0f, 0f), -90f, D, new[] { new Panel(0f, 1.8f, 2.5f), new Panel(1.8f, 1.5f, 1.4f), new Panel(3.3f, 1.9f, 2.0f) });
            Wall("Wall_E", new Vector3(W * 0.5f, 0f, 0f), 90f, D, new[] { new Panel(0f, 1.7f, 2.2f), new Panel(3.0f, 2.2f, 1.6f) }); // puerta de 1.3 m
            // Las paredes se desvanecen cuando tapan al papá (dentro del refugio o detrás).
            foreach (var wn in new[] { "Wall_N", "Wall_S", "Wall_W", "Wall_E" }) MakeFadeable(c.Find(wn).gameObject);
            // Dintel sobre la ventana sur y un tablón suelto colgando.
            Board(c, "HangingBoard", new Vector3(1.4f, 0.25f, 0.08f), m.planks, new Vector3(0.3f, 0.2f, -D * 0.5f - 0.35f), new Vector3(0f, 10f, 80f), false);

            // Postes de las esquinas.
            foreach (var p in new[] { new Vector3(-W, 0, -D), new Vector3(W, 0, -D), new Vector3(-W, 0, D), new Vector3(W, 0, D) })
            {
                float h = p.z < 0f ? 0.9f : 2.7f;
                LogPiece(c, "CornerPost", h, 0.14f, m, new Vector3(p.x * 0.5f, h * 0.5f, p.z * 0.5f), new Vector3(-90f, 0f, 0f));
            }

            // Techo caído: una viga aún cruza, otra apoyada en diagonal, tablones en el suelo.
            LogPiece(c, "Beam_Standing", W + 0.6f, 0.13f, m, new Vector3(0f, 2.25f, 0.6f), new Vector3(0f, 90f, 0f), false);
            LogPiece(c, "Beam_Fallen", 5.2f, 0.13f, m, new Vector3(-0.6f, 1.05f, -0.9f), new Vector3(24f, 70f, 0f));
            Board(c, "RoofBoard_1", new Vector3(2.2f, 0.06f, 0.5f), m.planks, new Vector3(1.4f, 0.17f, 1.4f), new Vector3(0f, 25f, 3f));
            Board(c, "RoofBoard_2", new Vector3(1.8f, 0.06f, 0.45f), m.planks, new Vector3(-1.9f, 0.25f, 0.8f), new Vector3(8f, -40f, 12f), false);
            Board(c, "RoofBoard_3", new Vector3(2.4f, 0.06f, 0.5f), m.planks, new Vector3(4.1f, 0.12f, -1.8f), new Vector3(0f, 70f, 6f), false);
            Board(c, "RoofBoard_4", new Vector3(1.6f, 0.06f, 0.45f), m.planks, new Vector3(-3.9f, 0.1f, 2.6f), new Vector3(0f, -15f, 4f), false);

            // Interior: catre, mesa con el farol y la foto, silla volcada.
            Board(c, "Cot", new Vector3(0.9f, 0.4f, 2.0f), m.planks, new Vector3(-2.3f, 0.3f, 1.2f), Vector3.zero);
            Board(c, "Mattress", new Vector3(0.8f, 0.12f, 1.8f), m.fabric, new Vector3(-2.3f, 0.56f, 1.2f), new Vector3(0f, 0f, 3f), false);
            var table = Group(c, "Table", c.TransformPoint(new Vector3(1.2f, 0f, 1.4f)), CabinYaw + 12f);
            Board(table, "Top", new Vector3(1.3f, 0.08f, 0.75f), m.planks, new Vector3(0f, 0.78f, 0f), Vector3.zero);
            foreach (var l in new[] { new Vector2(-0.55f, -0.3f), new Vector2(0.55f, -0.3f), new Vector2(-0.55f, 0.3f), new Vector2(0.55f, 0.3f) })
                Board(table, "Leg", new Vector3(0.08f, 0.74f, 0.08f), m.planks, new Vector3(l.x, 0.37f, l.y), Vector3.zero, false);
            Board(c, "Chair_Seat", new Vector3(0.45f, 0.06f, 0.45f), m.planks, new Vector3(0.3f, 0.22f, 0.2f), new Vector3(78f, 30f, 0f), false);
            Board(c, "Chair_Back", new Vector3(0.45f, 0.5f, 0.05f), m.planks, new Vector3(0.45f, 0.12f, -0.1f), new Vector3(90f, 30f, 0f), false);

            // Farol del refugio: la única luz local (CLAUDE.md), cálida y sin sombras.
            var lantern = Group(table, "Lantern", table.TransformPoint(new Vector3(-0.35f, 0.82f, 0.05f)), 0f);
            Board(lantern, "Glass", new Vector3(0.14f, 0.2f, 0.14f), m.glowLantern, new Vector3(0f, 0.1f, 0f), Vector3.zero, false);
            Board(lantern, "Cap", new Vector3(0.17f, 0.04f, 0.17f), m.darkMetal, new Vector3(0f, 0.22f, 0f), Vector3.zero, false);
            var lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(lantern, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.intensity = 2.2f;
            light.range = 7.5f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Mixed;

            // Exterior: leña apilada, valla rota y cartel.
            var wood = Group(c, "Woodpile", c.TransformPoint(new Vector3(-W * 0.5f - 0.8f, 0f, -1.2f)), CabinYaw + 90f);
            for (int i = 0; i < 6; i++)
                LogPiece(wood, "Split", 1.1f, 0.13f, m, new Vector3((i % 3) * 0.27f - 0.27f, 0.13f + (i / 3) * 0.24f, 0f), new Vector3(0f, 0f, i * 37f), i == 0);
            var fence = Group(c, "BrokenFence", c.TransformPoint(new Vector3(1.5f, 0f, -D * 0.5f - 2.8f)), CabinYaw);
            for (int i = 0; i < 4; i++)
            {
                LogPiece(fence, "Post", 1.2f, 0.07f, m, new Vector3(i * 1.6f - 2.4f, 0.55f, 0f), new Vector3(-90f + (i == 2 ? 25f : 0f), 0f, 0f), false);
                if (i < 3 && i != 1) Board(fence, "Rail", new Vector3(1.6f, 0.1f, 0.05f), m.planks, new Vector3(i * 1.6f - 1.6f, 0.8f, 0f), new Vector3(0f, 0f, i == 2 ? -14f : 0f), false);
            }
            Sign(c, "RANGER\nSTATION 7", new Vector3(W * 0.5f + 1.6f, 0f, -1.6f), -70f, m);

            blockers.Add(CabinCenter);
            return 1;
        }

        // ---------- Puente de madera sobre el arroyo (ruta corta, expuesta al OVNI del otro lado).

        static int BuildBridge(Transform parent, Terrain t, Mats m)
        {
            float cx = RavineX(BridgeZ);
            var b = Group(parent, "WoodenBridge", new Vector3(cx, 0f, BridgeZ), 0f);
            const float L = 10f, Wd = 1.9f, deckY = 0.22f;
            // Tablones transversales, con dos que faltan y uno roto.
            for (int i = 0; i < 31; i++)
            {
                if (i == 12 || i == 19) continue;
                float x = -L * 0.5f + 0.16f + i * 0.322f;
                var e = i == 15 ? new Vector3(0f, 6f, 4f) : new Vector3(0f, (i * 37) % 5 - 2f, 0f);
                Board(b, "Plank", new Vector3(0.28f, 0.07f, Wd), m.planks, new Vector3(x, deckY, 0f), e, false);
            }
            // Superficie caminable (invisible) y barandas.
            InvisibleWall(b, "DeckWalk", new Vector3(0f, deckY - 0.08f, 0f), new Vector3(L + 0.6f, 0.16f, Wd), Vector3.zero);
            InvisibleWall(b, "RailBlockN", new Vector3(0f, 0.9f, Wd * 0.5f + 0.05f), new Vector3(L, 1.6f, 0.15f), Vector3.zero);
            InvisibleWall(b, "RailBlockS", new Vector3(0f, 0.9f, -Wd * 0.5f - 0.05f), new Vector3(L, 1.6f, 0.15f), Vector3.zero);
            foreach (float side in new[] { -1f, 1f })
            {
                LogPiece(b, "Stringer", L + 0.4f, 0.12f, m, new Vector3(0f, deckY - 0.17f, side * (Wd * 0.5f - 0.15f)), new Vector3(0f, 90f, 0f), false);
                for (int i = 0; i < 5; i++)
                    LogPiece(b, "RailPost", 1.15f, 0.07f, m, new Vector3(-L * 0.5f + 0.2f + i * 2.4f, deckY + 0.55f, side * (Wd * 0.5f + 0.02f)), new Vector3(-90f, 0f, 0f), false);
                bool broken = side > 0f;
                LogPiece(b, "Rail", broken ? 5.2f : L, 0.06f, m, new Vector3(broken ? -2.4f : 0f, deckY + 1.0f, side * (Wd * 0.5f + 0.02f)), new Vector3(0f, 90f, 0f), false);
                if (broken) LogPiece(b, "RailBroken", 3.4f, 0.06f, m, new Vector3(2.9f, deckY + 0.6f, side * (Wd * 0.5f + 0.15f)), new Vector3(-14f, 90f, 0f), false);
            }
            // Pilotes que bajan al agua.
            foreach (float x in new[] { -1.8f, 1.8f })
                foreach (float z in new[] { -0.75f, 0.75f })
                    LogPiece(b, "Pile", 3f, 0.13f, m, new Vector3(x, -1.3f, z), new Vector3(-90f, 0f, 0f), false);
            return 1;
        }

        // ---------- Tronco caído (ruta larga y cubierta del norte).

        static int BuildLogCrossing(Transform parent, Terrain t, Mats m)
        {
            float cx = RavineX(LogZ);
            var g = Group(parent, "FallenTreeCrossing", new Vector3(cx, 0f, LogZ), 0f);
            LogPiece(g, "Trunk", 13f, 0.5f, m, new Vector3(0.6f, -0.12f, 0f), new Vector3(0f, 90f, 2f), false);
            InvisibleWall(g, "LogWalk", new Vector3(0f, 0.2f, 0f), new Vector3(10.5f, 0.16f, 0.95f), Vector3.zero);
            InvisibleWall(g, "LogSideN", new Vector3(0f, 1.1f, 0.6f), new Vector3(7f, 1.8f, 0.15f), Vector3.zero);
            InvisibleWall(g, "LogSideS", new Vector3(0f, 1.1f, -0.6f), new Vector3(7f, 1.8f, 0.15f), Vector3.zero);
            // Raíces arrancadas en un extremo y ramas rotas.
            var rootRock = RockPrefab(1, false, m);
            // Raíz arrancada a un costado del extremo, sin colisión: no debe desviar al jugador del tronco.
            var rootGo = Spawn(rootRock, g, g.TransformPoint(new Vector3(-6.8f, -0.2f, 1.6f)), 20f, 1.1f);
            Object.DestroyImmediate(rootGo.GetComponent<Collider>());
            LogPiece(g, "Branch1", 2.2f, 0.12f, m, new Vector3(2.4f, 0.55f, 0.45f), new Vector3(-30f, 40f, 0f), false);
            LogPiece(g, "Branch2", 1.8f, 0.1f, m, new Vector3(-1.5f, 0.6f, -0.5f), new Vector3(-35f, -130f, 0f), false);
            LogPiece(g, "Branch3", 1.4f, 0.08f, m, new Vector3(4.2f, 0.3f, -0.55f), new Vector3(10f, 160f, 0f), false);
            return 1;
        }

        // ---------- Agua en el fondo del barranco y piedras.

        static int BuildStream(Transform parent, Terrain t, Mats m)
        {
            var g = new GameObject("Stream").transform;
            g.SetParent(parent, false);
            for (float z = -30f; z < 30f; z += 3f)
            {
                float zc = z + 1.5f;
                float x = RavineX(zc);
                float slope = Mathf.Atan(1.5f * 0.07f * Mathf.Cos(zc * 0.07f)) * Mathf.Rad2Deg;
                var w = Board(g, "Water", new Vector3(4.6f, 0.05f, 3.3f), m.water, new Vector3(x, -2.35f, zc), new Vector3(0f, -slope, 0f), false);
                w.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // Puntos de sonido del agua a lo largo del cauce (el AudioDirector les pone el bucle del arroyo).
            var creekAudio = new GameObject("CreekAudio").transform;
            creekAudio.SetParent(g, false);
            for (float z = -18f; z <= 18f; z += 6f)
            {
                var a = new GameObject("Water").transform;
                a.SetParent(creekAudio, false);
                a.position = new Vector3(RavineX(z), -1.6f, z);
            }
            var rng = new System.Random(77);
            // Orillas: bloques redondeados semienterrados a lo largo del borde (lectura chunky del barranco).
            for (float z = -40f; z < 40f; z += 2.6f)
            {
                if (Mathf.Abs(z - BridgeZ) < 3f || Mathf.Abs(z - LogZ) < 2.5f) continue;
                foreach (float side in new[] { -1f, 1f })
                {
                    if (rng.NextDouble() < 0.25) continue;
                    float zz = z + (float)rng.NextDouble() * 1.4f;
                    float x = RavineX(zz) + side * (2.6f + (float)rng.NextDouble() * 0.6f);
                    var r = Spawn(RockPrefab(rng.Next(4), true, m), g, new Vector3(x, -1.5f - (float)rng.NextDouble() * 0.5f, zz), (float)rng.NextDouble() * 360f, 0.55f + (float)rng.NextDouble() * 0.3f);
                    Object.DestroyImmediate(r.GetComponent<Collider>());
                }
            }
            // Cara alta de las orillas: peñascos grandes incrustados en el talud (antes se leía como una pared lisa de
            // terreno estirado; en target_look las orillas son roca). Quedan bajo el nivel del piso: no tapan el paso.
            var bankRng = new System.Random(78);
            float B(float a, float b) => a + (float)bankRng.NextDouble() * (b - a);
            for (float z = -40f; z < 40f; z += 1.7f)
            {
                if (Mathf.Abs(z - BridgeZ) < 1.6f) continue; // libre solo el ancho del puente (pilotes); a los lados, roca hasta el borde
                foreach (float side in new[] { -1f, 1f })
                {
                    if (bankRng.NextDouble() < 0.15) continue;
                    float zz = z + B(0f, 0.8f);
                    float x = RavineX(zz) + side * B(2.45f, 2.85f); // al pie del talud: la roca se levanta delante de la tierra
                    // Del agua (−2.35) hasta 25 cm bajo el piso: nunca asoma sobre la orilla (regla de oclusión de la cámara).
                    int v = bankRng.Next(4);
                    float h = B(1.6f, 2.3f), s = h / RockSize(v, true).y;
                    var r = Spawn(RockPrefab(v, true, m), g, new Vector3(x, -0.25f - h * 0.5f, zz), B(0f, 360f), s);
                    Object.DestroyImmediate(r.GetComponent<Collider>());
                }
            }
            for (int i = 0; i < 26; i++)
            {
                float z = -21f + i * 1.7f + (float)rng.NextDouble() * 1.2f;
                float x = RavineX(z) + ((float)rng.NextDouble() - 0.5f) * 3f;
                var r = Spawn(RockPrefab(rng.Next(4), false, m), g, new Vector3(x, -2.6f, z), (float)rng.NextDouble() * 360f, 0.5f + (float)rng.NextDouble() * 0.5f);
                Object.DestroyImmediate(r.GetComponent<Collider>());
            }
            return 1;
        }

        // ---------- Campamento abandonado (salida interrumpida).

        static int BuildCamp(Transform parent, Terrain t, Mats m, List<Vector3> blockers)
        {
            var c = Group(parent, "AbandonedCamp", OnGround(t, Camp.x, Camp.z), -20f);
            // Carpa: un lado en pie, el otro caído.
            var tent = Group(c, "Tent", c.TransformPoint(new Vector3(-1.5f, 0f, 1.8f)), c.eulerAngles.y + 10f);
            Board(tent, "SideA", new Vector3(2.4f, 0.04f, 1.5f), m.fabric, new Vector3(0f, 0.62f, 0.5f), new Vector3(-52f, 0f, 0f));
            Board(tent, "SideB", new Vector3(2.4f, 0.04f, 1.5f), m.fabric, new Vector3(0.2f, 0.15f, -0.9f), new Vector3(8f, 6f, 0f), false);
            LogPiece(tent, "Pole", 1.3f, 0.025f, m, new Vector3(-1.15f, 0.55f, 0f), new Vector3(-80f, 0f, 0f), false);
            LogPiece(tent, "PoleFallen", 1.3f, 0.025f, m, new Vector3(1.3f, 0.05f, -0.4f), new Vector3(0f, 60f, 0f), false);
            // Fogata apagada: anillo de piedras y leña quemada.
            var fire = Group(c, "Firepit", c.TransformPoint(new Vector3(1.2f, 0f, -0.5f)), 0f);
            var stone = RockPrefab(0, false, m);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var s = Spawn(stone, fire, fire.TransformPoint(new Vector3(Mathf.Cos(a) * 0.65f, -0.05f, Mathf.Sin(a) * 0.65f)), i * 45f, 0.22f);
                Object.DestroyImmediate(s.GetComponent<Collider>());
            }
            var charred = Tinted(m.bark, "Hollow_Charred", new Color(0.12f, 0.1f, 0.09f));
            var cl1 = Piece(fire, "Charred1", LogMesh(0.8f, 0.07f), new[] { charred, charred }, new Vector3(0f, 0.08f, 0f), new Vector3(0f, 30f, 0f), false);
            var cl2 = Piece(fire, "Charred2", LogMesh(0.7f, 0.06f), new[] { charred, charred }, new Vector3(0.05f, 0.12f, 0.02f), new Vector3(0f, -50f, 8f), false);
            // Bancos de tronco, mochila, farol volcado.
            LogPiece(c, "Bench1", 1.8f, 0.2f, m, new Vector3(2.9f, 0.18f, -0.4f), new Vector3(0f, 10f, 0f));
            LogPiece(c, "Bench2", 1.6f, 0.2f, m, new Vector3(0.9f, 0.18f, -2.4f), new Vector3(0f, 80f, 0f));
            var pack = Tinted(m.fabric, "Hollow_Backpack", new Color(0.45f, 0.2f, 0.15f));
            Board(c, "Backpack", new Vector3(0.45f, 0.6f, 0.28f), pack, new Vector3(-0.2f, 0.28f, -1.0f), new Vector3(0f, 25f, -12f), false);
            Board(c, "Lantern_Fallen", new Vector3(0.14f, 0.2f, 0.14f), m.darkMetal, new Vector3(2.0f, 0.07f, 0.9f), new Vector3(90f, 30f, 0f), false);
            Board(c, "SleepingBag", new Vector3(0.8f, 0.12f, 1.9f), Tinted(m.fabric, "Hollow_SleepingBag", new Color(0.35f, 0.45f, 0.6f)), new Vector3(-3.2f, 0.07f, -0.6f), new Vector3(0f, 40f, 0f), false);
            Sign(c, "TRUCK >\nACCESS RD", new Vector3(5.2f, 0f, 1.5f), 80f, m);
            blockers.Add(Camp);
            return 1;
        }

        // ---------- Camino de acceso con la camioneta del guardabosques (final).

        static int BuildRoadAndTruck(Transform parent, Terrain t, Mats m, List<Vector3> blockers)
        {
            var truck = Group(parent, "RangerTruck", OnGround(t, TruckPos.x, TruckPos.z, 0f), 4f);
            truck.localRotation = Quaternion.Euler(0f, 4f, 2.5f); // llanta pinchada
            var paint = Tinted(m.metal, "Hollow_TruckPaint", new Color(0.55f, 0.6f, 0.5f));
            Board(truck, "Chassis", new Vector3(2.0f, 0.5f, 5.2f), m.darkMetal, new Vector3(0f, 0.7f, 0f), Vector3.zero, false);
            Board(truck, "Hood", new Vector3(1.9f, 0.45f, 1.5f), paint, new Vector3(0f, 1.15f, 1.75f), new Vector3(-3f, 0f, 0f), false);
            Board(truck, "Cab", new Vector3(1.9f, 1.05f, 1.6f), paint, new Vector3(0f, 1.45f, 0.25f), Vector3.zero, false);
            Board(truck, "Windshield", new Vector3(1.7f, 0.6f, 0.05f), m.water, new Vector3(0f, 1.65f, 1.07f), new Vector3(-18f, 0f, 0f), false);
            Board(truck, "BedFloor", new Vector3(1.9f, 0.1f, 2.3f), m.darkMetal, new Vector3(0f, 1.0f, -1.75f), Vector3.zero, false);
            Board(truck, "BedL", new Vector3(0.08f, 0.5f, 2.3f), paint, new Vector3(-0.92f, 1.3f, -1.75f), Vector3.zero, false);
            Board(truck, "BedR", new Vector3(0.08f, 0.5f, 2.3f), paint, new Vector3(0.92f, 1.3f, -1.75f), Vector3.zero, false);
            Board(truck, "Tailgate", new Vector3(1.9f, 0.5f, 0.08f), paint, new Vector3(0f, 0.85f, -3.0f), new Vector3(70f, 0f, 0f), false);
            Board(truck, "DoorOpen", new Vector3(0.06f, 0.9f, 1.1f), paint, new Vector3(-1.45f, 1.3f, 0.75f), new Vector3(0f, -50f, 0f), false);
            Board(truck, "LightBar", new Vector3(1.2f, 0.1f, 0.25f), Tinted(m.darkMetal, "Hollow_LightBar", new Color(0.4f, 0.1f, 0.1f)), new Vector3(0f, 2.03f, 0.3f), Vector3.zero, false);
            var tire = Tinted(m.darkMetal, "Hollow_Tire", new Color(0.08f, 0.08f, 0.08f));
            foreach (var w in new[] { new Vector3(-1f, 0.42f, 1.6f), new Vector3(1f, 0.42f, 1.6f), new Vector3(-1f, 0.42f, -1.7f), new Vector3(1f, 0.32f, -1.7f) })
                Piece(truck, "Wheel", LogMesh(0.3f, 0.42f), new[] { tire, tire }, w, new Vector3(0f, 90f, 0f), false);
            var body = truck.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 1.1f, 0f);
            body.size = new Vector3(2.1f, 2.2f, 5.6f);

            // La radio: se usa desde la puerta abierta del conductor.
            var radio = new GameObject("Radio");
            radio.transform.SetParent(truck, false);
            radio.transform.localPosition = new Vector3(-1.5f, 1.2f, 0.4f);
            // El dial encendido se ve por la puerta abierta: un punto ámbar que guía hacia la meta sin gastar una luz.
            var dial = Tinted(m.darkMetal, "Hollow_RadioDial", new Color(0.2f, 0.12f, 0.05f));
            dial.EnableKeyword("_EMISSION");
            dial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // con None, URP quita _EMISSION
            dial.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.18f) * 3.2f);
            Board(truck, "RadioDial", new Vector3(0.03f, 0.1f, 0.34f), dial, new Vector3(-0.965f, 1.5f, 0.55f), Vector3.zero, false);
            var cabGlow = Tinted(dial, "Hollow_CabGlow", new Color(0.15f, 0.1f, 0.06f));
            cabGlow.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.25f) * 0.45f);
            cabGlow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Board(truck, "DomeGlow", new Vector3(0.03f, 0.5f, 0.9f), cabGlow, new Vector3(-0.962f, 1.55f, 0.2f), Vector3.zero, false);
            var climax = radio.AddComponent<RadioClimax>();
            var cso = new SerializedObject(climax);
            cso.FindProperty("playerHealth").objectReferenceValue = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerHealth>();
            cso.ApplyModifiedPropertiesWithoutUndo();
            MakeFadeable(truck.gameObject);

            // Barrera de "camino cerrado" al sur y postes con reflectores.
            var barrier = Group(parent, "RoadClosed", OnGround(t, RoadX, -6.2f), 0f);
            foreach (float x in new[] { -2.2f, 2.2f }) LogPiece(barrier, "Post", 1.1f, 0.08f, m, new Vector3(x, 0.55f, 0f), new Vector3(-90f, 0f, 0f));
            var stripe = Tinted(m.planks, "Hollow_BarrierStripe", new Color(0.9f, 0.55f, 0.35f));
            Board(barrier, "Bar", new Vector3(5f, 0.3f, 0.08f), stripe, new Vector3(0f, 0.9f, 0f), new Vector3(0f, 0f, -4f));
            Sign(barrier, "ROAD CLOSED", new Vector3(3.6f, 0f, 0.4f), 0f, m);
            for (float z = -4f; z < 7f; z += 5f)
            {
                if (Mathf.Abs(z - TruckPos.z) < 3f) continue;
                foreach (float s in new[] { -1f, 1f })
                {
                    var p = OnGround(t, RoadX + s * (RoadHalf + 0.4f), z + (s > 0 ? 2f : 0f));
                    if (Walk(p) > -0.4f) continue;
                    var post = Group(parent, "RoadMarker", p, 0f);
                    LogPiece(post, "Post", 1f, 0.05f, m, new Vector3(0f, 0.5f, 0f), new Vector3(-90f, 0f, 0f), false);
                    Board(post, "Reflector", new Vector3(0.08f, 0.1f, 0.02f), m.glowLantern, new Vector3(0f, 0.9f, -0.05f), Vector3.zero, false);
                }
            }
            blockers.Add(TruckPos);
            return 1;
        }

        // ---------- Postes guía del sendero, troncos caídos como cobertura, árboles secos.

        /// <summary>Diámetro (m) del charco de luz cálida bajo cada farol del sendero.</summary>
        const float WarmPoolSize = 4.2f; // P9: sin el brillo blanco del arroyo faltaban islas de luz (histograma de P3)

        /// <summary>Charco de luz cálida de los faroles del sendero.</summary>
        static Material WarmPoolMaterial()
        {
            // Ámbar de farol: HDR en RGB y alfa < 1 (en aditivo el alfa multiplica y saturaba a amarillo).
            var mat = FxMaterial("FX_WarmPool", "Universal Render Pipeline/Particles/Unlit", SoftDot(), new Color(1.2f, 0.36f, 0.05f, 0.9f), true);
            // Sin partículas suaves: el charco está a pocos cm del suelo y la diferencia de profundidad cuantizada lo partía en
            // anillos escalonados (y lo atenuaba casi entero; por eso el color de antes era el doble).
            mat.SetFloat("_SoftParticlesEnabled", 0f);
            mat.DisableKeyword("_SOFTPARTICLES_ON");
            return mat;
        }

        static int BuildTrailDressing(Transform parent, Terrain t, Mats m, List<Vector3> blockers)
        {
            var g = new GameObject("TrailDressing").transform;
            g.SetParent(parent, false);
            var orange = Tinted(m.planks, "Hollow_TrailBlaze", new Color(1f, 0.55f, 0.25f));
            orange.EnableKeyword("_EMISSION");
            orange.SetColor("_EmissionColor", new Color(0.35f, 0.12f, 0.03f));
            var lantern = Tinted(m.planks, "Hollow_TrailLantern", new Color(1f, 0.8f, 0.55f));
            lantern.EnableKeyword("_EMISSION");
            lantern.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.28f) * 9f);
            // Sin la bandera de GI, URP descarta la emisión ("EmissiveIsBlack") y el farol se veía negro.
            lantern.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            orange.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var warmPool = WarmPoolMaterial();
            // Postes con marca naranja a la salida de cada sala: dicen "por aquí" sin texto.
            // Uno a la salida de cada sala y otro, del lado opuesto, a la entrada de la siguiente (junto al checkpoint: la
            // luz cálida marca el lugar seguro; antes del segundo, los pasillos medían ~1% de islas de luz en P3).
            var spots = new List<Vector3>();
            foreach (var c in Corridors)
            {
                Vector3 a = new Vector3(c.x, 0f, c.y), b = new Vector3(c.z, 0f, c.w);
                Vector3 dir = (b - a).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                spots.Add(a + dir * 1.5f + side * 1.4f);
                spots.Add(b - dir * 1.5f - side * 1.4f);
            }
            foreach (var s in spots)
            {
                var p = OnGround(t, s.x, s.z);
                var post = Group(g, "TrailMarker", p, Random.Range(0f, 360f));
                LogPiece(post, "Post", 1.2f, 0.07f, m, new Vector3(0f, 0.55f, 0f), new Vector3(-90f, 0f, 0f), false);
                Board(post, "Blaze", new Vector3(0.16f, 0.2f, 0.16f), orange, new Vector3(0f, 0.95f, 0f), Vector3.zero, false);
                // Farol en la punta y un charco de luz cálida en el suelo (aditivo, sin luz dinámica): "las luces cálidas
                // marcan el camino" (POLISH_PASS 3.9) y son las islas de luz de las salas sin haces (3.10).
                Board(post, "Lantern", new Vector3(0.14f, 0.18f, 0.14f), lantern, new Vector3(0f, 1.28f, 0f), Vector3.zero, false);
                var pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(pool.GetComponent<Collider>());
                pool.name = "WarmPool";
                pool.transform.SetParent(post, false);
                pool.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                pool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pool.transform.localScale = Vector3.one * WarmPoolSize;
                var pr = pool.GetComponent<MeshRenderer>();
                pr.sharedMaterial = warmPool;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                pr.receiveShadows = false;
            }

            // Troncos caídos de diseño.
            foreach (var l in CoverLogs)
            {
                var p = OnGround(t, l.x, l.y, 0.32f);
                var go = LogPiece(g, "FallenLog", l.w, 0.42f, m, Vector3.zero, Vector3.zero);
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, l.z, 0f));
                MakeNavBlocker(go);
                blockers.Add(p);
            }

            // Árboles secos sobre los acantilados del norte: siluetas contra la niebla, nunca tapan.
            var dead = new[] { new Vector2(-60f, 8.4f), new Vector2(-35f, 9f), new Vector2(-9f, 9.6f), new Vector2(14f, 10.2f), new Vector2(39f, 8.4f), new Vector2(57f, 10f) };
            foreach (var d in dead)
            {
                var p = OnGround(t, d.x, d.y, -0.1f);
                var tree = Group(g, "DeadTree", p, Random.Range(0f, 360f));
                LogPiece(tree, "Trunk", 6.5f, 0.24f, m, new Vector3(0f, 3.1f, 0f), new Vector3(-88f, 0f, 0f));
                LogPiece(tree, "Limb1", 2.4f, 0.1f, m, new Vector3(0.6f, 4.4f, 0f), new Vector3(-40f, 90f, 0f), false);
                LogPiece(tree, "Limb2", 1.9f, 0.08f, m, new Vector3(-0.5f, 3.6f, 0.3f), new Vector3(-30f, -100f, 0f), false);
                LogPiece(tree, "Limb3", 1.5f, 0.07f, m, new Vector3(0.1f, 5.3f, -0.4f), new Vector3(-55f, 200f, 0f), false);
                blockers.Add(p);
            }
            return 1;
        }

        // ------------------------------------------------------------------ Jugador, Lurkers, objetos, checkpoints

        static void SetupPlayerAndEnemies(Terrain t, GameObject player, System.Text.StringBuilder log)
        {
            var cabinRot = Quaternion.Euler(0f, CabinYaw, 0f);
            Vector3 start = OnGround(t, CabinCenter.x, CabinCenter.z, 0.25f) + cabinRot * new Vector3(0.6f, 0f, -0.8f);
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, CabinYaw + 90f, 0f));
            cc.enabled = true;
            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.SetPositionAndRotation(start, player.transform.rotation);
            var respawn = player.GetComponent<ProjectRidge.Player.PlayerRespawn>();
            if (respawn != null) Object.DestroyImmediate(respawn); // el GameFlow maneja muerte y checkpoints

            var template = GameObject.Find("Lurker");
            foreach (var (name, pts) in Patrols)
            {
                var lurker = name == "Lurker" ? template : Object.Instantiate(template);
                lurker.name = name;
                var route = Patrol("LurkerPatrol" + name.Substring("Lurker".Length), t, pts);
                SetPatrol(lurker, route, t);
                EnsureVisionCone(lurker);
            }
            log.AppendLine("lurkers=" + Patrols.Length);
        }

        static Transform[] Patrol(string name, Terrain t, Vector2[] pts)
        {
            var root = new GameObject(name).transform;
            var list = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var p = new GameObject("P" + i).transform;
                p.SetParent(root, false);
                p.position = OnGround(t, pts[i].x, pts[i].y, 0.02f);
                list[i] = p;
            }
            // En cada parada mira hacia afuera de su ruta, sobre todo a los lados (por donde llega el papá).
            // Una patrulla en línea (ida y vuelta) mira hacia afuera en cada extremo: le da la espalda al centro, y esa
            // es la ventana para cruzar su ruta (patrón legible).
            Vector3 c = Vector3.zero;
            foreach (var p in list) c += p.position;
            c /= list.Length;
            foreach (var p in list)
            {
                Vector3 o = p.position - c;
                o.y = 0f;
                if (list.Length > 2)
                {
                    o.z *= 0.35f;
                    if (o.sqrMagnitude < 0.25f || Mathf.Abs(o.x) < 0.4f) o = Vector3.left;
                }
                p.rotation = Quaternion.LookRotation(o.normalized);
            }
            return list;
        }

        static void SetPatrol(GameObject lurker, Transform[] pts, Terrain t)
        {
            lurker.transform.position = pts[0].position;
            // Desactivado en la escena: EnemyController lo activa en Start, cuando el NavMesh ya está registrado.
            lurker.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            var enemy = lurker.GetComponent<ProjectRidge.AI.EnemyController>();
            var so = new SerializedObject(enemy);
            var arr = so.FindProperty("patrolPoints");
            arr.arraySize = pts.Length;
            for (int i = 0; i < pts.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = pts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Cono de visión en el suelo: el patrón del Lurker se lee de un vistazo.</summary>
        static void EnsureVisionCone(GameObject lurker)
        {
            var old = lurker.transform.Find("VisionCone");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("VisionCone");
            go.transform.SetParent(lurker.transform, false);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            string path = DataDir + "/FX_VisionCone.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("ProjectRidge/VisionCone")); AssetDatabase.CreateAsset(mat, path); }
            mr.sharedMaterial = mat;
            var cone = go.AddComponent<ProjectRidge.AI.VisionCone>();
            var enemy = lurker.GetComponent<ProjectRidge.AI.EnemyController>();
            var so = new SerializedObject(cone);
            so.FindProperty("enemy").objectReferenceValue = enemy;
            so.FindProperty("config").objectReferenceValue = new SerializedObject(enemy).FindProperty("config").objectReferenceValue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void PlaceItems(Terrain t, System.Text.StringBuilder log)
        {
            var root = new GameObject("Items").transform;
            var cabinRot = Quaternion.Euler(0f, CabinYaw, 0f);
            Vector3 Cabin(float x, float z) => OnGround(t, CabinCenter.x, CabinCenter.z, 0.16f) + cabinRot * new Vector3(x, 0f, z);
            Vector3 At(float x, float z) => OnGround(t, x, z, 0.02f);

            var config = ItemsSetupBuilder.LoadConfig();
            var med = ItemsSetupBuilder.LoadItem("Medkit");
            var flare = ItemsSetupBuilder.LoadItem("Flare");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_weathered_brown_planks.mat");

            // Refugio: suministros.
            ItemsSetupBuilder.Crate(root, "Crate_Shelter", Cabin(2.3f, -1.4f), CabinYaw + 15f, config, wood,
                new SearchableContainer.Entry { item = med, amount = 1 });

            // Pasillo antes del arroyo: la primera bengala, justo antes del primer Lurker. En todo el nivel: 3 bengalas y 3
            // botiquines (POLISH_PASS 3.3).
            ItemsSetupBuilder.PickupObject(root, "Flare", At(-26.2f, -1.3f), 20f);
            // Sala 3: la bengala está justo en el cruce de los dos reflectores (recompensa con riesgo).
            ItemsSetupBuilder.PickupObject(root, "Flare", At(11.2f, 0.6f), 70f);
            ItemsSetupBuilder.PickupObject(root, "Medkit", At(16.4f, 5.6f), 200f);
            // Sala 4: caja escondida en el centro, entre las rocas.
            ItemsSetupBuilder.Crate(root, "Crate_Thicket", OnGround(t, 34.4f, 0.6f), 160f, config, wood,
                new SearchableContainer.Entry { item = flare, amount = 1 });
            // Campamento.
            ItemsSetupBuilder.Crate(root, "Crate_Camp", OnGround(t, Camp.x - 2.6f, Camp.z - 2.2f), 70f, config, wood,
                new SearchableContainer.Entry { item = med, amount = 1 });

            // Piedras para distraer, justo antes de cada sala con Lurker.
            ItemsSetupBuilder.EnsureItem("Stone", "Stone", "STONE", 4);
            foreach (var s in new[] { new Vector2(-25.5f, 1.1f), new Vector2(-21.6f, -1.6f), new Vector2(-3.6f, -2.6f), new Vector2(24.6f, -1.6f), new Vector2(29.4f, -4.2f), new Vector2(48.4f, 1.2f), new Vector2(51.2f, -2.2f) })
                ItemsSetupBuilder.PickupObject(root, "Stone", OnGround(t, s.x, s.y, 0.04f), Random.Range(0f, 360f));

            // Garrote de repuesto junto al puente.
            var club = new GameObject("Pickup_Club");
            club.transform.SetParent(root, false);
            club.transform.position = OnGround(t, -21f, 2.2f, 0.06f);
            club.transform.rotation = Quaternion.Euler(0f, 30f, 90f);
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(stick.GetComponent<Collider>());
            stick.transform.SetParent(club.transform, false);
            stick.transform.localScale = new Vector3(0.07f, 0.4f, 0.07f);
            stick.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_pine_bark.mat");
            var cp = club.AddComponent<ClubPickup>();
            var cso = new SerializedObject(cp);
            cso.FindProperty("config").objectReferenceValue = config;
            cso.ApplyModifiedPropertiesWithoutUndo();

            PlaceClues(root, t, Cabin);
            log.AppendLine("items placed");
        }

        static void PlaceClues(Transform root, Terrain t, System.Func<float, float, Vector3> cabin)
        {
            var paper = Simple("Hollow_Paper", null, null, new Color(0.85f, 0.82f, 0.72f), 0.2f);
            paper.EnableKeyword("_EMISSION");
            paper.SetColor("_EmissionColor", new Color(0.12f, 0.11f, 0.09f));
            var red = Simple("Hollow_RedCanvas", null, null, new Color(0.6f, 0.08f, 0.06f), 0.25f);

            GameObject Make(string name, Vector3 pos, float yaw, string display, string title, string body, string objective, Material mat, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                Board(go.transform, "Visual", size, mat, new Vector3(0f, size.y * 0.5f, 0f), Vector3.zero, false);
                var clue = go.AddComponent<Clue>();
                var so = new SerializedObject(clue);
                so.FindProperty("displayName").stringValue = display;
                so.FindProperty("title").stringValue = title;
                so.FindProperty("body").stringValue = body;
                so.FindProperty("objectiveAfter").stringValue = objective;
                so.ApplyModifiedPropertiesWithoutUndo();
                return go;
            }

            // 1. Refugio: la foto familiar, sobre la mesa junto al farol.
            var tablePos = cabin(1.2f, 1.4f) + Vector3.up * 0.83f;
            Make("Clue_Photo", tablePos, CabinYaw + 20f, "the photograph", "A PHOTOGRAPH",
                "Sarah, Emma and Leo at the trailhead, squinting into the sun.\nOn the back, in Sarah's handwriting:\n\"Ranger station 7. Wait for us here.\"\n\nThe station is empty. The door is gone.",
                "Follow the trail east.", paper, new Vector3(0.16f, 0.01f, 0.11f));

            // 2. Junto al puente: la zapatilla roja de Leo (alguien pasó corriendo).
            Make("Clue_Sneaker", OnGround(t, RavineX(BridgeZ) + 6.2f, BridgeZ - 1.4f, 0f), 35f, "the small shoe", "A SMALL RED SNEAKER",
                "Leo's. The laces are still double-knotted, the way you taught him.\nThe mud around it is churned. Long, narrow prints, too far apart to be a man's.\n\nThey came this way. They were running.",
                "Find where they went.", red, new Vector3(0.12f, 0.09f, 0.24f));

            // 3. Campamento: el mapa con la ruta a la camioneta.
            Make("Clue_Map", OnGround(t, Camp.x + 0.4f, Camp.z - 1.6f, 0.38f), -30f, "the map", "A TRAIL MAP",
                "Pinned under a rock on the bench. A route drawn in red marker from the station to the access road, circled twice:\n\"RANGER TRUCK - RADIO\"\n\nUnder it, smaller, shakier: \"they only come when the light is on you\"",
                "Reach the ranger truck.", paper, new Vector3(0.42f, 0.01f, 0.3f));
        }

        static void BuildCheckpoints(Terrain t)
        {
            var root = new GameObject("Checkpoints").transform;
            void Cp(string name, float x, float z, Vector3 size, string objective)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = OnGround(t, x, z, 0.3f);
                go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = size;
                box.center = Vector3.up * 1f;
                var cp = go.AddComponent<ProjectRidge.Core.Checkpoint>();
                var so = new SerializedObject(cp);
                so.FindProperty("objective").stringValue = objective;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // En los pasillos entre salas: lugares seguros, fuera de rutas y conos. El objetivo enseña el loop.
            Cp("CP_FirstLight", -48.5f, 0f, new Vector3(3f, 3f, 4.5f), "Stay out of the red light.");
            Cp("CP_Creek", -26f, 0f, new Vector3(3f, 3f, 4.5f), "Cross the creek.");
            Cp("CP_TwoLights", 0.2f, 0.7f, new Vector3(3f, 3f, 4.5f), "Slip between the lights.");
            Cp("CP_Thicket", 24f, 0f, new Vector3(3f, 3f, 4.5f), "Find where they went.");
            Cp("CP_Camp", 47.6f, -0.5f, new Vector3(3f, 3f, 4.5f), "Reach the ranger truck.");
            // Fuera de la ruta de S5 y de los reflectores del clímax (que se apagan al morir).
            Cp("CP_Radio", 58.6f, -2f, new Vector3(3f, 3f, 6f), "Use the truck's radio.");
        }

        // ------------------------------------------------------------------ Reflectores rojos

        static string BuildSearchlights(Terrain t)
        {
            const string cfgPath = "Assets/_Project/Data/SearchlightConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<ProjectRidge.AI.SearchlightConfig>(cfgPath);
            if (cfg == null) { cfg = ScriptableObject.CreateInstance<ProjectRidge.AI.SearchlightConfig>(); AssetDatabase.CreateAsset(cfg, cfgPath); }

            var noise = NoiseTex();
            string poolPath = FxDir + "/FX_LightPool.mat";
            var pool = AssetDatabase.LoadAssetAtPath<Material>(poolPath);
            if (pool == null) { pool = new Material(Shader.Find("ProjectRidge/LightPool")); AssetDatabase.CreateAsset(pool, poolPath); }
            pool.SetTexture("_NoiseTex", noise);
            pool.SetColor("_Color", cfg.color);
            pool.SetFloat("_RimWidth", 0.03f);   // borde fino que parpadea (no un neón grueso)
            pool.SetFloat("_RimIntensity", 1.3f);
            pool.SetFloat("_Intensity", 0.95f); // P9: a 1.6 el relleno saturaba y el círculo parecía un disco macizo (con el borde suave basta 0.95)
            EditorUtility.SetDirty(pool);

            string beamPath = FxDir + "/FX_SearchBeam.mat";
            var beam = AssetDatabase.LoadAssetAtPath<Material>(beamPath);
            if (beam == null) { beam = new Material(Shader.Find("ProjectRidge/VolumetricBeam")); AssetDatabase.CreateAsset(beam, beamPath); }
            beam.SetTexture("_NoiseTex", noise);
            beam.SetColor("_Color", cfg.color);
            beam.SetFloat("_Intensity", 1.1f);
            beam.SetFloat("_Density", 0.1f);
            beam.SetFloat("_NoiseScale", 0.16f);
            beam.SetFloat("_NoiseSpeed", 0.3f);
            beam.SetFloat("_CameraFade", 6f);
            beam.SetFloat("_EdgePower", 1.4f);
            beam.SetFloat("_LengthFalloff", 0.6f);
            beam.SetFloat("_GroundFade", 2.5f); // contacto suave con el suelo (P9: el "balde" rojo)
            EditorUtility.SetDirty(beam);

            var cone = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Meshes/UFO_BeamCone.asset");
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            var root = new GameObject("Searchlights").transform;
            var built = new Dictionary<string, ProjectRidge.AI.Searchlight>();
            foreach (var d in Searchlights)
            {
                var go = new GameObject(d.name);
                go.transform.SetParent(root, false);
                var poolGo = new GameObject("Pool");
                poolGo.transform.SetParent(go.transform, false);
                poolGo.AddComponent<MeshFilter>().sharedMesh = cube;
                var pr = poolGo.AddComponent<MeshRenderer>();
                pr.sharedMaterial = pool;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                pr.receiveShadows = false;
                var beamGo = new GameObject("Beam");
                beamGo.transform.SetParent(go.transform, false);
                beamGo.AddComponent<MeshFilter>().sharedMesh = cone;
                var br = beamGo.AddComponent<MeshRenderer>();
                br.sharedMaterial = beam;
                br.shadowCastingMode = ShadowCastingMode.Off;
                br.receiveShadows = false;

                var s = go.AddComponent<ProjectRidge.AI.Searchlight>();
                var so = new SerializedObject(s);
                so.FindProperty("config").objectReferenceValue = cfg;
                var route = so.FindProperty("route");
                route.arraySize = d.route.Length;
                for (int i = 0; i < d.route.Length; i++) route.GetArrayElementAtIndex(i).vector3Value = OnGround(t, d.route[i].x, d.route[i].y);
                so.FindProperty("loop").boolValue = d.loop;
                so.FindProperty("speed").floatValue = d.speed;
                so.FindProperty("pauseAtPoints").floatValue = d.pause;
                so.FindProperty("phase").floatValue = d.phase;
                so.FindProperty("startActive").boolValue = d.startActive;
                so.FindProperty("spotVolume").objectReferenceValue = pr;
                so.FindProperty("beamVisual").objectReferenceValue = br;
                so.FindProperty("dust").objectReferenceValue = BeamDust(go.transform, cfg.spotRadius);
                so.ApplyModifiedPropertiesWithoutUndo();
                go.transform.position = OnGround(t, d.route[0].x, d.route[0].y);
                built[d.name] = s;
            }

            // Una sola luz real con sombras, en el reflector más cercano al papá.
            var lightGo = new GameObject("SearchlightLight");
            lightGo.transform.SetParent(root, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.shadows = LightShadows.Soft;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.color = cfg.color;
            light.cookie = BeamCookie();
            var proxy = lightGo.AddComponent<ProjectRidge.AI.SearchlightLightProxy>();
            var pso = new SerializedObject(proxy);
            pso.FindProperty("config").objectReferenceValue = cfg;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // La radio enciende los reflectores del clímax.
            var radio = Object.FindFirstObjectByType<RadioClimax>();
            if (radio != null)
            {
                var rso = new SerializedObject(radio);
                var arr = rso.FindProperty("climaxLights");
                arr.arraySize = 2;
                arr.GetArrayElementAtIndex(0).objectReferenceValue = built["S6_Climax"];
                arr.GetArrayElementAtIndex(1).objectReferenceValue = built["S7_Climax"];
                rso.ApplyModifiedPropertiesWithoutUndo();
            }
            return "searchlights=" + Searchlights.Length;
        }
    }
}
