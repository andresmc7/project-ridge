using System;
using ProjectRidge.Core;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>Vida mínima para que el ataque del Lurker tenga efecto (M2). Muerte y checkpoint llegan en M4.</summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerHealthConfig config;

        public float Current { get; private set; }
        public float Max => config.maxHealth;
        public bool IsDead => Current <= 0f;

        public event Action<float> Damaged;
        public event Action Died;

        private void Awake() => Current = config.maxHealth;

        public void Restore() => Current = config.maxHealth;

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(config.maxHealth, Current + amount);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount);
            if (IsDead) Died?.Invoke();
        }
    }
}
