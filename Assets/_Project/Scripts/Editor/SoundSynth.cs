using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Sintetiza todo el audio del PoC a WAV (mono, 22.05 kHz) dentro del proyecto: pasos, golpes, bengalas, voces del
    /// Lurker (FM con distorsión y reverberación), ambiente (viento, grillos), música adaptativa (drone y persecución),
    /// golpes de alerta, latido, radio. Sin assets de terceros. Regenerable: mismos parámetros → mismos archivos.
    /// </summary>
    public static class SoundSynth
    {
        const int SR = 22050;
        const string OutDir = "Assets/_Project/Audio/Generated";
        const string BankPath = "Assets/_Project/Data/AudioBank.asset";

        static System.Random rng = new System.Random(1);
        static float Noise() => (float)rng.NextDouble() * 2f - 1f;
        static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];
        const float TAU = Mathf.PI * 2f;

        // ------------------------------------------------------------------ DSP

        struct LP
        {
            float y;
            public float Process(float x, float cutoff)
            {
                float a = 1f - Mathf.Exp(-TAU * Mathf.Clamp(cutoff, 10f, SR * 0.45f) / SR);
                y += a * (x - y);
                return y;
            }
        }

        /// <summary>Biquad pasa-banda (RBJ, ganancia de pico constante).</summary>
        struct BP
        {
            float x1, x2, y1, y2;
            public float Process(float x, float freq, float q)
            {
                float w = TAU * Mathf.Clamp(freq, 20f, SR * 0.45f) / SR;
                float alpha = Mathf.Sin(w) / (2f * q);
                float b0 = alpha, b2 = -alpha, a0 = 1f + alpha, a1 = -2f * Mathf.Cos(w), a2 = 1f - alpha;
                float y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay));
        static float Bell(float t, float len) => Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
        static float Drive(float x, float k) => (float)System.Math.Tanh(x * k) / (float)System.Math.Tanh(k);

        static void Normalize(float[] b, float peak = 0.9f)
        {
            float m = 0.0001f;
            foreach (var s in b) m = Mathf.Max(m, Mathf.Abs(s));
            for (int i = 0; i < b.Length; i++) b[i] *= peak / m;
        }

        static void FadeEdges(float[] b, float fadeIn = 0.002f, float fadeOut = 0.01f)
        {
            int fi = Mathf.Min(b.Length, (int)(fadeIn * SR)), fo = Mathf.Min(b.Length, (int)(fadeOut * SR));
            for (int i = 0; i < fi; i++) b[i] *= i / (float)fi;
            for (int i = 0; i < fo; i++) b[b.Length - 1 - i] *= i / (float)fo;
        }

        /// <summary>Reverberación de Schroeder (4 combs + 2 allpass). mix 0–1.</summary>
        static float[] Reverb(float[] dry, float mix, float roomSeconds, float tail)
        {
            var wet = new float[dry.Length + (int)(tail * SR)];
            int[] combs = { (int)(0.0297f * SR * roomSeconds), (int)(0.0371f * SR * roomSeconds), (int)(0.0411f * SR * roomSeconds), (int)(0.0437f * SR * roomSeconds) };
            float fb = 0.82f;
            foreach (int d in combs)
            {
                var line = new float[d];
                int idx = 0;
                float lp = 0f;
                for (int i = 0; i < wet.Length; i++)
                {
                    float x = i < dry.Length ? dry[i] : 0f;
                    float y = line[idx];
                    lp += 0.4f * (y - lp);
                    line[idx] = x + lp * fb;
                    idx = (idx + 1) % d;
                    wet[i] += y * 0.25f;
                }
            }
            foreach (int d in new[] { (int)(0.005f * SR), (int)(0.0017f * SR) })
            {
                var line = new float[d];
                int idx = 0;
                for (int i = 0; i < wet.Length; i++)
                {
                    float buf = line[idx];
                    float x = wet[i];
                    float y = -x + buf;
                    line[idx] = x + buf * 0.5f;
                    idx = (idx + 1) % d;
                    wet[i] = y;
                }
            }
            for (int i = 0; i < wet.Length; i++) wet[i] = wet[i] * mix + (i < dry.Length ? dry[i] : 0f) * (1f - mix * 0.5f);
            return wet;
        }

        /// <summary>Bucle sin costura: mezcla el final sobre el principio.</summary>
        static float[] MakeLoop(float[] b, float crossfade)
        {
            int x = (int)(crossfade * SR);
            int n = b.Length - x;
            var outB = new float[n];
            for (int i = 0; i < n; i++) outB[i] = b[i];
            for (int i = 0; i < x; i++)
            {
                float t = i / (float)x;
                outB[i] = b[i] * t + b[n + i] * (1f - t);
            }
            return outB;
        }

        static void Mix(float[] dst, float[] src, int offset, float gain)
        {
            for (int i = 0; i < src.Length && offset + i < dst.Length; i++)
                if (offset + i >= 0) dst[offset + i] += src[i] * gain;
        }

        // ------------------------------------------------------------------ Bloques

        static float[] Thump(float seconds, float f0, float f1, float decay, float noiseAmt, float noiseCut)
        {
            var b = Buf(seconds);
            float ph = 0f;
            var lp = new LP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(f1, f0, Mathf.Exp(-t / (decay * 0.5f)));
                ph += TAU * f / SR;
                b[i] = Mathf.Sin(ph) * Env(t, 0.002f, decay) + lp.Process(Noise(), noiseCut) * noiseAmt * Env(t, 0.001f, decay * 0.4f);
            }
            return b;
        }

        static float[] Clicks(float seconds, int count, float freq, float q, float spread)
        {
            var b = Buf(seconds);
            var bp = new BP();
            float f = freq * Rand(0.9f, 1.1f);
            var impulses = new float[b.Length];
            for (int c = 0; c < count; c++)
            {
                int at = (int)(Rand(0f, spread) * SR);
                if (at < impulses.Length) impulses[at] = Rand(0.6f, 1f) * (rng.NextDouble() < 0.5 ? 1f : -1f);
            }
            for (int i = 0; i < b.Length; i++) b[i] = bp.Process(impulses[i] * 30f + Noise() * 0.02f, f, q);
            return b;
        }

        /// <summary>Chillido FM: portadora con modulador inarmónico, glissando, trémolo y distorsión.</summary>
        static float[] Screech(float seconds, float carrier, float modRatio, float index, float glideUp, float glideDown, float tremolo, float drive, float noise)
        {
            var b = Buf(seconds);
            float pc = 0f, pm = 0f, pc2 = 0f;
            var bp = new BP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR, u = t / seconds;
                float glide = u < 0.3f ? Mathf.Lerp(1f, glideUp, u / 0.3f) : Mathf.Lerp(glideUp, glideDown, (u - 0.3f) / 0.7f);
                float f = carrier * glide * (1f + 0.02f * Mathf.Sin(TAU * 7f * t));
                pm += TAU * f * modRatio / SR;
                float idx = index * (0.5f + 0.5f * Mathf.Sin(u * Mathf.PI));
                pc += TAU * f / SR + idx * Mathf.Sin(pm) * TAU * f * modRatio / SR * 0.15f;
                pc2 += TAU * f * 1.37f / SR;
                float trem = 1f - 0.45f * (0.5f + 0.5f * Mathf.Sin(TAU * tremolo * t));
                float env = Mathf.Min(1f, t / 0.04f) * Mathf.Pow(1f - u, 0.6f);
                float s = Mathf.Sin(pc) + 0.4f * Mathf.Sin(pc2 + Mathf.Sin(pm) * 2f);
                s += bp.Process(Noise(), f * 1.5f, 3f) * noise;
                b[i] = Drive(s * trem * env, drive);
            }
            return b;
        }

        /// <summary>Murmullo "vocal": diente de sierra con contorno de sílabas por dos formantes.</summary>
        static float[] Formant(float seconds, float pitch, float pitchEnd, float f1, float f2, float breath)
        {
            var b = Buf(seconds);
            float ph = 0f;
            var a = new BP();
            var c = new BP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR, u = t / seconds;
                float f = Mathf.Lerp(pitch, pitchEnd, u) * (1f + 0.01f * Mathf.Sin(TAU * 5f * t));
                ph += f / SR;
                float saw = (ph % 1f) * 2f - 1f;
                float src = saw + Noise() * breath;
                b[i] = (a.Process(src, f1, 5f) + 0.7f * c.Process(src, f2, 7f)) * Bell(t, seconds);
            }
            return b;
        }

        // ------------------------------------------------------------------ Sonidos

        static float[] StepDirt(int v)
        {
            var b = Buf(0.18f);
            var lp = new LP();
            float cut = 1400f + v * 300f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float crunch = rng.NextDouble() < 0.02 ? Noise() * 2f : 0f;
                b[i] = lp.Process(Noise() + crunch, cut) * Env(t, 0.004f, 0.045f + v * 0.008f);
            }
            Mix(b, Thump(0.12f, 85f + v * 10f, 60f, 0.03f, 0f, 100f), 0, 0.5f);
            FadeEdges(b);
            Normalize(b, 0.8f);
            return b;
        }

        static float[] StepWood(int v)
        {
            var b = Thump(0.25f, 160f + v * 15f, 110f, 0.05f, 0.3f, 900f);
            var bp = new BP();
            for (int i = 0; i < b.Length; i++)
                b[i] += bp.Process(Noise(), 420f + v * 40f, 6f) * Env(i / (float)SR, 0.002f, 0.06f) * 2.5f;
            FadeEdges(b);
            Normalize(b, 0.8f);
            return b;
        }

        static float[] Whoosh(float seconds, float f0, float fPeak, float f1)
        {
            var b = Buf(seconds);
            var bp = new BP();
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)b.Length;
                float f = u < 0.5f ? Mathf.Lerp(f0, fPeak, u * 2f) : Mathf.Lerp(fPeak, f1, (u - 0.5f) * 2f);
                b[i] = bp.Process(Noise(), f, 1.2f) * Bell(u, 1f);
            }
            Normalize(b, 0.8f);
            return b;
        }

        static float[] ClubHit()
        {
            var b = Thump(0.35f, 75f, 45f, 0.09f, 0.9f, 2500f);
            var bp = new BP();
            for (int i = 0; i < b.Length; i++) b[i] += bp.Process(Noise(), 320f, 2f) * Env(i / (float)SR, 0.001f, 0.05f) * 3f;
            Mix(b, Clicks(0.1f, 3, 2400f, 4f, 0.02f), 0, 0.5f);
            for (int i = 0; i < b.Length; i++) b[i] = Drive(b[i], 1.8f);
            Normalize(b);
            return Reverb(b, 0.15f, 0.8f, 0.3f);
        }

        static float[] ClubBreak()
        {
            var b = Buf(0.9f);
            Mix(b, Clicks(0.4f, 22, 3200f, 3f, 0.12f), 0, 1f);
            Mix(b, Thump(0.3f, 120f, 70f, 0.06f, 0.8f, 3000f), 0, 0.8f);
            for (int k = 0; k < 4; k++) Mix(b, Thump(0.15f, 300f + k * 70f, 200f, 0.02f, 0.4f, 2000f), (int)((0.35f + k * 0.11f) * SR), 0.35f);
            Normalize(b);
            return Reverb(b, 0.2f, 0.8f, 0.4f);
        }

        static float[] FlareShot()
        {
            var b = Buf(1.4f);
            Mix(b, Thump(0.4f, 60f, 40f, 0.12f, 1.2f, 5000f), 0, 1f);
            var w = Whoosh(0.6f, 800f, 3000f, 1800f);
            Mix(b, w, (int)(0.03f * SR), 0.5f);
            var lp = new LP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[i] += (Noise() - lp.Process(Noise(), 2000f)) * 0.25f * Env(t - 0.05f, 0.05f, 0.4f) * (t > 0.05f ? 1f : 0f);
                b[i] = Drive(b[i], 1.5f);
            }
            Normalize(b);
            return Reverb(b, 0.3f, 1.2f, 0.8f);
        }

        static float[] FlareBurnLoop()
        {
            var b = Buf(3.5f);
            var lp = new LP();
            var lp2 = new LP();
            float amp = 1f;
            for (int i = 0; i < b.Length; i++)
            {
                amp += (Rand(0.6f, 1.2f) - amp) * 0.0005f;
                float n = Noise();
                float hiss = n - lp.Process(n, 1800f);
                float crackle = rng.NextDouble() < 0.0015 ? Noise() * 6f : 0f;
                b[i] = hiss * amp * 0.5f + lp2.Process(crackle, 3000f);
            }
            Normalize(b, 0.7f);
            return MakeLoop(b, 0.4f);
        }

        static float[] LurkerStep(int v)
        {
            var b = Thump(0.22f, 55f + v * 6f, 40f, 0.06f, 0.4f, 600f);
            Mix(b, Clicks(0.08f, 2 + v, 3500f, 5f, 0.03f), (int)(0.01f * SR), 0.6f);
            FadeEdges(b);
            Normalize(b, 0.85f);
            return b;
        }

        static float[] LurkerClick(int v)
        {
            var b = Clicks(0.45f, 4 + v * 2, 2600f + v * 400f, 6f, 0.25f + v * 0.04f);
            Mix(b, Clicks(0.3f, 2, 900f, 3f, 0.2f), 0, 0.6f); // crujido húmedo
            FadeEdges(b);
            Normalize(b, 0.85f);
            return Reverb(b, 0.12f, 0.7f, 0.25f);
        }

        static float[] LurkerScreech()
        {
            var b = Screech(1.5f, 820f, 0.137f * 7.3f, 9f, 1.35f, 0.7f, 23f, 3f, 0.6f);
            var b2 = Screech(1.5f, 1210f, 0.71f, 6f, 1.2f, 0.85f, 31f, 2.5f, 0.3f);
            Mix(b, b2, (int)(0.05f * SR), 0.6f);
            Normalize(b);
            return Reverb(b, 0.35f, 1.3f, 1.0f);
        }

        static float[] LurkerAttack()
        {
            var b = Screech(0.7f, 1100f, 1.31f, 11f, 1.5f, 0.9f, 37f, 4f, 0.8f);
            Mix(b, Thump(0.3f, 70f, 45f, 0.08f, 1f, 3000f), 0, 0.7f);
            Normalize(b);
            return Reverb(b, 0.25f, 1f, 0.6f);
        }

        static float[] LurkerHurt()
        {
            var b = Screech(0.55f, 1400f, 0.77f, 7f, 1.1f, 0.55f, 41f, 3f, 0.5f);
            Mix(b, Clicks(0.3f, 5, 2800f, 6f, 0.2f), 0, 0.5f);
            Normalize(b);
            return Reverb(b, 0.25f, 1f, 0.5f);
        }

        static float[] LurkerDeath()
        {
            var b = Screech(2.4f, 900f, 0.61f, 10f, 1.2f, 0.35f, 17f, 3.5f, 0.7f);
            var lp = new LP();
            for (int i = (int)(1.4f * SR); i < b.Length; i++)
            {
                float t = i / (float)SR;
                float gurgle = lp.Process(Noise(), 300f) * (0.5f + 0.5f * Mathf.Sin(TAU * 9f * t)) * 1.5f;
                b[i] += gurgle * Mathf.Clamp01((t - 1.4f) * 2f) * (1f - (t - 1.4f) / 1.0f);
            }
            Mix(b, Clicks(1.5f, 12, 2500f, 6f, 1.4f), (int)(0.6f * SR), 0.6f);
            Normalize(b);
            return Reverb(b, 0.35f, 1.3f, 1.2f);
        }

        static float[] LurkerGrowlLoop()
        {
            var b = Buf(4.2f);
            var bp = new BP();
            var bp2 = new BP();
            float sub = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float breath = 0.55f + 0.45f * Mathf.Sin(TAU * t / 1.4f);
                float rasp = bp.Process(Noise() * (1f + 0.8f * Mathf.Sin(TAU * 37f * t)), 420f + 200f * breath, 2.5f);
                float hiss = bp2.Process(Noise(), 2400f, 1.5f) * 0.25f;
                sub += TAU * 46f / SR;
                b[i] = Drive((rasp * 2.2f + hiss) * breath + Mathf.Sin(sub) * 0.25f * (0.6f + 0.4f * Mathf.Sin(TAU * 3f * t)), 2f);
            }
            Normalize(b, 0.8f);
            return MakeLoop(b, 0.6f);
        }

        static float[] Hurt()
        {
            var b = Formant(0.42f, 135f, 95f, 650f, 1100f, 0.4f);
            Mix(b, Thump(0.15f, 90f, 60f, 0.04f, 0.6f, 1500f), 0, 0.6f);
            Normalize(b, 0.85f);
            return b;
        }

        static float[] Death()
        {
            var b = Formant(1.1f, 120f, 70f, 600f, 950f, 0.6f);
            Mix(b, Thump(0.4f, 70f, 40f, 0.12f, 0.8f, 1200f), (int)(0.8f * SR), 0.9f);
            Normalize(b, 0.85f);
            return Reverb(b, 0.2f, 1f, 0.6f);
        }

        static float[] Heal()
        {
            var b = Buf(0.6f);
            var lp = new LP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float n = Noise();
                float rip = (n - lp.Process(n, 1500f)) * (0.5f + 0.5f * Mathf.Sin(TAU * 34f * t)) * Bell(t, 0.35f);
                b[i] = rip;
            }
            Mix(b, Formant(0.5f, 110f, 90f, 500f, 1500f, 2.5f), (int)(0.2f * SR), 0.25f); // suspiro
            Normalize(b, 0.7f);
            return b;
        }

        static float[] Pickup()
        {
            var b = Buf(0.35f);
            var bp = new BP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[i] = bp.Process(Noise(), 2200f, 1.5f) * Bell(t, 0.12f) * 0.8f
                     + (Mathf.Sin(TAU * 1760f * t) + 0.5f * Mathf.Sin(TAU * 2637f * t)) * Env(t - 0.06f, 0.002f, 0.08f) * (t > 0.06f ? 0.25f : 0f);
            }
            Normalize(b, 0.7f);
            return b;
        }

        static float[] Creak(float seconds)
        {
            var b = Buf(seconds);
            float ph = 0f;
            var bp = new BP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float f = 140f + 60f * Mathf.Sin(TAU * 1.3f * t) + Rand(-8f, 8f);
                ph += f / SR;
                float pulses = (ph % 1f) < 0.08f ? 1f : 0f; // fricción a saltos
                b[i] = bp.Process(pulses + Noise() * 0.1f, 900f, 4f) * Bell(t, seconds);
            }
            Normalize(b, 0.7f);
            return b;
        }

        static float[] Craft()
        {
            var b = Buf(1.3f);
            for (int k = 0; k < 4; k++) Mix(b, Thump(0.2f, 220f, 150f, 0.04f, 0.5f, 2500f), (int)((0.1f + k * 0.28f) * SR), 0.8f);
            Mix(b, Creak(0.5f), (int)(0.5f * SR), 0.4f);
            Normalize(b, 0.8f);
            return b;
        }

        static float[] DryFire()
        {
            var b = Clicks(0.15f, 2, 3000f, 4f, 0.06f);
            Normalize(b, 0.7f);
            return b;
        }

        static float[] WindLoop()
        {
            var b = Buf(14f);
            var lp = new LP();
            var lp2 = new LP();
            float gust = 0.5f, target = 0.5f;
            for (int i = 0; i < b.Length; i++)
            {
                if (i % (SR / 2) == 0) target = Rand(0.2f, 1f);
                gust += (target - gust) * 0.00004f;
                float n = Noise();
                float low = lp.Process(n, 180f + 500f * gust);
                float whistle = lp2.Process(n, 900f) - low;
                b[i] = low * (0.5f + gust) + whistle * 0.15f * gust;
            }
            Normalize(b, 0.7f);
            return MakeLoop(b, 2f);
        }

        static float[] ForestLoop()
        {
            var b = Buf(12f);
            // Grillos: trenes de chirridos a ~4 kHz pulsados a 30 Hz.
            for (int insect = 0; insect < 4; insect++)
            {
                float f = 3800f + insect * 330f;
                float period = Rand(0.7f, 1.3f);
                for (float start = Rand(0f, 1f); start < 11.5f; start += period * Rand(0.9f, 1.1f))
                {
                    int s0 = (int)(start * SR), len = (int)(0.22f * SR);
                    for (int i = 0; i < len && s0 + i < b.Length; i++)
                    {
                        float t = i / (float)SR;
                        float pulse = Mathf.Max(0f, Mathf.Sin(TAU * 32f * t));
                        b[s0 + i] += Mathf.Sin(TAU * f * t) * pulse * Bell(t, 0.22f) * 0.12f / (1 + insect * 0.4f);
                    }
                }
            }
            // Un búho lejano.
            for (int h = 0; h < 2; h++)
            {
                int s0 = (int)((6.2f + h * 0.55f) * SR), len = (int)(0.4f * SR);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)SR;
                    b[s0 + i] += Mathf.Sin(TAU * (390f - 25f * t) * t) * Bell(t, 0.4f) * 0.18f;
                }
            }
            var withVerb = Reverb(b, 0.4f, 1.5f, 0f);
            Normalize(withVerb, 0.5f);
            return MakeLoop(withVerb, 1f);
        }

        static float[] MusicAmbient()
        {
            var b = Buf(26f);
            float[] freqs = { 55f, 82.41f, 87.31f, 110f };
            float[] gains = { 0.5f, 0.35f, 0.12f, 0.2f };
            var phases = new float[8];
            var lp = new LP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    for (int d = 0; d < 2; d++)
                    {
                        int p = k * 2 + d;
                        phases[p] += freqs[k] * (d == 0 ? 0.998f : 1.003f) / SR;
                        s += ((phases[p] % 1f) * 2f - 1f) * gains[k] * 0.5f;
                    }
                }
                float cut = 260f + 180f * (0.5f + 0.5f * Mathf.Sin(TAU * t / 13f));
                float pad = lp.Process(s, cut);
                // Armónico alto inquietante que aparece y se va.
                float high = Mathf.Sin(TAU * 880f * t + 3f * Mathf.Sin(TAU * 0.3f * t)) * 0.05f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(TAU * t / 26f * 2f)), 3f);
                b[i] = pad + high;
            }
            var wet = Reverb(b, 0.5f, 2.2f, 0f);
            Normalize(wet, 0.6f);
            return MakeLoop(wet, 3f);
        }

        static float[] MusicChase()
        {
            const float bpm = 124f;
            float beat = 60f / bpm;
            var b = Buf(beat * 16 + 0.5f);
            // Tambores graves sincopados.
            int[] pattern = { 0, 3, 6, 8, 10, 11, 14 };
            foreach (int step in pattern)
            {
                Mix(b, Thump(0.5f, 58f, 38f, 0.16f, 0.5f, 900f), (int)(step * beat * SR), step % 8 == 0 ? 1f : 0.7f);
            }
            // Cuerdas en trémolo disonante (segunda menor) y ruido que crece.
            float p1 = 0f, p2 = 0f;
            var lp = new LP();
            var lpN = new LP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                p1 += 220f / SR; p2 += 233.08f / SR;
                float saw = ((p1 % 1f) * 2f - 1f) + 0.8f * ((p2 % 1f) * 2f - 1f);
                float trem = 0.5f + 0.5f * Mathf.Sin(TAU * 12f * t);
                b[i] += lp.Process(saw, 1400f) * trem * 0.22f;
                b[i] += lpN.Process(Noise(), 3000f) * 0.06f * (t % (beat * 4)) / (beat * 4);
                b[i] = Drive(b[i], 1.3f);
            }
            Normalize(b, 0.75f);
            return MakeLoop(b, 0.5f);
        }

        static float[] AlertSting()
        {
            var b = Buf(2.6f);
            float[] f = { 110f, 116.54f, 155.56f, 164.81f, 233.08f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float swell = t < 0.9f ? Mathf.Pow(t / 0.9f, 2f) : Mathf.Exp(-(t - 0.9f) * 3f);
                float s = 0f;
                for (int k = 0; k < f.Length; k++) s += ((f[k] * t % 1f) * 2f - 1f) * 0.2f;
                b[i] = s * swell * 0.6f;
                if (t > 0.9f)
                {
                    float tt = t - 0.9f;
                    b[i] += (Mathf.Sin(TAU * 523f * tt) + Mathf.Sin(TAU * 1307f * tt) * 0.6f + Mathf.Sin(TAU * 2011f * tt) * 0.4f) * Env(tt, 0.002f, 0.5f) * 0.5f;
                }
            }
            Mix(b, Thump(0.6f, 50f, 32f, 0.25f, 0.6f, 800f), (int)(0.9f * SR), 1f);
            Normalize(b);
            return Reverb(b, 0.4f, 1.6f, 1.2f);
        }

        static float[] Heartbeat()
        {
            var b = Buf(1f);
            Mix(b, Thump(0.25f, 52f, 40f, 0.07f, 0.2f, 300f), 0, 1f);
            Mix(b, Thump(0.25f, 48f, 38f, 0.06f, 0.2f, 300f), (int)(0.28f * SR), 0.75f);
            Normalize(b, 0.9f);
            return b;
        }

        static float[] UfoCapture()
        {
            var b = Buf(2.5f);
            float ph = 0f;
            var lp = new LP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR, u = t / 2.5f;
                float f = Mathf.Lerp(180f, 2400f, u * u) * (1f + 0.06f * Mathf.Sin(TAU * 9f * t));
                ph += TAU * f / SR;
                b[i] = (Mathf.Sin(ph) * 0.6f + lp.Process(Noise(), 400f + 4000f * u) * 0.8f) * Mathf.Min(1f, u * 3f) * (1f - Mathf.Pow(u, 6f));
            }
            Normalize(b);
            return Reverb(b, 0.45f, 1.8f, 1.2f);
        }

        static float[] Chime(float[] notes, float seconds)
        {
            var b = Buf(seconds);
            for (int n = 0; n < notes.Length; n++)
            {
                int s0 = (int)(n * 0.12f * SR);
                for (int i = s0; i < b.Length; i++)
                {
                    float t = (i - s0) / (float)SR;
                    b[i] += (Mathf.Sin(TAU * notes[n] * t) + 0.3f * Mathf.Sin(TAU * notes[n] * 2.01f * t)) * Env(t, 0.004f, seconds * 0.35f) * 0.3f;
                }
            }
            Normalize(b, 0.7f);
            return Reverb(b, 0.4f, 1.5f, 1f);
        }

        static float[] UiClick()
        {
            var b = Buf(0.06f);
            for (int i = 0; i < b.Length; i++) b[i] = Mathf.Sin(TAU * 2200f * i / SR) * Env(i / (float)SR, 0.001f, 0.012f);
            Normalize(b, 0.6f);
            return b;
        }

        static float[] RadioStatic()
        {
            var b = Buf(3.5f);
            var bp = new BP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float crackle = rng.NextDouble() < 0.004 ? Noise() * 4f : 0f;
                float squelch = t < 0.15f ? Mathf.Sin(TAU * 1500f * t) * 0.6f : 0f;
                b[i] = bp.Process(Noise() + crackle, 1800f, 0.8f) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 6f, 1f)) + squelch;
            }
            FadeEdges(b, 0.01f, 0.4f);
            Normalize(b, 0.6f);
            return b;
        }

        static float[] RadioVoice()
        {
            var b = Buf(7f);
            float t0 = 0.2f;
            while (t0 < 6.4f)
            {
                float syl = Rand(0.12f, 0.3f);
                float p = Rand(105f, 150f);
                var s = Formant(syl, p, p * Rand(0.85f, 1.1f), Rand(400f, 800f), Rand(1000f, 2200f), 0.3f);
                Mix(b, s, (int)(t0 * SR), 0.8f);
                t0 += syl + (rng.NextDouble() < 0.2 ? Rand(0.25f, 0.6f) : Rand(0.02f, 0.08f));
            }
            var bp = new BP();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                float dropout = Mathf.PerlinNoise(t * 3f, 4f) < 0.3f ? 0.15f : 1f;
                b[i] = Drive(bp.Process(b[i], 1300f, 0.7f) * 3f * dropout, 2f) + bp.Process(Noise(), 2000f, 0.8f) * 0.08f;
            }
            Normalize(b, 0.7f);
            return b;
        }

        // ------------------------------------------------------------------ Escritura y banco

        static AudioClip Write(string name, float[] samples)
        {
            PolyHavenMaterialBuilder.EnsureFolder(OutDir);
            string path = OutDir + "/" + name + ".wav";
            // Limitador final: la reverberación suma nivel después de normalizar.
            float peak = 0f;
            foreach (var s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak > 0.9f) for (int i = 0; i < samples.Length; i++) samples[i] *= 0.9f / peak;
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + bytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(SR);
                w.Write(SR * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(bytes);
                foreach (var s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }
            AssetDatabase.ImportAsset(path);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = imp.defaultSampleSettings;
            bool longClip = samples.Length > SR * 3;
            settings.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            imp.defaultSampleSettings = settings;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        [MenuItem("Project Ridge/Audio/Synthesize Audio Bank")]
        public static string Build()
        {
            rng = new System.Random(1);
            var bank = AssetDatabase.LoadAssetAtPath<ProjectRidge.Core.AudioBank>(BankPath);
            if (bank == null) { bank = ScriptableObject.CreateInstance<ProjectRidge.Core.AudioBank>(); AssetDatabase.CreateAsset(bank, BankPath); }

            AudioClip[] Many(string n, int count, System.Func<int, float[]> make)
            {
                var list = new List<AudioClip>();
                for (int i = 0; i < count; i++) list.Add(Write(n + "_" + i, make(i)));
                return list.ToArray();
            }

            bank.stepsDirt = Many("step_dirt", 4, StepDirt);
            bank.stepsWood = Many("step_wood", 3, StepWood);
            bank.hurt = Write("player_hurt", Hurt());
            bank.death = Write("player_death", Death());
            bank.heal = Write("player_heal", Heal());
            bank.pickup = Write("pickup", Pickup());
            bank.crateOpen = Write("crate_open", Creak(0.6f));
            bank.craft = Write("craft", Craft());
            bank.clubSwing = Write("club_swing", Whoosh(0.28f, 300f, 1600f, 500f));
            bank.clubHit = Write("club_hit", ClubHit());
            bank.clubBreak = Write("club_break", ClubBreak());
            bank.flareShot = Write("flare_shot", FlareShot());
            bank.flareBurnLoop = Write("flare_burn_loop", FlareBurnLoop());
            bank.dryFire = Write("dry_fire", DryFire());
            bank.lurkerSteps = Many("lurker_step", 3, LurkerStep);
            bank.lurkerClicks = Many("lurker_click", 4, LurkerClick);
            bank.lurkerScreech = Write("lurker_screech", LurkerScreech());
            bank.lurkerAttack = Write("lurker_attack", LurkerAttack());
            bank.lurkerGrowlLoop = Write("lurker_growl_loop", LurkerGrowlLoop());
            bank.lurkerHurt = Write("lurker_hurt", LurkerHurt());
            bank.lurkerDeath = Write("lurker_death", LurkerDeath());
            bank.windLoop = Write("amb_wind_loop", WindLoop());
            bank.forestLoop = Write("amb_forest_loop", ForestLoop());
            bank.musicAmbientLoop = Write("music_ambient_loop", MusicAmbient());
            bank.musicChaseLoop = Write("music_chase_loop", MusicChase());
            bank.alertSting = Write("alert_sting", AlertSting());
            bank.heartbeatLoop = Write("heartbeat_loop", Heartbeat());
            bank.ufoCapture = Write("ufo_capture", UfoCapture());
            bank.checkpoint = Write("checkpoint", Chime(new[] { 659.25f, 987.77f }, 1.6f));
            bank.clueSting = Write("clue_sting", Chime(new[] { 220f, 261.63f, 329.63f, 196f }, 2.8f));
            bank.uiClick = Write("ui_click", UiClick());
            bank.radioStatic = Write("radio_static", RadioStatic());
            bank.radioVoice = Write("radio_voice", RadioVoice());
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            return "audio bank: " + AssetDatabase.FindAssets("t:AudioClip", new[] { OutDir }).Length + " clips";
        }

        /// <summary>Crea el AudioDirector en la escena activa y el siseo de la bengala.</summary>
        [MenuItem("Project Ridge/Audio/Setup Audio in Active Scene")]
        public static string SetupScene()
        {
            var bank = AssetDatabase.LoadAssetAtPath<ProjectRidge.Core.AudioBank>(BankPath);
            var old = GameObject.Find("AudioDirector");
            if (old != null) Object.DestroyImmediate(old);
            var player = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerController>();
            var go = new GameObject("AudioDirector");
            var dir = go.AddComponent<ProjectRidge.Core.AudioDirector>();
            var so = new SerializedObject(dir);
            so.FindProperty("bank").objectReferenceValue = bank;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("fatherAnimator").objectReferenceValue = player.GetComponent<ProjectRidge.Player.FatherAnimator>();
            so.FindProperty("combat").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerCombat>();
            so.FindProperty("health").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerHealth>();
            so.FindProperty("inventory").objectReferenceValue = player.GetComponent<ProjectRidge.Items.PlayerInventory>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Bengala: siseo 3D en bucle.
            string prefabPath = "Assets/_Project/Prefabs/FlareProjectile.prefab";
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (!prefabRoot.TryGetComponent(out AudioSource src)) src = prefabRoot.AddComponent<AudioSource>();
            src.clip = bank.flareBurnLoop;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            src.maxDistance = 25f;
            var fso = new SerializedObject(prefabRoot.GetComponent<ProjectRidge.Items.FlareProjectile>());
            fso.FindProperty("burnAudio").objectReferenceValue = src;
            fso.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            return "audio director in " + scene.path;
        }
    }
}
