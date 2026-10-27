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
    /// atrae a los Lurker, los reflectores cercanos aceleran y se encienden los del clímax; el papá debe esconderse
    /// en el pasto alrededor de la camioneta. Si sobrevive, la luz cae sobre la camioneta y termina el nivel.
    /// </summary>
    public class RadioClimax : Interactable
    {
        [SerializeField] private float duration = 25f;
        [Tooltip("Ruido de la radio (m): llega a los Lurker del campamento.")]
        [SerializeField] private float noiseRadius = 28f;
        [SerializeField] private float noiseInterval = 6f;
        [Tooltip("Segundos de estática antes del primer chillido: tiempo para llegar al pasto antes de que los Lurker giren hacia la radio (si no, el que está cerca ve al papá parado junto a ella).")]
        [Min(0f)] [SerializeField] private float firstSquealDelay = 3f;
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("Reflectores que se encienden solo durante la transmisión.")]
        [SerializeField] private Searchlight[] climaxLights;
        [Tooltip("Los reflectores a esta distancia aceleran su barrido.")]
        [Min(0f)] [SerializeField] private float rushRadius = 30f;
        [Min(1f)] [SerializeField] private float rushSpeed = 1.45f;

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
            SetRush(false);
            routine = null;
        }

        private void SetRush(bool on)
        {
            if (climaxLights != null) foreach (var s in climaxLights) if (s != null) s.Activate(on);
            foreach (var s in Searchlight.Active)
                if (Vector3.Distance(s.Spot, transform.position) <= rushRadius) s.SpeedScale = on ? rushSpeed : 1f;
        }

        private IEnumerator Transmission()
        {
            running = true;
            Started?.Invoke();
            var flow = GameFlow.Instance;
            SetRush(true);
            float end = Time.time + duration;
            float nextNoise = Time.time + firstSquealDelay;
            while (Time.time < end)
            {
                // Lo atrapó el OVNI o murió: se corta y se puede reintentar desde el checkpoint.
                if (flow != null && flow.Current != GameFlow.State.Playing && flow.Current != GameFlow.State.Paused)
                {
                    running = false;
                    routine = null;
                    SetRush(false);
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
                yield return new WaitForSeconds(0.5f);
            }
            running = false;
            done = true;
            // La luz encuentra la camioneta: final.
            SetRush(false);
            Searchlight nearest = null;
            foreach (var s in Searchlight.Active)
                if (nearest == null || (s.Spot - transform.position).sqrMagnitude < (nearest.Spot - transform.position).sqrMagnitude) nearest = s;
            if (nearest != null) nearest.Override(transform.position);
            flow?.TriggerEnding();
        }
    }
}
