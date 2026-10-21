using System.Collections.Generic;
using System.Linq;
using ProjectRidge.Core;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Música y loudness (POLISH_PASS 3.11): asigna las capas y stingers de OpenGameArt (CC0, ver ASSETS.md) al
    /// AudioBank, mide el loudness integrado (ITU-R BS.1770-4) de cada clip del banco y guarda la ganancia que lo
    /// lleva a su objetivo: música −20, efectos −16, ambiente −24 LUFS. AudioDirector aplica esa ganancia.
    /// </summary>
    public static class MusicBuilder
    {
        public const string MusicDir = "Assets/ThirdParty/OpenGameArt/Music";
        const string BankPath = "Assets/_Project/Data/AudioBank.asset";
        /// <summary>Ganancias por debajo de esto se recortan: más bajo sería un error de medición.</summary>
        const float MinGain = 0.05f;

        static readonly HashSet<string> AmbientFields = new HashSet<string> { "windLoop", "forestLoop", "rainLoop", "creekLoop", "searchlightHumLoop" };

        static bool IsMusic(string field) => field.StartsWith("music") || field.StartsWith("sting") || field == "alertSting" || field == "clueSting";

        [MenuItem("Project Ridge/Audio/Assign Music and Normalize Loudness")]
        public static string Assign()
        {
            var bank = AssetDatabase.LoadAssetAtPath<AudioBank>(BankPath);
            AudioClip M(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(MusicDir + "/" + file);
            ConfigureImports();
            bank.musicExplore = M("abandoned_passages_freezer.ogg");
            bank.musicTension = M("srg774_pulse.mp3");
            bank.musicChase = M("srg774_urgent.mp3");
            bank.stingClue = M("congusbongus_piano_sting.wav");
            bank.stingDetected = M("congusbongus_abyss.ogg");
            bank.stingCapture = M("congusbongus_abyss.ogg");
            bank.stingCheckpoint = M("srg774_victory.mp3");
            bank.stingEnd = M("srg774_transmission.mp3");

            // Todos los clips del banco con su categoría (por el nombre del campo). Si ya existe la copia normalizada de un
            // clip (porque necesitaba subir de volumen), el banco pasa a usarla.
            var targets = new Dictionary<AudioClip, float>();
            var so = new SerializedObject(bank);
            var it = so.GetIterator();
            bool enter = true;
            int swapped = 0;
            AudioClip Swap(AudioClip c)
            {
                var n = AssetDatabase.LoadAssetAtPath<AudioClip>(NormalizedDir + "/" + c.name + "_n.ogg");
                if (n == null) return c;
                swapped++;
                return n;
            }
            while (it.NextVisible(enter))
            {
                enter = false;
                float target = IsMusic(it.name) ? bank.musicLufs : AmbientFields.Contains(it.name) ? bank.ambientLufs : bank.effectsLufs;
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is AudioClip c)
                {
                    it.objectReferenceValue = Swap(c);
                    targets[(AudioClip)it.objectReferenceValue] = target;
                }
                else if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.name != "gains")
                    for (int i = 0; i < it.arraySize; i++)
                    {
                        var el = it.GetArrayElementAtIndex(i);
                        if (!(el.objectReferenceValue is AudioClip ac)) continue;
                        el.objectReferenceValue = Swap(ac);
                        targets[(AudioClip)el.objectReferenceValue] = target;
                    }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // Medir. Bajar volumen se hace en tiempo de juego (ganancia < 1); subirlo no (AudioSource.volume tope 1):
            // esos clips van a la lista de trabajo para generar su copia normalizada con Blender.
            var log = new System.Text.StringBuilder();
            var gains = new List<AudioBank.ClipGain>();
            var jobs = new List<string>();
            int short_ = 0;
            foreach (var kv in targets)
            {
                float lufs = Measure(kv.Key, out float peak);
                float gain = float.IsNegativeInfinity(lufs) ? 1f : Mathf.Pow(10f, (kv.Value - lufs) / 20f);
                bool isCopy = kv.Key.name.EndsWith("_n");
                float boost = Mathf.Min(gain, PeakCeiling / Mathf.Max(peak, 1e-4f));
                if (gain > 1.06f && boost <= 1.06f) short_++; // transitorios: el pico llega al techo antes que el loudness
                else if (gain > 1.06f)
                {
                    if (!isCopy)
                    {
                        string src = System.IO.Path.GetFullPath(AssetDatabase.GetAssetPath(kv.Key)).Replace('\\', '/');
                        string dst = System.IO.Path.GetFullPath(NormalizedDir + "/" + kv.Key.name + "_n.ogg").Replace('\\', '/');
                        jobs.Add("{\"src\": \"" + src + "\", \"dst\": \"" + dst + "\", \"gain\": " + boost.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
                                 + ", \"channels\": " + kv.Key.channels + ", \"rate\": " + kv.Key.frequency + "}");
                    }
                    else short_++; // la copia ya está al techo de pico
                }
                gains.Add(new AudioBank.ClipGain { clip = kv.Key, measuredLufs = lufs, gain = Mathf.Clamp(gain, MinGain, 1f), peakLimited = gain > 1.06f && (isCopy || boost <= 1.06f) });
            }
            bank.gains = gains.ToArray();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            System.IO.File.WriteAllText(NormalizeJobs, "[\n" + string.Join(",\n", jobs) + "\n]\n");
            foreach (var c in new[] { bank.musicExplore, bank.musicTension, bank.musicChase })
            {
                var g = gains.First(x => x.clip == c);
                log.AppendLine(c.name + ": " + g.measuredLufs.ToString("0.0") + " LUFS, gain " + g.gain.ToString("0.00"));
            }
            log.Append("music: layers + 5 stingers, " + gains.Count + " clips measured, " + swapped + " normalized copies in use, "
                       + jobs.Count + " still need a copy (run Tools/Blender/normalize_audio.py), " + short_ + " limited by peak");
            return log.ToString();
        }

        /// <summary>Objetivo de loudness (LUFS) de un clip según el campo del banco donde está (el mismo criterio de Assign).</summary>
        public static float TargetOf(AudioBank bank, AudioClip clip)
        {
            var it = new SerializedObject(bank).GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                float target = IsMusic(it.name) ? bank.musicLufs : AmbientFields.Contains(it.name) ? bank.ambientLufs : bank.effectsLufs;
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == clip) return target;
                if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.name != "gains")
                    for (int i = 0; i < it.arraySize; i++)
                        if (it.GetArrayElementAtIndex(i).objectReferenceValue == clip) return target;
            }
            return bank.effectsLufs;
        }

        /// <summary>
        /// Música en streaming (memoria en iPhone) salvo para medir: GetData necesita el clip descomprimido, así que se
        /// mide con DecompressOnLoad y se deja en Streaming después (ConfigureImports(streaming: true)).
        /// </summary>
        static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir }))
            {
                var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                imp.defaultSampleSettings = s;
                imp.loadInBackground = false;
                imp.SaveAndReimport();
            }
        }

        /// <summary>Pasa la música larga a Streaming tras medir (se llama al final de Assign desde BuildAll).</summary>
        public static void StreamLongMusic()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.length < 20f) continue;
                var imp = (AudioImporter)AssetImporter.GetAtPath(path);
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.Streaming;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }

        /// <summary>
        /// Loudness integrado de un clip (LUFS). GetData solo funciona con DecompressOnLoad: si el clip está comprimido
        /// en memoria o en streaming, se reimporta así un momento y se devuelve a su ajuste.
        /// </summary>
        public static float Measure(AudioClip clip) => Measure(clip, out _);

        public static float Measure(AudioClip clip, out float peak)
        {
            peak = 0f;
            string path = AssetDatabase.GetAssetPath(clip);
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            AudioImporterSampleSettings? restore = null;
            if (imp != null && imp.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                restore = imp.defaultSampleSettings;
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            try
            {
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0)) return float.NegativeInfinity;
                foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
                return Integrated(data, clip.channels, clip.frequency);
            }
            finally
            {
                if (restore.HasValue) { imp.defaultSampleSettings = restore.Value; imp.SaveAndReimport(); }
            }
        }

        /// <summary>Copias normalizadas (Tools/Blender/normalize_audio.py) de los clips que necesitan subir de volumen.</summary>
        public const string NormalizedDir = "Assets/_Project/Audio/Normalized";
        /// <summary>Lista de trabajo para el script de Blender (fuera de Assets).</summary>
        public const string NormalizeJobs = "Tools/Blender/normalize_jobs.json";
        /// <summary>Techo de pico (lineal) al normalizar hacia arriba: −1 dBFS.</summary>
        const float PeakCeiling = 0.89f;

        /// <summary>
        /// ITU-R BS.1770-4: filtro K (estante alto + paso alto), bloques de 400 ms con 75% de solape, compuerta
        /// absoluta a −70 LUFS y relativa a −10 LU. Canales L/R con peso 1. Clips de menos de 400 ms: un solo bloque.
        /// </summary>
        public static float Integrated(float[] interleaved, int channels, int rate)
        {
            int n = interleaved.Length / channels;
            var weighted = new float[interleaved.Length];
            for (int ch = 0; ch < channels; ch++)
            {
                // Etapa 1: estante alto (+4 dB sobre ~1.7 kHz).
                double f0 = 1681.974450955533, g = 3.999843853973347, q = 0.7071752369554196;
                double k = System.Math.Tan(System.Math.PI * f0 / rate);
                double vh = System.Math.Pow(10.0, g / 20.0), vb = System.Math.Pow(vh, 0.4996667741545416);
                double a0 = 1.0 + k / q + k * k;
                double b0 = (vh + vb * k / q + k * k) / a0, b1 = 2.0 * (k * k - vh) / a0, b2 = (vh - vb * k / q + k * k) / a0;
                double a1 = 2.0 * (k * k - 1.0) / a0, a2 = (1.0 - k / q + k * k) / a0;
                // Etapa 2: paso alto (RLB, ~38 Hz).
                double f1 = 38.13547087602444, q1 = 0.5003270373238773;
                double k1 = System.Math.Tan(System.Math.PI * f1 / rate);
                double d0 = 1.0 + k1 / q1 + k1 * k1;
                double c1 = 2.0 * (k1 * k1 - 1.0) / d0, c2 = (1.0 - k1 / q1 + k1 * k1) / d0;
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0, u1 = 0, u2 = 0, w1 = 0, w2 = 0;
                for (int i = 0; i < n; i++)
                {
                    double x = interleaved[i * channels + ch];
                    double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                    x2 = x1; x1 = x; y2 = y1; y1 = y;
                    double w = y - 2.0 * u1 + u2 - c1 * w1 - c2 * w2;
                    u2 = u1; u1 = y; w2 = w1; w1 = w;
                    weighted[i * channels + ch] = (float)w;
                }
            }
            int block = Mathf.Min(n, Mathf.RoundToInt(0.4f * rate)), step = Mathf.Max(1, Mathf.RoundToInt(0.1f * rate));
            var z = new List<double>();
            for (int start = 0; start + block <= n; start += step)
            {
                double sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    double ms = 0;
                    for (int i = start; i < start + block; i++) { double v = weighted[i * channels + ch]; ms += v * v; }
                    sum += ms / block;
                }
                z.Add(sum);
            }
            double L(double v) => -0.691 + 10.0 * System.Math.Log10(v);
            var abs = z.Where(v => v > 0 && L(v) > -70.0).ToList();
            if (abs.Count == 0) return float.NegativeInfinity;
            double rel = L(abs.Average()) - 10.0;
            var gated = abs.Where(v => L(v) > rel).ToList();
            return (float)L(gated.Average());
        }
    }
}
