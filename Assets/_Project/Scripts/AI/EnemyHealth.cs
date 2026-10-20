using System;
using ProjectRidge.Core;
using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Vida del Lurker. El garrote y la bengala lo dañan; a 0 queda muerto solo si la config lo permite (en el PoC
    /// nunca: la vida no baja de 1 y el golpe solo aturde).
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private EnemyConfig config;
        [SerializeField] private EnemyController enemy;

        public float Current { get; private set; }

        public event Action<float> Damaged;
        public event Action Died;

        private void Awake() => Current = config.maxHealth;

        public void TakeDamage(float amount)
        {
            if (enemy.IsDead || amount <= 0f) return;
            Current = Mathf.Max(config.canDie ? 0f : 1f, Current - amount);
            Damaged?.Invoke(amount);
            if (Current <= 0f)
            {
                enemy.Die();
                Died?.Invoke();
            }
        }
    }
}
