using System;
using System.Collections;
using ProjectRidge.AI;
using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Flujo de la partida: título → juego → (pausa, pista, muerte o captura del OVNI → último checkpoint) → final.
    /// Dueño de los paneles de pantalla completa y del objetivo actual.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public enum State { Title, Playing, Paused, Clue, Dying, Ending }

        public static GameFlow Instance { get; private set; }

        [Header("Jugador")]
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private CharacterController body;
        [SerializeField] private PlayerHealthConfig healthConfig;
        [SerializeField] private UFOExposure ufoExposure;
        [SerializeField] private Transform firstCheckpoint;
        [Tooltip("Secuencia de captura del OVNI (bloqueo, parálisis, forcejeo, elevación).")]
        [SerializeField] private CaptureSequence capture;

        [Header("UI")]
        [SerializeField] private CanvasGroup gameplayHud;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private Image fadeImage;
        [SerializeField] private CanvasGroup titlePanel;
        [SerializeField] private CanvasGroup pausePanel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private CanvasGroup cluePanel;
        [SerializeField] private Text clueTitle;
        [SerializeField] private Text clueBody;
        [SerializeField] private Text bigMessage;
        [SerializeField] private Text objectiveLabel;
        [SerializeField] private CanvasGroup endPanel;
        [SerializeField] private Text endBody;
        [SerializeField] private CanvasGroup endButtons;
        [Tooltip("Etiqueta del botón \"Ayudas de sigilo\" del menú de pausa.")]
        [SerializeField] private Text stealthAidsLabel;

        [Header("Textos")]
        [SerializeField] private string firstObjective = "Find a way through the forest.";
        [Tooltip("Segundos que se ve el objetivo al cambiar (al entrar a una sala nueva); luego se desvanece. En pausa se ve siempre.")]
        [Min(0f)] [SerializeField] private float objectiveShowSeconds = 4f;
        [Min(0.01f)] [SerializeField] private float objectiveFadeSeconds = 0.8f;
        [TextArea] [SerializeField] private string[] endingLines =
        {
            "The radio crackles to life.",
            "\"...anyone on this frequency... the lights took the whole valley... we're heading north to the old dam...\"",
            "A child's laugh, then static.",
            "He knows that laugh.",
            "The trail goes on."
        };

        private Vector3 checkpointPos;
        private Quaternion checkpointRot;
        private float objectiveFlash, objectiveUntil;
        /// <summary>Opacidad actual del objetivo (para las pruebas).</summary>
        public float ObjectiveAlpha => objectiveLabel != null ? objectiveLabel.color.a : 0f;
        public string Objective => objectiveLabel != null ? objectiveLabel.text : "";

        public State Current { get; private set; } = State.Title;
        public event Action<State> StateChanged;
        public event Action CheckpointReached;
        public event Action Captured;

        private const string VolumeKey = "ridge.volume";

        private void Awake()
        {
            Instance = this;
            checkpointPos = firstCheckpoint != null ? firstCheckpoint.position : player.transform.position;
            checkpointRot = firstCheckpoint != null ? firstCheckpoint.rotation : player.transform.rotation;
        }

        private void OnEnable()
        {
            health.Died += OnDied;
            input.PausePressed += OnPausePressed;
            input.ActionPressed += OnDismiss;
            if (ufoExposure != null) ufoExposure.Captured += OnCaptured;
            Searchlight.PlayerCaught += OnSearchlightCaught;
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
            input.PausePressed -= OnPausePressed;
            input.ActionPressed -= OnDismiss;
            if (ufoExposure != null) ufoExposure.Captured -= OnCaptured;
            Searchlight.PlayerCaught -= OnSearchlightCaught;
            Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            float volume = 1f;
            try { volume = PlayerPrefs.GetFloat(VolumeKey, 1f); } catch (Exception) { }
            AudioListener.volume = volume;
            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(volume);
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }

            Show(pausePanel, false);
            Show(cluePanel, false);
            Show(endPanel, false);
            UpdateStealthAidsLabel();
            Show(endButtons, false); // D14: nunca en el título (solo al final de la demo)
            Show(gameplayHud, false);
            Show(titlePanel, true);
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
            bigMessage.text = "";
            objectiveLabel.text = "";
            SetState(State.Title);
            player.InputLocked = true;
            StartCoroutine(FadeTo(0f, 2.5f, Color.black));
            InputSystem.onAnyButtonPress.CallOnce(_ => BeginFromTitle());
        }

        // ---------- Título

        private void BeginFromTitle()
        {
            if (Current != State.Title) return;
            StartCoroutine(BeginRoutine());
        }

        private IEnumerator BeginRoutine()
        {
            SetState(State.Playing);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 1.2f;
                titlePanel.alpha = 1f - t;
                yield return null;
            }
            Show(titlePanel, false);
            Show(gameplayHud, true);
            player.InputLocked = false;
            SetObjective(firstObjective);
        }

        // ---------- Pausa

        private void OnPausePressed()
        {
            if (Current == State.Playing) Pause(true);
            else if (Current == State.Paused) Pause(false);
        }

        public void Pause(bool paused)
        {
            if (paused && Current != State.Playing) return;
            if (!paused && Current != State.Paused) return;
            Time.timeScale = paused ? 0f : 1f;
            Show(pausePanel, paused);
            SetState(paused ? State.Paused : State.Playing);
        }

        public void Resume() => Pause(false);

        /// <summary>Menú de pausa: enciende o apaga las "Ayudas de sigilo" (cono de visión en el suelo).</summary>
        public void ToggleStealthAids()
        {
            StealthAids.Enabled = !StealthAids.Enabled;
            UpdateStealthAidsLabel();
        }

        private void UpdateStealthAidsLabel()
        {
            if (stealthAidsLabel != null) stealthAidsLabel.text = StealthAids.Enabled ? "STEALTH AIDS: ON" : "STEALTH AIDS: OFF";
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetVolume(float v)
        {
            AudioListener.volume = v;
            try { PlayerPrefs.SetFloat(VolumeKey, v); } catch (Exception) { }
        }

        // ---------- Pistas

        public void ShowClue(string title, string body)
        {
            if (Current != State.Playing) return;
            clueTitle.text = title;
            clueBody.text = body;
            Show(cluePanel, true);
            player.InputLocked = true;
            Time.timeScale = 0f;
            SetState(State.Clue);
        }

        private void OnDismiss()
        {
            if (Current != State.Clue) return;
            Show(cluePanel, false);
            Time.timeScale = 1f;
            SetState(State.Playing);
            // La misma pulsación que cierra la pista no debe atacar ni volver a abrirla.
            StartCoroutine(UnlockNextFrame());
        }

        private IEnumerator UnlockNextFrame()
        {
            yield return null;
            if (Current == State.Playing) player.InputLocked = false;
        }

        /// <summary>Botón de pantalla completa del panel de pista (táctil).</summary>
        public void DismissClue() => OnDismiss();

        // ---------- Objetivos y checkpoints

        public void SetObjective(string text)
        {
            if (string.IsNullOrEmpty(text) || objectiveLabel.text == text) return;
            objectiveLabel.text = text;
            objectiveFlash = 1f;
            objectiveUntil = Time.unscaledTime + objectiveShowSeconds + objectiveFadeSeconds;
        }

        public void ReachCheckpoint(Transform point, string objective)
        {
            bool moved = (point.position - checkpointPos).sqrMagnitude > 1f;
            checkpointPos = point.position;
            checkpointRot = point.rotation;
            if (moved)
            {
                CheckpointReached?.Invoke();
                StartCoroutine(Flash("CHECKPOINT", 1.6f));
            }
            SetObjective(objective);
        }

        private void Update()
        {
            // Una frase corta que aparece al cambiar (4 s) y se desvanece; en pausa vuelve a verse (3.12).
            objectiveFlash = Mathf.Max(0f, objectiveFlash - Time.unscaledDeltaTime * 0.5f);
            var c = Color.Lerp(new Color(0.88f, 0.86f, 0.8f, 1f), new Color(1f, 0.9f, 0.6f, 1f), objectiveFlash);
            c.a = Current == State.Paused ? 1f : Mathf.Clamp01((objectiveUntil - Time.unscaledTime) / objectiveFadeSeconds);
            objectiveLabel.color = c;
        }

        // ---------- Muerte y captura

        private void OnDied()
        {
            if (Current != State.Playing) return;
            StartCoroutine(DeathRoutine("YOU DIED", Color.black, lift: false));
        }

        private void OnSearchlightCaught(Searchlight beam) => OnCaptured(beam);

        private void OnCaptured() => OnCaptured(null);

        /// <summary>
        /// La nave te atrapó (POLISH_PASS 3.4): bloqueo, parálisis, forcejeo y, si no te sueltas, elevación. Si el
        /// forcejeo sale bien, se sigue jugando; si no, al último checkpoint.
        /// </summary>
        private void OnCaptured(Searchlight beam)
        {
            if (Current != State.Playing) return;
            Captured?.Invoke();
            if (capture == null)
            {
                // Sin secuencia (escenas viejas): la luz se lo lleva con un fundido de su color.
                StartCoroutine(DeathRoutine("TAKEN", new Color(0.95f, 0.32f, 0.26f), lift: true));
                return;
            }
            StartCoroutine(CaptureRoutine(beam));
        }

        private IEnumerator CaptureRoutine(Searchlight beam)
        {
            SetState(State.Dying);
            if (ufoExposure != null) ufoExposure.enabled = false;
            bool freed = false;
            yield return capture.Run(beam, ok => freed = ok);
            if (freed)
            {
                if (ufoExposure != null) { ufoExposure.ResetExposure(); ufoExposure.enabled = true; }
                SetState(State.Playing);
                yield break;
            }
            // La elevación termina en blanco: de ahí a negro y al checkpoint.
            fade.alpha = 1f;
            fadeImage.color = Color.white;
            yield return FadeTo(1f, 0.5f, Color.black);
            yield return Respawn("TAKEN", Color.black, new Color(0.75f, 0.8f, 0.9f));
        }

        private IEnumerator DeathRoutine(string message, Color fadeColor, bool lift)
        {
            SetState(State.Dying);
            player.InputLocked = true;
            if (ufoExposure != null) ufoExposure.enabled = false;
            yield return new WaitForSeconds(lift ? 0.2f : 1.2f);

            float t = 0f;
            Vector3 start = player.transform.position;
            fadeImage.color = fadeColor;
            while (t < 1f)
            {
                t += Time.deltaTime / (lift ? 1.6f : 0.9f);
                fade.alpha = t;
                if (lift)
                {
                    body.enabled = false;
                    player.transform.position = start + Vector3.up * (t * t * 4f);
                }
                yield return null;
            }
            yield return Respawn(message, fadeColor, lift ? new Color(0.1f, 0.15f, 0.2f) : new Color(0.75f, 0.1f, 0.08f));
        }

        /// <summary>Con la pantalla ya cubierta: mensaje, vuelta al checkpoint, enemigos y reflectores en gracia.</summary>
        private IEnumerator Respawn(string message, Color fadeColor, Color messageColor)
        {
            bigMessage.text = message;
            bigMessage.color = messageColor;
            yield return new WaitForSeconds(healthConfig.respawnDelay);
            if (capture != null) capture.ResetForNewLife();

            body.enabled = false;
            player.transform.SetPositionAndRotation(checkpointPos, checkpointRot);
            body.enabled = true;
            health.Restore();
            foreach (var enemy in EnemyController.Active.ToArray())
                enemy.ResetAfterPlayerRespawn(checkpointPos, healthConfig.respawnEnemyClearRadius, healthConfig.respawnGraceSeconds);
            if (ufoExposure != null) { ufoExposure.ResetExposure(); ufoExposure.enabled = true; }
            Searchlight.SuppressAll(healthConfig.respawnGraceSeconds);

            bigMessage.text = "";
            yield return FadeTo(0f, 1.2f, fadeColor);
            player.InputLocked = false;
            SetState(State.Playing);
        }

        // ---------- Final

        public void TriggerEnding()
        {
            if (Current != State.Playing) return;
            StartCoroutine(EndingRoutine());
        }

        private IEnumerator EndingRoutine()
        {
            SetState(State.Ending);
            player.InputLocked = true;
            if (ufoExposure != null) ufoExposure.enabled = false;
            Show(gameplayHud, false);
            yield return FadeTo(0.82f, 2.5f, Color.black);
            Show(endPanel, true);
            Show(endButtons, false);
            endBody.text = "";
            foreach (var line in endingLines)
            {
                endBody.text += (endBody.text.Length > 0 ? "\n\n" : "") + line;
                yield return new WaitForSeconds(Mathf.Clamp(line.Length * 0.055f, 2.2f, 5.5f));
            }
            yield return FadeTo(1f, 2f, Color.black);
            endBody.text += "\n\n\nTHE HOLLOW\n<size=20>end of the demo</size>";
            yield return new WaitForSeconds(1.5f);
            Show(endButtons, true);
        }

        // ---------- Utilidades

        private void SetState(State s)
        {
            if (s == Current && s != State.Title) return;
            Current = s;
            StateChanged?.Invoke(s);
        }

        private IEnumerator Flash(string text, float seconds)
        {
            bigMessage.text = text;
            bigMessage.color = new Color(1f, 1f, 1f, 0.8f);
            yield return new WaitForSeconds(seconds);
            if (bigMessage.text == text) bigMessage.text = "";
        }

        private IEnumerator FadeTo(float target, float seconds, Color color)
        {
            fadeImage.color = color;
            fade.gameObject.SetActive(true);
            float startA = fade.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
                fade.alpha = Mathf.Lerp(startA, target, t);
                yield return null;
            }
            fade.alpha = target;
        }

        private static void Show(CanvasGroup g, bool on)
        {
            if (g == null) return;
            g.alpha = on ? 1f : 0f;
            g.interactable = on;
            g.blocksRaycasts = on;
        }
    }
}
