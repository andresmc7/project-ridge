using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Análisis de una grabación de la mezcla (MixRecorder): loudness integrado (BS.1770, MusicBuilder.Integrated),
    /// pico de muestra, muestras saturadas y los tramos de 3 s más fuertes y más bajos (saltos de volumen, stingers
    /// que se comen todo, silencios). No reemplaza escucharla: dice si algo está fuera de rango.
    /// </summary>
    public static class MixReport
    {
        /// <summary>Objetivo de loudness para juego en móvil (sin auriculares de referencia): −16 LUFS ± 2.</summary>
        public const float TargetLufs = -16f;

        public static string Analyze(string path = "Temp/mix.wav")
        {
            var (data, ch, rate) = ReadWav(path);
            int frames = data.Length / ch;
            float integrated = MusicBuilder.Integrated(data, ch, rate);
            float peak = 0f; int clipped = 0;
            foreach (var s in data) { float a = Mathf.Abs(s); if (a > peak) peak = a; if (a >= 0.999f) clipped++; }

            // Loudness de corto plazo (ventanas de 3 s cada 1 s).
            var shortTerm = new List<(float t, float lufs)>();
            int win = 3 * rate, hop = rate;
            for (int start = 0; start + win <= frames; start += hop)
            {
                var slice = new float[win * ch];
                System.Array.Copy(data, start * ch, slice, 0, slice.Length);
                shortTerm.Add((start / (float)rate, MusicBuilder.Integrated(slice, ch, rate)));
            }
            var audible = shortTerm.Where(s => !float.IsNegativeInfinity(s.lufs)).OrderBy(s => s.lufs).ToList();
            float p10 = audible.Count > 0 ? audible[(int)(audible.Count * 0.1f)].lufs : float.NaN;
            float p95 = audible.Count > 0 ? audible[(int)(audible.Count * 0.95f)].lufs : float.NaN;
            int silent = shortTerm.Count - audible.Count;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("MIX: " + (frames / (float)rate).ToString("0.0") + " s, " + ch + " ch, " + rate + " Hz");
            sb.AppendLine("integrated " + integrated.ToString("0.0") + " LUFS (target " + TargetLufs + " ± 2), sample peak " + (20f * Mathf.Log10(Mathf.Max(peak, 1e-6f))).ToString("0.0") + " dBFS, clipped samples " + clipped);
            sb.AppendLine("short-term (3 s): 10th pct " + p10.ToString("0.0") + ", 95th pct " + p95.ToString("0.0") + " LUFS, range " + (p95 - p10).ToString("0.0") + " LU; silent windows " + silent);
            sb.Append("loudest:");
            foreach (var s in audible.AsEnumerable().Reverse().Take(5)) sb.Append(" t=" + s.t.ToString("0") + "s " + s.lufs.ToString("0.0"));
            sb.AppendLine();
            sb.Append("quietest:");
            foreach (var s in audible.Take(5)) sb.Append(" t=" + s.t.ToString("0") + "s " + s.lufs.ToString("0.0"));
            string result = sb.ToString();
            Debug.Log(result);
            return result;
        }

        static (float[] data, int ch, int rate) ReadWav(string path)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                r.ReadBytes(12);
                int ch = 2, rate = 48000;
                while (r.BaseStream.Position < r.BaseStream.Length)
                {
                    string id = new string(r.ReadChars(4));
                    int size = r.ReadInt32();
                    if (id == "fmt ")
                    {
                        r.ReadInt16(); ch = r.ReadInt16(); rate = r.ReadInt32();
                        r.ReadBytes(size - 8);
                    }
                    else if (id == "data")
                    {
                        var data = new float[size / 4];
                        for (int i = 0; i < data.Length; i++) data[i] = r.ReadSingle();
                        return (data, ch, rate);
                    }
                    else r.ReadBytes(size);
                }
                return (new float[0], ch, rate);
            }
        }
    }
}
