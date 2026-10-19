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
    }
}
