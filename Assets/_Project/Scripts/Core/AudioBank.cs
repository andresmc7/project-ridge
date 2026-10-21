using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>Todos los clips del PoC (sintetizados en el proyecto por SoundSynth) y los volúmenes por categoría.</summary>
    [CreateAssetMenu(menuName = "Project Ridge/Audio Bank", fileName = "AudioBank")]
    public class AudioBank : ScriptableObject
    {
        [Header("Volumen por categoría (equivale a los grupos del mixer)")]
        [Range(0f, 1f)] public float music = 0.55f;
        [Range(0f, 1f)] public float ambient = 0.7f;
        [Range(0f, 1f)] public float player = 0.8f;
        [Range(0f, 1f)] public float enemy = 1f;
        [Range(0f, 1f)] public float weapons = 0.9f;
        [Range(0f, 1f)] public float ui = 0.7f;

        [Header("Ambiente y música")]
        public AudioClip windLoop;
        public AudioClip forestLoop;
        public AudioClip rainLoop;
        public AudioClip creekLoop;
        public AudioClip musicAmbientLoop;
        public AudioClip musicChaseLoop;
        public AudioClip alertSting;
        public AudioClip heartbeatLoop;

        [Header("Jugador")]
        public AudioClip[] stepsDirt;
        public AudioClip[] stepsWood;
        [Tooltip("Pisadas reales (Kenney, CC0): pasto y tierra del bosque.")]
        public AudioClip[] stepsGrass;
        [Tooltip("Golpes de cuerpo (garrote que conecta, zarpazo que te alcanza).")]
        public AudioClip[] bodyHits;
        [Tooltip("Caídas y golpes sordos (cuerpo que cae, escombros pesados).")]
        public AudioClip[] bodyFalls;
        public AudioClip[] debrisWood;
        public AudioClip[] debrisMetal;
        public AudioClip hurt;
        public AudioClip death;
        public AudioClip heal;
        public AudioClip pickup;
        public AudioClip crateOpen;
        public AudioClip craft;

        [Header("Armas")]
        public AudioClip clubSwing;
        public AudioClip clubHit;
        public AudioClip clubBreak;
        public AudioClip flareShot;
        public AudioClip flareBurnLoop;
        public AudioClip dryFire;

        [Header("Lurker")]
        public AudioClip[] lurkerSteps;
        public AudioClip[] lurkerClicks;
        public AudioClip lurkerScreech;
        public AudioClip lurkerAttack;
        public AudioClip lurkerGrowlLoop;
        public AudioClip lurkerHurt;
        public AudioClip lurkerDeath;

        [Header("OVNI y UI")]
        public AudioClip ufoCapture;
        public AudioClip checkpoint;
        public AudioClip clueSting;
        public AudioClip uiClick;
        public AudioClip radioStatic;
        public AudioClip radioVoice;
        public AudioClip radioLoop;
        public AudioClip stoneImpact;
        [Tooltip("Zumbido 3D sobre cada reflector rojo: se oye venir.")]
        public AudioClip searchlightHumLoop;
        [Tooltip("Un reflector te vio (empieza la exposición).")]
        public AudioClip searchlightAlarm;

        [Header("Música por capas (POLISH_PASS 3.11; OpenGameArt CC0, ver ASSETS.md)")]
        [Tooltip("Explorar: dron grave con texturas espaciadas.")]
        public AudioClip musicExplore;
        [Tooltip("Tensión: pulso grave (latido).")]
        public AudioClip musicTension;
        [Tooltip("Persecución: pulso urgente.")]
        public AudioClip musicChase;
        [Tooltip("Sospecha de algún Lurker a partir de la cual suena la capa de tensión.")]
        [Range(0f, 1f)] public float tensionSuspicion = 0.3f;
        [Tooltip("Un Lurker a esta distancia (m) o menos: tensión.")]
        [Min(0f)] public float tensionLurkerDistance = 12f;
        [Tooltip("El haz de un reflector a esta distancia (m) o menos: tensión.")]
        [Min(0f)] public float tensionBeamDistance = 10f;
        [Tooltip("Segundos de crossfade entre capas.")]
        [Min(0.05f)] public float layerCrossfade = 1.5f;

        [Header("Stingers")]
        public AudioClip stingClue;
        public AudioClip stingDetected;
        public AudioClip stingCapture;
        public AudioClip stingCheckpoint;
        public AudioClip stingEnd;

        [Header("Loudness (LUFS integrados, ITU-R BS.1770; medidos por MusicBuilder)")]
        public float musicLufs = -20f;
        public float effectsLufs = -16f;
        public float ambientLufs = -24f;
        [Tooltip("Ganancia por clip para llevarlo a su objetivo de loudness (la escribe MusicBuilder).")]
        public ClipGain[] gains = new ClipGain[0];

        [System.Serializable]
        public struct ClipGain
        {
            public AudioClip clip;
            [Tooltip("Loudness medido del archivo (LUFS).")]
            public float measuredLufs;
            [Tooltip("Factor lineal de volumen que lo lleva al objetivo de su categoría.")]
            public float gain;
            [Tooltip("No llega al objetivo sin saturar: su pico ya está a −1 dBFS (sonidos transitorios).")]
            public bool peakLimited;
        }

        private System.Collections.Generic.Dictionary<AudioClip, float> gainLookup;

        /// <summary>Factor de normalización de loudness del clip (1 si no se midió).</summary>
        public float Gain(AudioClip clip)
        {
            if (clip == null) return 1f;
            if (gainLookup == null || gainLookup.Count != gains.Length)
            {
                gainLookup = new System.Collections.Generic.Dictionary<AudioClip, float>();
                foreach (var g in gains) if (g.clip != null) gainLookup[g.clip] = g.gain;
            }
            return gainLookup.TryGetValue(clip, out float v) ? v : 1f;
        }
    }
}
