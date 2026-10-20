using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Zumbido ominoso del OVNI, sintetizado al iniciar (sin assets externos): drone grave con armónicos,
    /// batido lento y ruido filtrado. Loop exacto (todas las frecuencias completan ciclos enteros en el clip).
    /// Sube de volumen y tono mientras el OVNI te sigue.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class UFOHum : MonoBehaviour
    {
        [SerializeField] private UFOConfig config;
        [SerializeField] private UFOController ufo;

        private AudioSource source;
        private float trackBlend;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.clip = BuildClip();
            source.loop = true;
            source.spatialBlend = config.humSpatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = config.humMinDistance;
            source.maxDistance = config.humMaxDistance;
            source.dopplerLevel = 0f;
            source.volume = config.humVolume;
            source.Play();
        }

        /// <summary>Subida de tono extra (0 = ninguna). La pone la secuencia de captura según la exposición.</summary>
        public static float ExposurePitch;

        private void Update()
        {
            trackBlend = Mathf.MoveTowards(trackBlend, ufo.IsTracking ? 1f : 0f, Time.deltaTime * 0.8f);
            source.volume = config.humVolume * Mathf.Lerp(1f, config.humTrackingVolumeBoost, trackBlend);
            // La exposición de un reflector sube el tono (aviso antes de la captura); atrapado, aún más.
            source.pitch = Mathf.Lerp(1f, config.humTrackingPitch, trackBlend) * (1f + ExposurePitch);
            source.volume *= 1f + ExposurePitch;
        }

        private AudioClip BuildClip()
        {
            int rate = AudioSettings.outputSampleRate;
            float seconds = config.humLoopSeconds;
            int samples = Mathf.RoundToInt(rate * seconds);
            var data = new float[samples];

            // Frecuencias redondeadas a ciclos enteros por loop para que no haya clic en la unión.
            float F(float hz) => Mathf.Round(hz * seconds) / seconds;
            float baseHz = F(config.humBaseFrequency);
            float h2 = F(config.humBaseFrequency * 2.01f);
            float h3 = F(config.humBaseFrequency * 3.02f);
            float beat = F(config.humBeatFrequency);
            float wobble = F(config.humBeatFrequency * 0.37f);

            var rng = new System.Random(7);
            float noiseLp = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float tw = 2f * Mathf.PI * t;
                float drone = Mathf.Sin(tw * baseHz) * 0.55f + Mathf.Sin(tw * h2) * 0.25f + Mathf.Sin(tw * h3) * 0.12f;
                float am = 0.7f + 0.3f * Mathf.Sin(tw * beat) * (0.6f + 0.4f * Mathf.Sin(tw * wobble));
                float white = (float)rng.NextDouble() * 2f - 1f;
                noiseLp += (white - noiseLp) * 0.02f; // ruido grave filtrado
                data[i] = (drone * am + noiseLp * config.humNoise) * 0.6f;
            }
            var clip = AudioClip.Create("UFO_Hum", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
