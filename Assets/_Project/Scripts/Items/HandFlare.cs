using System;
using System.Collections.Generic;
using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>
    /// Bengala de mano colocada en el suelo: mecha de unos segundos (chispas y siseo, aún no atrae a nadie) para
    /// que el papá llegue a un escondite; luego arde con luz roja, humo rojo y ruido periódico que atrae a los
    /// Lurker cercanos, que se quedan mirándola hasta que se apaga.
    /// </summary>
    public class HandFlare : MonoBehaviour
    {
        [SerializeField] private CombatConfig config;
        [SerializeField] private Light glow;
        [SerializeField] private ParticleSystem fuseSparks;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private AudioSource fuseAudio;
        [SerializeField] private AudioSource burnAudio;
        [Tooltip("Punta emisiva (se enciende al arder).")]
        [SerializeField] private Renderer tip;

        private GameObject owner;
        private float igniteAt = float.MaxValue;
        private float burnEnd;
        private float nextNoise;
        private bool burning;
        private bool spent;
        private MaterialPropertyBlock block;

        /// <summary>Bengalas ardiendo ahora (para la IA, el audio y el presupuesto de luces).</summary>
        public static readonly List<HandFlare> Burning = new List<HandFlare>();
        public static event Action<HandFlare> Ignited;

        public bool IsBurning => burning;
        public float LureRadius => config.handFlareLureRadius;

        /// <summary>La bengala ardiendo más cercana dentro de su radio de atracción, o null.</summary>
        public static HandFlare NearestBurning(Vector3 position)
        {
            HandFlare best = null;
            float bestSqr = float.MaxValue;
            foreach (var f in Burning)
            {
                float sqr = (f.transform.position - position).sqrMagnitude;
                if (sqr > f.LureRadius * f.LureRadius || sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = f;
            }
            return best;
        }

        public void Arm(GameObject placedBy)
        {
            owner = placedBy;
            igniteAt = Time.time + config.handFlareFuse;
            SetGlow(0f);
            if (fuseSparks != null) fuseSparks.Play();
            if (fuseAudio != null) fuseAudio.Play();
        }

        private void Update()
        {
            if (spent) return;
            if (!burning)
            {
                if (Time.time >= igniteAt) Ignite();
                return;
            }

            float remaining = burnEnd - Time.time;
            if (Time.time >= nextNoise)
            {
                nextNoise = Time.time + config.handFlareNoiseInterval;
                NoiseSystem.Emit(transform.position, config.handFlareLureRadius, NoiseType.Flare, owner);
            }
            float flicker = 0.82f + 0.18f * Mathf.PerlinNoise(Time.time * 11f, transform.position.x);
            float fade = Mathf.Clamp01(remaining / config.handFlareFadeTime);
            SetGlow(flicker * fade);
            if (burnAudio != null) burnAudio.volume = fade;
            if (remaining <= 0f) Extinguish();
        }

        private void Ignite()
        {
            burning = true;
            burnEnd = Time.time + config.handFlareBurn;
            nextNoise = Time.time;
            if (fuseSparks != null) fuseSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (fuseAudio != null) fuseAudio.Stop();
            if (sparks != null) sparks.Play();
            if (smoke != null) smoke.Play();
            if (burnAudio != null) burnAudio.Play();
            Burning.Add(this);
            Ignited?.Invoke(this);
        }

        private void Extinguish()
        {
            spent = true;
            burning = false;
            Burning.Remove(this);
            SetGlow(0f);
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (burnAudio != null) burnAudio.Stop();
            // El humo se queda flotando un rato antes de desaparecer.
            Destroy(gameObject, smoke != null ? smoke.main.startLifetime.constantMax + 0.5f : 0.5f);
        }

        private void SetGlow(float factor)
        {
            if (glow != null)
            {
                glow.color = config.lightColor;
                glow.range = config.lightRange;
                glow.intensity = config.lightIntensity * factor;
                glow.enabled = factor > 0.01f;
            }
            if (tip != null)
            {
                block ??= new MaterialPropertyBlock();
                tip.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", config.lightColor * (0.2f + 6f * factor));
                tip.SetPropertyBlock(block);
            }
        }

        private void OnDisable() => Burning.Remove(this);
    }
}
