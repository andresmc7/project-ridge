using System.Collections.Generic;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Árboles y arbustos estilo diorama (Echoes of Wisdom): copas en capas escalonadas cubiertas de "escamas" de
    /// hoja que se solapan como tejas, con la punta curvada hacia afuera y una nervadura central que parte la luz
    /// en dos facetas. Tronco corto y grueso con raíces en abanico. Todo con color por vértice (albedo y oclusión en
    /// alfa) para un solo material (FoliageWind con _UseVertexColor). El pivote queda en la base (viento por altura).
    /// </summary>
    public static class StylizedTreeGenerator
    {
        public struct Palette
        {
            public Color leafDark, leafLight, core, barkDark, barkLight;
        }

        public struct Shape
        {
            public int tiers;
            public float height;        // altura total
            public float trunkHeight;   // tronco visible bajo la primera capa
            public float trunkRadius;
            public float baseRadius;    // radio de la capa de abajo
            public float topRadius;     // radio de la capa de arriba
            public bool round;          // copa redonda (frondoso) en vez de capas cónicas
            public float leafLength;
            public float leafWidth;
        }

        class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Color> c = new List<Color>();
            public readonly List<int> t = new List<int>();

            public int Add(Vector3 p, Vector3 normal, Color color)
            {
                v.Add(p); n.Add(normal); c.Add(color);
                return v.Count - 1;
            }

            public void Tri(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetColors(c);
                m.SetTriangles(t, 0);
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }

        static float Hash(int a, int b) => Mathf.Abs(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f) % 1f;

        /// <summary>Malla completa (tronco + copa) para un árbol. Sin bushes: trunkHeight 0 y tiers 1.</summary>
        public static Mesh Build(string name, Shape s, Palette pal, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var b = new Builder();

            if (s.trunkHeight > 0f) Trunk(b, s, pal, rng);

            float canopyBottom = s.trunkHeight;
            float canopyHeight = s.height - canopyBottom;
            if (s.round)
            {
                // Frondoso: 1–2 esferas aplastadas que se solapan, la de arriba más pequeña.
                for (int k = 0; k < s.tiers; k++)
                {
                    float f = s.tiers == 1 ? 0f : k / (float)(s.tiers - 1);
                    float r = Mathf.Lerp(s.baseRadius, s.topRadius, f);
                    float squash = 0.82f;
                    float cy = canopyBottom + r * squash * 0.9f + f * (canopyHeight - 2f * r * squash * 0.9f);
                    var center = new Vector3(R(-0.15f, 0.15f) * r, cy, R(-0.15f, 0.15f) * r);
                    Dome(b, center, r, squash, s, pal, rng, k == s.tiers - 1);
                }
            }
            else
            {
                // Conífera: capas cónicas en escalera; cada una mete su cima dentro de la siguiente.
                float tierStep = canopyHeight / (s.tiers + 0.6f);
                for (int k = 0; k < s.tiers; k++)
                {
                    float f = s.tiers == 1 ? 0f : k / (float)(s.tiers - 1);
                    float r = Mathf.Lerp(s.baseRadius, s.topRadius, f);
                    float yBottom = canopyBottom + k * tierStep;
                    float th = k == s.tiers - 1 ? s.height - yBottom : tierStep * 1.65f;
                    Skirt(b, new Vector3(0f, yBottom, 0f), r, th, s, pal, rng, k == s.tiers - 1);
                }
            }
            return b.ToMesh(name);
        }

        // ------------------------------------------------------------------ Tronco con raíces

        static void Trunk(Builder b, Shape s, Palette pal, System.Random rng)
        {
            const int sides = 12;
            const int rings = 7;
            float top = s.trunkHeight + (s.height - s.trunkHeight) * 0.45f; // sube dentro de la copa
            int lobes = 5;
            float lobePhase = (float)rng.NextDouble() * Mathf.PI * 2f;
            int start = b.v.Count;
            for (int r = 0; r <= rings; r++)
            {
                float u = r / (float)rings;
                float y = Mathf.Lerp(-0.15f, top, u * u);
                float flare = Mathf.Exp(-Mathf.Max(0f, y) * 3.2f);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float lobe = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a * lobes + lobePhase)), 3f);
                    float radius = s.trunkRadius * (1f - 0.35f * u) * (1f + 0.55f * flare + 1.1f * lobe * flare);
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    // Vetas: franjas verticales más oscuras (corteza estilizada), base algo más oscura (tierra).
                    float groove = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 4.5f + Mathf.Sin(y * 2f) * 0.6f)), 6f);
                    var col = Color.Lerp(pal.barkLight, pal.barkDark, 0.25f + 0.6f * groove);
                    col.a = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(y / 0.8f)) * (1f - 0.35f * groove);
                    b.Add(dir * radius + Vector3.up * y, (dir + Vector3.up * 0.15f * flare).normalized, col);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < sides; i++)
                {
                    int a0 = start + r * (sides + 1) + i, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                    b.Tri(a0, b0, a1);
                    b.Tri(a1, b0, b1);
                }
        }

        // ------------------------------------------------------------------ Capas

        /// <summary>Capa cónica: núcleo liso oscuro + filas de escamas que cuelgan hacia el borde.</summary>
        static void Skirt(Builder b, Vector3 bottomCenter, float radius, float height, Shape s, Palette pal, System.Random rng, bool isTop)
        {
            // Perfil: u = 0 en la cima, 1 en el borde de abajo. Domo convexo (no un cono recto).
            Vector3 P(float u, float a)
            {
                float r = radius * Mathf.Pow(Mathf.Sin(u * Mathf.PI * 0.5f), isTop ? 1.05f : 0.75f);
                float y = bottomCenter.y + height * (1f - Mathf.Pow(u, 1.25f));
                return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            Vector3 sphereCenter = bottomCenter + Vector3.up * height * 0.35f;
            Core(b, P, sphereCenter, pal, 0.06f);
            Shingles(b, P, sphereCenter, radius, height, s, pal, rng, 0.06f);
        }

        /// <summary>Copa redonda: esfera aplastada; las escamas bajan por los meridianos.</summary>
        static void Dome(Builder b, Vector3 center, float radius, float squash, Shape s, Palette pal, System.Random rng, bool isTop)
        {
            Vector3 P(float u, float a)
            {
                float phi = Mathf.Lerp(0.02f, 0.86f, u) * Mathf.PI; // de la cima a un poco bajo el ecuador
                return center + new Vector3(Mathf.Cos(a) * Mathf.Sin(phi) * radius, Mathf.Cos(phi) * radius * squash, Mathf.Sin(a) * Mathf.Sin(phi) * radius);
            }
            Core(b, P, center, pal, 0.97f);
            Shingles(b, P, center, radius, radius * squash * 2f, s, pal, rng, 0.97f);
        }

        /// <summary>Superficie lisa y oscura bajo las escamas: tapa los huecos (nunca se ve el cielo a través).</summary>
        static void Core(Builder b, System.Func<float, float, Vector3> P, Vector3 center, Palette pal, float uMax)
        {
            const int sides = 14, rings = 6;
            int start = b.v.Count;
            for (int r = 0; r <= rings; r++)
            {
                float u = Mathf.Lerp(0.0f, 1f, r / (float)rings) * (uMax > 0.5f ? 1f : 1f);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    var p = P(u * 0.97f, a);
                    var col = pal.core;
                    col.a = 0.45f;
                    b.Add(p, (p - center).normalized, col);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < sides; i++)
                {
                    int a0 = start + r * (sides + 1) + i, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                    b.Tri(a0, a1, b0);
                    b.Tri(a1, b1, b0);
                }
        }

        static void Shingles(Builder b, System.Func<float, float, Vector3> P, Vector3 center, float radius, float height, Shape s, Palette pal, System.Random rng, float _)
        {
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            float slant = Mathf.Sqrt(radius * radius + height * height);
            int rows = Mathf.Max(2, Mathf.CeilToInt(slant / (s.leafLength * 0.5f)));
            for (int row = 0; row < rows; row++)
            {
                // Las filas de abajo cuelgan por fuera del borde (festón visible).
                float uTip = Mathf.Lerp(0.3f, 1.06f, (row + 1) / (float)rows);
                float ring = P(Mathf.Min(uTip, 1f), 0f).x - center.x;
                ring = Mathf.Max(0.25f, Mathf.Abs(new Vector2(P(Mathf.Min(uTip, 1f), 0f).x - center.x, P(Mathf.Min(uTip, 1f), 0f).z - center.z).magnitude));
                int count = Mathf.Max(4, Mathf.CeilToInt(2f * Mathf.PI * ring / (s.leafWidth * 0.62f)));
                float offset = R(0f, Mathf.PI * 2f);
                float shade = Mathf.Lerp(0.62f, 1f, (row + 1) / (float)rows); // la fila de arriba queda bajo la capa superior
                for (int i = 0; i < count; i++)
                {
                    float a = offset + (i + (row % 2) * 0.5f + R(-0.18f, 0.18f)) / count * Mathf.PI * 2f;
                    Leaf(b, P, center, a, uTip, s, pal, rng, shade);
                }
            }
        }

        /// <summary>Una escama: punta redondeada que cuelga siguiendo la superficie, curvada hacia afuera, con nervadura.</summary>
        static void Leaf(Builder b, System.Func<float, float, Vector3> P, Vector3 center, float a, float uTip, Shape s, Palette pal, System.Random rng, float shade)
        {
            float R(float x, float y) => x + (float)rng.NextDouble() * (y - x);
            float len = s.leafLength * R(0.85f, 1.15f);
            float wid = s.leafWidth * R(0.85f, 1.15f);

            float uClamped = Mathf.Min(uTip, 1f);
            Vector3 tipOnSurface = P(uClamped, a);
            Vector3 down = (P(Mathf.Min(uClamped + 0.02f, 1f), a) - P(Mathf.Max(uClamped - 0.02f, 0f), a));
            if (down.sqrMagnitude < 1e-6f) down = Vector3.down;
            down.Normalize();
            if (uTip > 1f) down = Vector3.Slerp(down, Vector3.down, (uTip - 1f) * 4f);
            Vector3 outward = (tipOnSurface - center); outward.y *= 0.35f; outward.Normalize();
            Vector3 normal = Vector3.ProjectOnPlane(outward, down).normalized;
            Vector3 side = Vector3.Cross(normal, down).normalized;
            Vector3 tip = tipOnSurface + normal * 0.05f;
            Vector3 baseP = tip - down * len;

            // Color por escama: variación de brillo y algo de tono.
            float bright = R(0.88f, 1.12f);
            float hue = R(-0.04f, 0.04f);
            const int seg = 4;
            int[] left = new int[seg + 1], mid = new int[seg + 1], right = new int[seg + 1];
            int[] mid2 = new int[seg + 1];
            for (int k = 0; k <= seg; k++)
            {
                float tt = k / (float)seg;
                // Ancho: estrecho en la base, ancho a 2/3, punta redondeada.
                float w = wid * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 1f, tt)) * Mathf.Lerp(0.55f, 1f, tt);
                if (k == seg) w = wid * 0.12f;
                float curl = len * 0.22f * tt * tt;            // la punta se levanta
                Vector3 axis = baseP + down * (len * tt) + normal * curl;
                Vector3 ridge = normal * wid * 0.12f;           // nervadura central levantada
                var col = Color.Lerp(pal.leafDark, pal.leafLight, Mathf.Pow(tt, 1.4f)) * bright;
                col.r += hue; col.b -= hue * 0.5f;
                col.a = Mathf.Lerp(0.55f, 1f, tt) * shade;
                Vector3 sphereN = (axis - center).normalized;
                Vector3 nl = (normal * 0.55f - side * 0.45f + sphereN * 0.6f).normalized;
                Vector3 nr = (normal * 0.55f + side * 0.45f + sphereN * 0.6f).normalized;
                left[k] = b.Add(axis - side * w, nl, col);
                mid[k] = b.Add(axis + ridge, nl, col * 1.05f);
                mid2[k] = b.Add(axis + ridge, nr, col * 1.05f);
                right[k] = b.Add(axis + side * w, nr, col);
            }
            for (int k = 0; k < seg; k++)
            {
                b.Tri(left[k], mid[k], left[k + 1]);
                b.Tri(mid[k], mid[k + 1], left[k + 1]);
                b.Tri(mid2[k], right[k], mid2[k + 1]);
                b.Tri(right[k], right[k + 1], mid2[k + 1]);
            }
        }
    }
}
