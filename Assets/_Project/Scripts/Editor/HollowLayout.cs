using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Layout de The Hollow (v3): una cadena de salas del tamaño de la pantalla unidas por pasillos, cada una un
    /// encuentro de sigilo con su patrón (reflectores rojos, Lurkers) y sus escondites (pasto alto). El loop:
    /// esconderse en la entrada, observar el patrón completo, avanzar de escondite en escondite, checkpoint.
    ///
    /// Cámara fija mirando al norte (yaw 0, pitch 55). Regla de oclusión: nada puede tapar el suelo caminable; un
    /// objeto a s metros al sur del área caminable puede medir como mucho ~1.43·s (tan 55°). Los bordes del lado
    /// de la cámara son cornisas bajas; los del norte, acantilados altos con bosque encima.
    /// </summary>
    public static partial class HollowBuilder
    {
        // Terreno: x ∈ [-76, 80], z ∈ [-26, 26].
        const float SizeX = 156f, SizeZ = 52f, MaxHeight = 24f, Base = 3f;
        static readonly Vector2 TerrainMin = new Vector2(-76f, -26f);
        const int HeightRes = 513, AlphaRes = 512;

        /// <summary>tan(pitch de la cámara): cuánto puede medir algo por cada metro que está al sur de lo caminable.</summary>
        const float OcclusionSlope = 1.43f;

        struct Room
        {
            public string name;
            public Vector2 c, half;
            public Room(string n, float x, float z, float hx, float hz) { name = n; c = new Vector2(x, z); half = new Vector2(hx, hz); }
        }

        static readonly Room[] Rooms =
        {
            new Room("Cabin", -62f, 0f, 7f, 5.5f),
            new Room("FirstLight", -38f, 0f, 9f, 6f),
            new Room("Creek", -14f, 0f, 9f, 6.5f),
            new Room("TwoLights", 11f, 1f, 10f, 6.5f),
            new Room("Thicket", 36f, -1f, 9f, 6f),
            new Room("Camp", 61f, 0f, 11f, 7f),
        };

        static readonly Vector4[] Corridors =
        {
            new Vector4(-55.5f, 0f, -46.5f, 0f),
            new Vector4(-29.5f, 0f, -22.5f, 0f),
            new Vector4(-5.5f, 0f, 1.5f, 1f),
            new Vector4(20.5f, 1f, 27.5f, -1f),
            new Vector4(44.5f, -1f, 50.5f, 0f),
        };
        const float CorridorHalf = 1.9f, RoomCorner = 2.6f;

        // Hitos.
        static readonly Vector3 CabinCenter = new Vector3(-63f, 0f, 0.5f);
        const float CabinYaw = 0f;
        static readonly Vector3 Meadow = new Vector3(11f, 0f, 1f);       // sala de los dos reflectores
        const float MeadowRadius = 6f;
        static readonly Vector3 Camp = new Vector3(56f, 0f, 3.4f);
        static readonly Vector3 TruckPos = new Vector3(67.4f, 0f, -1.2f);
        const float RoadX = 69.5f, RoadHalf = 2.2f;
        const float BridgeZ = -1f, LogZ = 999f;                          // sin cruce por tronco en v3
        static float RavineX(float z) => -14f + 0.6f * Mathf.Sin(z * 0.2f);

        static readonly Vector3[] MainTrail =
        {
            new Vector3(-62, 0, 0), new Vector3(-55, 0, 0), new Vector3(-47, 0, 0), new Vector3(-38, 0, 0), new Vector3(-29, 0, 0),
            new Vector3(-22.5f, 0, 0), new Vector3(-19.5f, 0, -1), new Vector3(-8.5f, 0, -1), new Vector3(-5.5f, 0, 0),
            new Vector3(1.5f, 0, 1), new Vector3(11, 0, 1), new Vector3(20.5f, 0, 1), new Vector3(27.5f, 0, -1),
            new Vector3(31, 0, -4.5f), new Vector3(41, 0, -4.5f), new Vector3(44.5f, 0, -1), new Vector3(50.5f, 0, 0),
            new Vector3(56, 0, -1.5f), new Vector3(65.5f, 0, -1.2f)
        };
        static readonly Vector3[] NorthTrail = new Vector3[0];

        // ------------------------------------------------------------------ Diseño de encuentros (datos)

        /// <summary>Pastizales escondite: x, z, radio. Agachado dentro = invisible.</summary>
        static readonly Vector3[] HidePatches =
        {
            // Refugio: práctica junto a la salida.
            new Vector3(-57.2f, -2.4f, 1.5f),
            // Sala 1 (un reflector en lazo rectangular): sobre la primera línea, isla central, sobre la segunda.
            new Vector3(-42f, -0.4f, 1.9f), new Vector3(-38f, 1.4f, 1.6f), new Vector3(-34f, -0.8f, 1.9f),
            // Sala 2 (arroyo + Lurker en la otra orilla): mirador antes del puente, a medio patrullaje, salida.
            new Vector3(-20.6f, -3.3f, 1.8f), new Vector3(-7f, -4.1f, 1.5f), new Vector3(-3.4f, 3.3f, 1.5f),
            // Sala 3 (dos reflectores cruzados): islas en zigzag a lo largo del camino.
            new Vector3(4.2f, 0.6f, 1.6f), new Vector3(8f, -2.7f, 1.6f), new Vector3(11.2f, 3.6f, 1.5f),
            new Vector3(14.4f, -2.5f, 1.6f), new Vector3(17.8f, 2.8f, 1.6f), new Vector3(20.6f, 0.4f, 1.4f),
            // Sala 4 (Lurker en lazo + reflector en la salida).
            new Vector3(28.6f, -2.6f, 1.5f), new Vector3(36f, 1.3f, 1.3f), new Vector3(43f, 1.4f, 1.5f),
            // Sala 5 (campamento, camino, camioneta y clímax).
            new Vector3(52f, -4.6f, 1.7f), new Vector3(57.4f, -3.3f, 1.5f), new Vector3(62.4f, -0.7f, 1.5f),
            new Vector3(64.6f, 4.4f, 1.5f), new Vector3(60.3f, -5.7f, 1.4f), new Vector3(53.2f, 1.5f, 1.4f),
        };

        /// <summary>Rocas de cobertura (tapan la vista de los Lurker agachado): x, z, escala.</summary>
        static readonly Vector3[] CoverRocks =
        {
            new Vector3(-10.8f, 2.6f, 1.05f), new Vector3(-10.4f, -4.4f, 1f), new Vector3(-18.2f, 3.4f, 0.95f),
            new Vector3(35f, -1f, 1.15f), new Vector3(37.1f, -0.4f, 1.05f), new Vector3(36.2f, -2.4f, 1.1f),
            new Vector3(29.6f, 2.6f, 0.95f), new Vector3(42.4f, -2.6f, 1f),
            new Vector3(50.8f, 4.6f, 1f), new Vector3(58.6f, -1.6f, 0.95f),
        };

        /// <summary>Troncos caídos de cobertura: x, z, yaw, largo.</summary>
        static readonly Vector4[] CoverLogs =
        {
            new Vector4(-50.5f, 1.2f, 80f, 3.2f), new Vector4(-45.6f, 3.4f, 20f, 3.6f), new Vector4(-30.8f, -3.6f, -30f, 3.4f),
            new Vector4(-4.4f, -1.6f, 15f, 3f), new Vector4(6.2f, 4.6f, 60f, 3.4f), new Vector4(15.8f, -5f, -15f, 3.6f),
            new Vector4(32.6f, -1.2f, 85f, 3f), new Vector4(55f, -2.2f, 10f, 3.2f), new Vector4(65.6f, 2.4f, 80f, 3f),
        };

        struct SearchlightDesign
        {
            public string name; public Vector2[] route; public bool loop; public float speed, pause, phase; public bool startActive;
            public SearchlightDesign(string n, Vector2[] r, bool l, float s, float p, float ph, bool active = true)
            { name = n; route = r; loop = l; speed = s; pause = p; phase = ph; startActive = active; }
        }

        static readonly SearchlightDesign[] Searchlights =
        {
            // Sala 1: lazo rectangular lento. Las líneas verticales cruzan el camino; el centro queda libre.
            new SearchlightDesign("S1_FirstLight", new[] { new Vector2(-42f, 4.6f), new Vector2(-42f, -4.6f), new Vector2(-34f, -4.6f), new Vector2(-34f, 4.6f) }, true, 2.5f, 0.5f, 0f),
            // Sala 3: uno barre el camino de punta a punta, el otro lo cruza por el centro.
            new SearchlightDesign("S2_Sweep", new[] { new Vector2(3f, 0.6f), new Vector2(19.5f, 0.6f) }, false, 2.8f, 0.6f, 0f),
            new SearchlightDesign("S3_Cross", new[] { new Vector2(11.2f, -4.8f), new Vector2(11.2f, 6.6f) }, false, 2.2f, 0.8f, 3f),
            // Sala 4: guarda la salida.
            new SearchlightDesign("S4_Gate", new[] { new Vector2(44.2f, -5.6f), new Vector2(44.2f, 3.6f) }, false, 2.6f, 0.5f, 1.5f),
            // Sala 5: lazo alrededor de la camioneta; dos más se encienden con la radio.
            new SearchlightDesign("S5_Truck", new[] { new Vector2(63.6f, 3.8f), new Vector2(63.6f, -5.2f), new Vector2(71.2f, -5.2f), new Vector2(71.2f, 3.8f) }, true, 2.4f, 0.6f, 0f),
            new SearchlightDesign("S6_Climax", new[] { new Vector2(55.5f, -4.2f), new Vector2(70.5f, -4.2f) }, false, 3f, 0.4f, 0f, false),
            new SearchlightDesign("S7_Climax", new[] { new Vector2(60.4f, -6.2f), new Vector2(60.4f, 5.6f) }, false, 2.6f, 0.4f, 2f, false),
        };

        /// <summary>Patrullas: nombre del Lurker y puntos (x, z). Cada parada mira hacia afuera de su zona.</summary>
        static readonly (string name, Vector2[] pts)[] Patrols =
        {
            ("Lurker", new[] { new Vector2(-8.3f, 4.6f), new Vector2(-8.3f, -4.8f), new Vector2(-5.4f, -4.8f), new Vector2(-5.4f, 4.6f) }),
            ("Lurker_B", new[] { new Vector2(31.4f, 2.9f), new Vector2(40.6f, 2.9f), new Vector2(40.6f, -4.9f), new Vector2(31.4f, -4.9f) }),
            ("Lurker_C", new[] { new Vector2(52.4f, 5.4f), new Vector2(59.6f, 5.8f), new Vector2(60f, 0.4f), new Vector2(53f, -0.6f) }),
            ("Lurker_D", new[] { new Vector2(71.4f, 5.6f), new Vector2(71.4f, -5.6f) }),
        };

        // ------------------------------------------------------------------ Campo de distancia

        static float RoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - (half - Vector2.one * r);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Distancia con signo al suelo caminable (negativa dentro de salas y pasillos).</summary>
        static float Walk(Vector3 w)
        {
            var p = new Vector2(w.x, w.z);
            float d = float.MaxValue;
            foreach (var r in Rooms) d = Mathf.Min(d, RoundBox(p, r.c, r.half, RoomCorner));
            foreach (var c in Corridors) d = Mathf.Min(d, Segment(p, new Vector2(c.x, c.y), new Vector2(c.z, c.w)) - CorridorHalf);
            // Borde orgánico (no rectángulos perfectos).
            d += (Mathf.PerlinNoise(w.x * 0.22f + 4f, w.z * 0.22f + 9f) - 0.5f) * 1.1f;
            return d;
        }

        /// <summary>Metros hacia el norte (hacia el fondo de la pantalla) hasta tocar suelo caminable; infinito si no hay.</summary>
        static float SouthClearance(Vector3 p, float lateral)
        {
            for (float s = 0f; s <= 20f; s += 0.4f)
            {
                if (Walk(new Vector3(p.x, 0f, p.z + s)) < 0f) return s;
                if (lateral > 0f && (Walk(new Vector3(p.x - lateral, 0f, p.z + s)) < 0f || Walk(new Vector3(p.x + lateral, 0f, p.z + s)) < 0f)) return s;
            }
            return float.PositiveInfinity;
        }

        /// <summary>Altura máxima (m sobre el piso de las salas) que puede tener algo aquí sin tapar suelo caminable.</summary>
        static float MaxHeightAt(Vector3 p, float lateral) => OcclusionSlope * SouthClearance(p, lateral) - 0.5f;

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

        static float DistanceToTrails(Vector3 w) => DistanceToPath(w, MainTrail);

        static float HeightAt(Vector3 w)
        {
            float d = Walk(w);
            float noise = (Mathf.PerlinNoise(w.x * 0.06f + 7f, w.z * 0.06f + 3f) - 0.5f);
            float h;
            if (d <= 0f)
            {
                // Piso de las salas casi plano; el sendero del todo plano.
                h = noise * 0.35f * Smooth(1.2f, 3f, DistanceToTrails(w)) * Smooth(0f, -2f, d);
            }
            else
            {
                float s = SouthClearance(w, 0f);
                bool south = !float.IsInfinity(s);
                float wall = Smooth(0f, 0.9f, d);
                if (south)
                {
                    // Lado de la cámara: cornisa baja y luego terrazas que suben solo lo que la regla permite.
                    float allowed = Mathf.Max(0.9f, OcclusionSlope * s - 0.8f);
                    h = Mathf.Min(1.0f * wall + 2.2f * Smooth(4.5f, 6.5f, s) + 2.2f * Smooth(9f, 11f, s) + noise * 0.6f * Smooth(2f, 5f, d), allowed);
                }
                else
                {
                    // Fondo y laterales: acantilado alto con bosque encima.
                    h = 4.6f * wall + 2.4f * Smooth(5f, 9f, d) + noise * 1.6f * Smooth(2f, 6f, d);
                }
            }

            // Arroyo: barranco que atraviesa la sala 2 y sigue por las paredes.
            float dx = Mathf.Abs(w.x - RavineX(w.z));
            h = Mathf.Lerp(h, -2.7f, Smooth(3.7f, 2.2f, dx));
            return h;
        }

        /// <summary>Lugares ocupados por el diseño: ahí no se dispersa nada.</summary>
        static bool Reserved(Vector3 p, System.Collections.Generic.List<Vector3> blockers, float margin)
        {
            if (Walk(p) < margin) return true;
            if (Mathf.Abs(p.x - RavineX(p.z)) < 4.5f) return true;
            foreach (var b in blockers) if ((Flat(b) - Flat(p)).sqrMagnitude < 9f) return true;
            return false;
        }
    }
}
