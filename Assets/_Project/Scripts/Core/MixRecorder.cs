using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// QA de la mezcla: graba la salida final del AudioListener (todo lo que oye el jugador, ya mezclado) y la guarda
    /// como WAV al destruirse. Solo para pruebas en el editor (MixReport la analiza); no va en la escena del juego.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class MixRecorder : MonoBehaviour
    {
        public string outputPath = "Temp/mix.wav";
        [Tooltip("Tope de grabación (s) para no llenar la memoria.")]
        public float maxSeconds = 600f;

        private readonly List<float> samples = new List<float>(1 << 22);
        private readonly object gate = new object();
        private int channels = 2, rate;
        private int maxSamples;

        private void Awake()
        {
            rate = AudioSettings.outputSampleRate;
            maxSamples = Mathf.RoundToInt(maxSeconds * rate * 2);
        }

        private void OnAudioFilterRead(float[] data, int ch)
        {
            lock (gate)
            {
                channels = ch;
                if (samples.Count + data.Length > maxSamples) return;
                samples.AddRange(data);
            }
        }

        private void OnDestroy() => Save();

        public void Save()
        {
            float[] data;
            int ch;
            lock (gate) { data = samples.ToArray(); ch = channels; samples.Clear(); }
            if (data.Length == 0) return;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            using (var w = new BinaryWriter(File.Create(outputPath)))
            {
                int bytes = data.Length * 4;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
                w.Write((short)3); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 4); w.Write((short)(ch * 4)); w.Write((short)32);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var s in data) w.Write(s);
            }
            Debug.Log("MIX: saved " + (data.Length / ch / (float)rate).ToString("0.0") + " s to " + outputPath);
        }
    }
}
