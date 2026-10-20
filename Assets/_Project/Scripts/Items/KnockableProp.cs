using System;
using ProjectRidge.AI;
using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>
    /// Objeto bajo y suelto (rama, lata, cubeta, cajita): quieto hasta que algo lo atraviesa. Un Lurker siempre lo
    /// manda volando; el papá solo si va rápido (y entonces hace ruido). Nada atraviesa objetos sin reacción.
    /// Necesita un Rigidbody (cinemático al inicio) y un trigger un poco más grande que su colisión.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KnockableProp : MonoBehaviour
    {
        public enum Surface { Wood, Metal }

        [SerializeField] private KnockableConfig config;
        [SerializeField] private Surface surface = Surface.Wood;

        private Rigidbody body;
        private float readyAt;

        /// <summary>Posición, material, fuerza 0–1 (para el audio).</summary>
        public static event Action<Vector3, Surface, float> Knocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void IgnorePlayerAndEnemies()
        {
            // Los escombros no frenan al papá ni a los Lurker: solo reaccionan (también va en la matriz del proyecto).
            int debris = LayerMask.NameToLayer("Debris");
            if (debris < 0) return;
            foreach (var other in new[] { "Player", "Enemy", "TallGrass" })
            {
                int l = LayerMask.NameToLayer(other);
                if (l >= 0) Physics.IgnoreLayerCollision(debris, l, true);
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
        }

        private void OnTriggerEnter(Collider other) => TryKnock(other);
        private void OnTriggerStay(Collider other) => TryKnock(other);

        private void TryKnock(Collider other)
        {
            if (Time.time < readyAt) return;
            Vector3 velocity;
            bool byPlayer = false;
            var enemy = other.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                if (enemy.IsDead || !enemy.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) return;
                velocity = agent.velocity;
                if (velocity.sqrMagnitude < 0.04f) return;
            }
            else if (other is CharacterController cc)
            {
                velocity = cc.velocity;
                velocity.y = 0f;
                if (velocity.magnitude < config.playerKickSpeed) return;
                byPlayer = true;
            }
            else return;

            readyAt = Time.time + config.cooldown;
            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            Vector3 dir = (away.normalized * 0.6f + velocity.normalized * 0.4f).normalized;
            float speed = Mathf.Clamp(velocity.magnitude * config.impulsePerSpeed, config.minImpulse, config.maxImpulse);
            body.isKinematic = false;
            body.linearVelocity = dir * speed + Vector3.up * config.upward;
            body.angularVelocity = UnityEngine.Random.onUnitSphere * config.spin;
            Knocked?.Invoke(transform.position, surface, Mathf.InverseLerp(config.minImpulse, config.maxImpulse, speed));
            if (byPlayer) NoiseSystem.Emit(transform.position, config.playerNoiseRadius, NoiseType.Impact, other.gameObject);
        }
    }
}
