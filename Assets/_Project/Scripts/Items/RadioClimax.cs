using System;
using System.Collections;
using ProjectRidge.AI;
using ProjectRidge.Core;
using ProjectRidge.Noise;
using ProjectRidge.Player;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>
    /// Clímax: encender la radio de la camioneta inicia una transmisión que hay que aguantar. El ruido de la radio
    /// atrae a los Lurker y el OVNI barre la zona; el papá debe esconderse cerca (arbustos, pasto alto, detrás de la
    /// camioneta). Si sobrevive hasta el final, la luz cae sobre la camioneta y termina el nivel.
    /// </summary>
    public class RadioClimax : Interactable
    {
        [SerializeField] private float duration = 25f;
        [Tooltip("Ruido de la radio (m): llega a los Lurker del campamento.")]
        [SerializeField] private float noiseRadius = 28f;
        [SerializeField] private float noiseInterval = 6f;
        [SerializeField] private PlayerHealth playerHealth;

        public static event Action Started;
        public static event Action Squealed;

        private bool running;
        private bool done;
        private Coroutine routine;

        public bool Running => running;
        public override string Prompt => "Use the radio";
        public override bool CanInteract => base.CanInteract && !running && !done;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        public override void Interact(PlayerInteractor who)
        {
            if (running || done) return;
            if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) return;
            routine = StartCoroutine(Transmission());
        }

        private void OnPlayerDied()
        {
            // Murió durante la transmisión: se puede reintentar desde el checkpoint junto a la camioneta.
            if (routine != null) StopCoroutine(routine);
            running = false;
            routine = null;
        }

        private IEnumerator Transmission()
        {
            running = true;
            Started?.Invoke();
            var flow = GameFlow.Instance;
            var ufo = FindFirstObjectByType<UFOController>();
            float end = Time.time + duration;
            float nextNoise = Time.time;
            while (Time.time < end)
            {
                // Lo atrapó el OVNI o murió: se corta y se puede reintentar desde el checkpoint.
                if (flow != null && flow.Current != GameFlow.State.Playing && flow.Current != GameFlow.State.Paused)
                {
                    running = false;
                    routine = null;
                    yield break;
                }
                int left = Mathf.CeilToInt(end - Time.time);
                flow?.SetObjective("Stay hidden. Transmission: " + left + " s");
                if (Time.time >= nextNoise)
                {
                    nextNoise = Time.time + noiseInterval;
                    NoiseSystem.Emit(transform.position, noiseRadius, NoiseType.Interaction, gameObject);
                    Squealed?.Invoke();
                }
                if (ufo != null) ufo.Track(transform.position + new Vector3(Mathf.Sin(Time.time * 0.6f) * 6f, 0f, Mathf.Cos(Time.time * 0.45f) * 6f));
                yield return new WaitForSeconds(0.5f);
            }
            running = false;
            done = true;
            // La luz encuentra la camioneta: final.
            if (ufo != null) ufo.Track(transform.position);
            flow?.TriggerEnding();
        }
    }
}
