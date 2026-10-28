using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Limitador de la mezcla final (va junto al AudioListener). Medido con MixRecorder en una partida completa: la
    /// mezcla quedaba en −20 LUFS (bajo para los parlantes de un teléfono) y saturaba a +1.3 dBFS cuando coincidían
    /// la radio, una captura y la música de persecución. Sube la mezcla y no deja pasar picos sobre el techo: ataque
    /// instantáneo (la ganancia se calcula con la muestra actual, nunca hay exceso) y liberación suave.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class MasterLimiter : MonoBehaviour
    {
        [Tooltip("Ganancia antes del limitador (dB): lleva la mezcla de ~−20 a ~−16 LUFS.")]
        [Range(0f, 12f)] public float makeupDb = 4f;
        [Tooltip("Techo de pico (dBFS).")]
        [Range(-6f, 0f)] public float ceilingDb = -1f;
        [Tooltip("Tiempo de recuperación de la ganancia (ms).")]
        [Min(1f)] public float releaseMs = 150f;

        private float gain = 1f, makeup = 1f, ceiling = 1f, release;

        /// <summary>Reducción de ganancia actual (dB, ≤ 0), para pruebas.</summary>
        public float GainReductionDb => 20f * Mathf.Log10(Mathf.Max(gain, 1e-4f));

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();

        private void Refresh()
        {
            makeup = Mathf.Pow(10f, makeupDb / 20f);
            ceiling = Mathf.Pow(10f, ceilingDb / 20f);
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            release = 1f - Mathf.Exp(-1f / (releaseMs * 0.001f * rate));
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float g = gain;
            for (int i = 0; i < data.Length; i += channels)
            {
                float peak = 0f;
                for (int c = 0; c < channels; c++) peak = Mathf.Max(peak, Mathf.Abs(data[i + c]) * makeup);
                float target = peak > ceiling ? ceiling / peak : 1f;
                g = target < g ? target : g + (target - g) * release;
                float k = makeup * g;
                for (int c = 0; c < channels; c++) data[i + c] *= k;
            }
            gain = g;
        }
    }
}
