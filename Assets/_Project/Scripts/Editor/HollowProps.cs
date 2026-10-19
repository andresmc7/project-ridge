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

        // ------------------------------------------------------------------ Hitos

        static int BuildLandmarks(Transform root, Terrain t, Mats m, List<Vector3> blockers)
        {
            var parent = new GameObject("Landmarks").transform;
            parent.SetParent(root, false);
            int n = 0;
            n += BuildCabin(parent, t, m, blockers);
            n += BuildBridge(parent, t, m);
            n += BuildLogCrossing(parent, t, m);
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
            Wall("Wall_S", new Vector3(0f, 0f, -D * 0.5f), 180f, W, new[] { new Panel(0f, 1.7f, 2.2f), new Panel(1.7f, 1.2f, 0.85f), new Panel(2.9f, 1.3f, 1.9f), new Panel(4.2f, 2.0f, 2.4f) });
            Wall("Wall_W", new Vector3(-W * 0.5f, 0f, 0f), -90f, D, new[] { new Panel(0f, 1.8f, 2.5f), new Panel(1.8f, 1.5f, 1.4f), new Panel(3.3f, 1.9f, 2.0f) });
            Wall("Wall_E", new Vector3(W * 0.5f, 0f, 0f), 90f, D, new[] { new Panel(0f, 1.7f, 2.2f), new Panel(3.0f, 2.2f, 1.6f) }); // puerta de 1.3 m
            // Dintel sobre la ventana sur y un tablón suelto colgando.
            Board(c, "Sill_S", new Vector3(1.2f, 0.12f, 0.22f), m.planks, new Vector3(-0.75f, 0.95f, -D * 0.5f), Vector3.zero, false);
            Board(c, "HangingBoard", new Vector3(1.4f, 0.25f, 0.08f), m.planks, new Vector3(0.3f, 1.25f, -D * 0.5f - 0.25f), new Vector3(0f, 10f, 35f), false);

            // Postes de las esquinas.
            foreach (var p in new[] { new Vector3(-W, 0, -D), new Vector3(W, 0, -D), new Vector3(-W, 0, D), new Vector3(W, 0, D) })
                LogPiece(c, "CornerPost", 2.7f, 0.14f, m, new Vector3(p.x * 0.5f, 1.35f, p.z * 0.5f), new Vector3(-90f, 0f, 0f));

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
            Spawn(rootRock, g, g.TransformPoint(new Vector3(-6.6f, -0.2f, 0f)), 20f, 1.3f);
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
            for (float z = -46f; z < 46f; z += 3f)
            {
                float zc = z + 1.5f;
                float x = RavineX(zc);
                float slope = Mathf.Atan(1.5f * 0.07f * Mathf.Cos(zc * 0.07f)) * Mathf.Rad2Deg;
                var w = Board(g, "Water", new Vector3(4.6f, 0.05f, 3.3f), m.water, new Vector3(x, -2.35f, zc), new Vector3(0f, -slope, 0f), false);
                w.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
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
            for (int i = 0; i < 26; i++)
            {
                float z = -38f + i * 3f + (float)rng.NextDouble() * 2f;
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
            radio.AddComponent<RadioEnding>();

            // Barrera de "camino cerrado" al sur y postes con reflectores.
            var barrier = Group(parent, "RoadClosed", OnGround(t, RoadX, -14f), 0f);
            foreach (float x in new[] { -2.2f, 2.2f }) LogPiece(barrier, "Post", 1.1f, 0.08f, m, new Vector3(x, 0.55f, 0f), new Vector3(-90f, 0f, 0f));
            var stripe = Tinted(m.planks, "Hollow_BarrierStripe", new Color(0.9f, 0.55f, 0.35f));
            Board(barrier, "Bar", new Vector3(5f, 0.3f, 0.08f), stripe, new Vector3(0f, 0.9f, 0f), new Vector3(0f, 0f, -4f));
            Sign(barrier, "ROAD CLOSED", new Vector3(3.6f, 0f, 0.4f), 0f, m);
            for (float z = -36f; z < 38f; z += 9f)
            {
                if (Mathf.Abs(z - TruckPos.z) < 4f) continue;
                foreach (float s in new[] { -1f, 1f })
                {
                    var p = OnGround(t, RoadX + s * (RoadHalf + 0.4f), z + (s > 0 ? 3f : 0f));
                    var post = Group(parent, "RoadMarker", p, 0f);
                    LogPiece(post, "Post", 1f, 0.05f, m, new Vector3(0f, 0.5f, 0f), new Vector3(-90f, 0f, 0f), false);
                    Board(post, "Reflector", new Vector3(0.08f, 0.1f, 0.02f), m.glowLantern, new Vector3(0f, 0.9f, -0.05f), Vector3.zero, false);
                }
            }
            blockers.Add(TruckPos);
            return 1;
        }

        // ---------- Postes guía del sendero, troncos caídos como cobertura, árboles secos.

        static int BuildTrailDressing(Transform parent, Terrain t, Mats m, List<Vector3> blockers)
        {
            var g = new GameObject("TrailDressing").transform;
            g.SetParent(parent, false);
            var orange = Tinted(m.planks, "Hollow_TrailBlaze", new Color(1f, 0.55f, 0.25f));
            orange.EnableKeyword("_EMISSION");
            orange.SetColor("_EmissionColor", new Color(0.35f, 0.12f, 0.03f));
            foreach (var path in new[] { MainTrail, NorthTrail })
                for (int i = 1; i < path.Length - 1; i += 2)
                {
                    Vector3 dir = (path[i + 1] - path[i - 1]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    var p = OnGround(t, path[i].x + side.x * 2.2f, path[i].z + side.z * 2.2f);
                    var post = Group(g, "TrailMarker", p, Random.Range(0f, 360f));
                    LogPiece(post, "Post", 1.2f, 0.07f, m, new Vector3(0f, 0.55f, 0f), new Vector3(-90f, 0f, 0f), false);
                    Board(post, "Blaze", new Vector3(0.16f, 0.2f, 0.16f), orange, new Vector3(0f, 0.95f, 0f), Vector3.zero, false);
                }

            // Troncos caídos: cobertura baja (agacharse detrás).
            var logs = new[] { new Vector4(-36, -3, 30, 4.5f), new Vector4(-25, -11, -60, 4f), new Vector4(-2, -2, 15, 5f), new Vector4(14, -20, 70, 4.5f),
                               new Vector4(26, -4, -20, 4f), new Vector4(4, 30, 10, 5f), new Vector4(16, 16, 80, 4f), new Vector4(40, -6, 45, 4.5f), new Vector4(-44, 12, 0, 5f) };
            foreach (var l in logs)
            {
                var p = OnGround(t, l.x, l.y, 0.32f);
                var go = LogPiece(g, "FallenLog", l.w, 0.42f, m, Vector3.zero, Vector3.zero);
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, l.z, 0f));
                blockers.Add(p);
            }

            // Árboles secos (siluetas inquietantes) junto al prado y el camino.
            var dead = new[] { new Vector2(-8, -18), new Vector2(22, -8), new Vector2(45, 12), new Vector2(-30, 14), new Vector2(36, -16) };
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

            // Lurker A: guarda el campamento y la llegada al camino (las dos rutas convergen aquí).
            var lurkerA = GameObject.Find("Lurker");
            var a = Patrol("LurkerPatrol", t, new[] { new Vector2(30, -4), new Vector2(39, -1), new Vector2(42, 9), new Vector2(33, 13), new Vector2(26, 6) });
            SetPatrol(lurkerA, a, t);

            // Lurker B: el bosque del norte (la ruta cubierta no es gratis).
            var lurkerB = Object.Instantiate(lurkerA);
            lurkerB.name = "Lurker_B";
            var b = Patrol("LurkerPatrol_B", t, new[] { new Vector2(-3, 23), new Vector2(6, 28), new Vector2(17, 23), new Vector2(9, 19), new Vector2(0, 21) });
            SetPatrol(lurkerB, b, t);
            log.AppendLine("lurkers=2");
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
            return list;
        }

        static void SetPatrol(GameObject lurker, Transform[] pts, Terrain t)
        {
            lurker.transform.position = pts[0].position;
            var enemy = lurker.GetComponent<ProjectRidge.AI.EnemyController>();
            var so = new SerializedObject(enemy);
            var arr = so.FindProperty("patrolPoints");
            arr.arraySize = pts.Length;
            for (int i = 0; i < pts.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = pts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void PlaceItems(Terrain t, System.Text.StringBuilder log)
        {
            var root = new GameObject("Items").transform;
            var cabinRot = Quaternion.Euler(0f, CabinYaw, 0f);
            Vector3 Cabin(float x, float z) => OnGround(t, CabinCenter.x, CabinCenter.z, 0.16f) + cabinRot * new Vector3(x, 0f, z);

            var config = ItemsSetupBuilder.LoadConfig();
            var med = ItemsSetupBuilder.LoadItem("Medkit");
            var flare = ItemsSetupBuilder.LoadItem("Flare");
            var scrap = ItemsSetupBuilder.LoadItem("Scrap");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterialBuilder.OutMaterials + "/PolyHaven_weathered_brown_planks.mat");

            // Refugio: suministros esparcidos.
            ItemsSetupBuilder.Crate(root, "Crate_Shelter", Cabin(2.3f, -1.6f), CabinYaw + 15f, config, wood,
                new SearchableContainer.Entry { item = med, amount = 1 }, new SearchableContainer.Entry { item = scrap, amount = 1 });
            ItemsSetupBuilder.PickupObject(root, "Scrap", Cabin(-1.2f, -1.7f), 40f);

            // Sendero y prado (la bengala del prado está bajo el barrido del OVNI: recompensa con riesgo).
            ItemsSetupBuilder.PickupObject(root, "Scrap", OnGround(t, -38f, -11f, 0.02f), 10f);
            ItemsSetupBuilder.PickupObject(root, "Flare", OnGround(t, Meadow.x + 1f, Meadow.z + 1f, 0.02f), 70f);
            ItemsSetupBuilder.PickupObject(root, "Medkit", OnGround(t, Meadow.x - 7.2f, Meadow.z - 5.2f, 0.02f), 200f);

            // Ruta norte: caja escondida entre arbustos.
            ItemsSetupBuilder.Crate(root, "Crate_North", OnGround(t, 2f, 29f), 160f, config, wood,
                new SearchableContainer.Entry { item = flare, amount = 1 }, new SearchableContainer.Entry { item = med, amount = 1 });
            ItemsSetupBuilder.PickupObject(root, "Scrap", OnGround(t, 14f, 25.5f, 0.02f), 120f);

            // Campamento.
            ItemsSetupBuilder.Crate(root, "Crate_Camp", OnGround(t, Camp.x - 2.6f, Camp.z - 2.2f), 70f, config, wood,
                new SearchableContainer.Entry { item = flare, amount = 1 }, new SearchableContainer.Entry { item = scrap, amount = 2 });
            ItemsSetupBuilder.PickupObject(root, "Medkit", OnGround(t, Camp.x + 3.6f, Camp.z + 2.8f, 0.02f), 15f);

            // Garrote de repuesto junto al puente.
            var club = new GameObject("Pickup_Club");
            club.transform.SetParent(root, false);
            club.transform.position = OnGround(t, RavineX(BridgeZ) - 6.5f, BridgeZ + 1.6f, 0.06f);
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
                "Reach the truck on the access road.", paper, new Vector3(0.42f, 0.01f, 0.3f));
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
            Cp("CP_Creek", -22f, -6.5f, new Vector3(6f, 3f, 3f), "Cross the creek.");
            Cp("CP_Creek_North", -20f, 20f, new Vector3(6f, 3f, 3f), "Cross the creek.");
            Cp("CP_Bridge", -4f, -7f, new Vector3(6f, 3f, 3f), "Find where they went.");
            Cp("CP_Log", -4f, 24.5f, new Vector3(6f, 3f, 3f), "Find where they went.");
            Cp("CP_Camp", 28f, 4f, new Vector3(10f, 3f, 3f), "");
        }

        static void PlaceUfoScanPoints(Terrain t)
        {
            var pointsRoot = GameObject.Find("UFOScanPoints").transform;
            for (int i = pointsRoot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(pointsRoot.GetChild(i).gameObject);
            // Barre el prado y la salida del puente (ruta expuesta).
            var xz = new[] { new Vector2(-4, -8), new Vector2(2, -18), new Vector2(9, -10), new Vector2(15, -18), new Vector2(21, -11), new Vector2(10, -22), new Vector2(3, -12) };
            var pts = new Transform[xz.Length];
            for (int i = 0; i < xz.Length; i++)
            {
                var p = new GameObject("S" + i).transform;
                p.SetParent(pointsRoot, false);
                p.position = OnGround(t, xz[i].x, xz[i].y);
                pts[i] = p;
            }
            var ufo = Object.FindFirstObjectByType<ProjectRidge.AI.UFOController>();
            var so = new SerializedObject(ufo);
            var arr = so.FindProperty("scanPoints");
            arr.arraySize = pts.Length;
            for (int i = 0; i < pts.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = pts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
