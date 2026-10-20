using System;
using System.Collections;
using ProjectRidge.AI;
using ProjectRidge.CameraRig;
using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ProjectRidge.Core
{
    /// <summary>
    /// La captura del OVNI con peso (POLISH_PASS 3.4):
    /// 1. Exposición: mientras se llena, el zumbido sube de tono y aparece una viñeta fría.
    /// 2. Bloqueo: el haz se detiene en seco, se estrecha y pasa a blanco frío; el papá se detiene en seco.
    /// 3. Parálisis (1.6 s): pose de shock rígida con temblor eléctrico, linterna que falla, aberración, grano,
    ///    desaturación y la cámara que se acerca; crepitar eléctrico y el latido se corta; háptico continuo.
    /// 4. Forcejeo (una vez por vida): toques rápidos en cualquier parte (o vaivenes del joystick) llenan un anillo;
    ///    si se llena, el papá se suelta de un tirón, rueda y el haz retrocede ciego.
    /// 5. Elevación (si no se suelta): sube con el temblor al máximo, destello blanco y al último checkpoint.
    /// </summary>
    public class CaptureSequence : MonoBehaviour
    {
        public enum Phase { None, Lock, Paralysis, Struggle, Freed, Lift }

        [SerializeField] private CaptureConfig config;
        [SerializeField] private PlayerController player;
        [SerializeField] private CharacterController body;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private FatherAnimator father;
        [SerializeField] private ShockPose shock;
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private UFOExposure ufoExposure;
        [Tooltip("Volume global con la aberración, el grano, la desaturación y la viñeta fría (su peso va de 0 a 1).")]
        [SerializeField] private Volume volume;
        [Tooltip("Anillo del forcejeo (Image de relleno radial) y su grupo; se coloca sobre el papá en pantalla.")]
        [SerializeField] private Image struggleFill;
        [SerializeField] private CanvasGroup struggleGroup;
        [SerializeField] private CanvasGroup whiteFlash;
        [Tooltip("Crepitar eléctrico (se genera al arrancar).")]
        [SerializeField] private AudioSource crackle;

        public Phase Current { get; private set; }
        /// <summary>0–1: relleno del anillo del forcejeo.</summary>
        public float StruggleProgress { get; private set; }
        /// <summary>Ya se usó el forcejeo en esta vida.</summary>
        public bool StruggleUsed { get; private set; }
        public event Action<Phase> PhaseChanged;

        private Camera cam;
        private float lastStickX;

        private void Awake()
        {
            cam = Camera.main;
            if (crackle != null) { crackle.clip = BuildCrackle(); crackle.loop = true; crackle.playOnAwake = false; }
            HideUi();
        }

        /// <summary>Tras reaparecer: forcejeo disponible otra vez y todo el estado visual a cero.</summary>
        public void ResetForNewLife()
        {
            StruggleUsed = false;
            StopEffects();
            if (whiteFlash != null) whiteFlash.alpha = 0f;
            foreach (var s in Searchlight.Active) if (s.Locked) s.Unlock();
            SetPhase(Phase.None);
        }

        private void Update()
        {
            if (Current != Phase.None) return;
            // Aviso mientras se llena la exposición: viñeta fría y zumbido más agudo.
            float e = ufoExposure != null ? ufoExposure.Exposure * 0.5f : 0f;
            foreach (var s in Searchlight.Active) e = Mathf.Max(e, s.Exposure);
            if (volume != null) volume.weight = config.exposureVignette * e;
            UFOHum.ExposurePitch = 0.25f * e;
        }

        public IEnumerator Run(Searchlight beam, Action<bool> done)
        {
            // ---- Bloqueo: todo se detiene en seco.
            SetPhase(Phase.Lock);
            if (beam != null) beam.Lock(config.beamNarrow, config.lockColor);
            player.InputLocked = true;
            body.enabled = false;
            father.Freeze(true);
            Haptics.Continuous(true);
            AudioDirector.HeartbeatMuted = true;
            if (crackle != null) crackle.Play();
            UFOHum.ExposurePitch = 0.4f;

            // ---- Parálisis.
            SetPhase(Phase.Paralysis);
            float t = 0f;
            while (t < config.paralysisTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / config.paralysisTime);
                shock.Weight = Mathf.Clamp01(t / 0.2f);
                shock.Tremor = Mathf.Lerp(config.tremorStart, config.tremorEnd, k);
                shock.TremorHz = UnityEngine.Random.Range(config.tremorHzMin, config.tremorHzMax);
                flashlight.Flicker = 0.8f;
                if (volume != null) volume.weight = Mathf.Lerp(config.exposureVignette, 1f, k);
                IsoCameraRig.ZoomScale = 1f - config.cameraZoom * k;
                yield return null;
            }

            // ---- Forcejeo (una vez por vida).
            bool freed = false;
            if (!StruggleUsed)
            {
                StruggleUsed = true;
                SetPhase(Phase.Struggle);
                StruggleProgress = 0f;
                lastStickX = 0f;
                if (struggleGroup != null) struggleGroup.alpha = 1f;
                t = 0f;
                while (t < config.struggleTime && !freed)
                {
                    t += Time.deltaTime;
                    StruggleProgress = Mathf.Min(1f, StruggleProgress + CountTaps() / (float)config.tapsNeeded);
                    freed = StruggleProgress >= 1f;
                    PlaceRing();
                    if (struggleFill != null) struggleFill.fillAmount = StruggleProgress;
                    shock.Tremor = config.tremorEnd * (1f + 0.5f * StruggleProgress);
                    yield return null;
                }
                if (struggleGroup != null) struggleGroup.alpha = 0f;
            }

            if (freed)
            {
                // ---- Se suelta: tirón, rueda lejos del haz; el haz retrocede ciego.
                SetPhase(Phase.Freed);
                Vector3 away = beam != null ? player.transform.position - beam.Source : -player.transform.forward;
                away.y = 0f;
                if (beam != null) beam.Recoil(-away, config.beamRecoil, config.beamBlind);
                StopEffects();
                body.enabled = true;
                player.FaceTowards(player.transform.position + away);
                player.AddKnockback(away.normalized * config.breakFreeKnock);
                father.PlayRoll();
                player.InputLocked = false;
                SetPhase(Phase.None);
                done(true);
                yield break;
            }

            // ---- Elevación: los pies se despegan, temblor al máximo, destello blanco.
            SetPhase(Phase.Lift);
            Vector3 start = player.transform.position;
            t = 0f;
            while (t < config.liftTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / config.liftTime);
                player.transform.position = start + Vector3.up * (config.liftHeight * k * k);
                shock.Tremor = config.tremorLift;
                shock.TremorHz = UnityEngine.Random.Range(config.tremorHzMin, config.tremorHzMax);
                if (whiteFlash != null) whiteFlash.alpha = Mathf.Clamp01((t - (config.liftTime - config.flashTime)) / config.flashTime);
                yield return null;
            }
            if (whiteFlash != null) whiteFlash.alpha = 1f;
            StopEffects();
            done(false);
        }

        /// <summary>Toques nuevos este frame: pantalla, cualquier tecla o botón, o un vaivén del joystick.</summary>
        private int CountTaps()
        {
            int n = 0;
            foreach (var d in InputSystem.devices)
            {
                switch (d)
                {
                    case Touchscreen ts:
                        foreach (var touch in ts.touches) if (touch.press.wasPressedThisFrame) n++;
                        break;
                    case Keyboard k:
                        if (k.anyKey.wasPressedThisFrame) n++;
                        break;
                    case Mouse m:
                        if (m.leftButton.wasPressedThisFrame) n++;
                        break;
                    case Gamepad g:
                        foreach (var b in new ButtonControl[] { g.buttonSouth, g.buttonEast, g.buttonWest, g.buttonNorth, g.rightShoulder, g.leftShoulder })
                            if (b.wasPressedThisFrame) n++;
                        break;
                }
            }
            float x = input.Move.x;
            if (Mathf.Abs(x) > 0.5f)
            {
                if (lastStickX != 0f && Mathf.Sign(x) != Mathf.Sign(lastStickX)) n++;
                lastStickX = x;
            }
            return n;
        }

        private void PlaceRing()
        {
            if (struggleGroup == null || cam == null) return;
            var rt = (RectTransform)struggleGroup.transform;
            Vector3 sp = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 1f);
            var parent = (RectTransform)rt.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, null, out Vector2 local)) rt.anchoredPosition = local;
        }

        private void StopEffects()
        {
            shock.Weight = 0f;
            shock.Tremor = 0f;
            father.Freeze(false);
            flashlight.Flicker = 0f;
            if (volume != null) volume.weight = 0f;
            IsoCameraRig.ZoomScale = 1f;
            Haptics.Continuous(false);
            AudioDirector.HeartbeatMuted = false;
            UFOHum.ExposurePitch = 0f;
            if (crackle != null) crackle.Stop();
            HideUi();
        }

        private void HideUi()
        {
            if (struggleGroup != null) struggleGroup.alpha = 0f;
        }

        private void SetPhase(Phase p)
        {
            if (p == Current) return;
            Current = p;
            PhaseChanged?.Invoke(p);
        }

        /// <summary>Crepitar eléctrico: chasquidos de ruido con un zumbido de red de fondo (procedural, sin assets).</summary>
        private static AudioClip BuildCrackle()
        {
            int rate = AudioSettings.outputSampleRate;
            int samples = rate * 2;
            var data = new float[samples];
            var rng = new System.Random(31);
            float env = 0f;
            for (int i = 0; i < samples; i++)
            {
                if (rng.NextDouble() < 0.0016) env = 0.6f + (float)rng.NextDouble() * 0.4f;
                env *= 0.9965f;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float t = i / (float)rate;
                // 100 Hz (2 s = ciclos enteros: el bucle no hace clic) y su armónico.
                float mains = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.18f + Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.06f;
                data[i] = noise * env * 0.7f + mains;
            }
            var clip = AudioClip.Create("Capture_Crackle", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
