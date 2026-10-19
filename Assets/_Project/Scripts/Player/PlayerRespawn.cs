using System.Collections;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Respawn mínimo para el playable: al morir, tras un retraso, el papá vuelve al punto de inicio con la vida llena.
    /// M4 lo reemplaza por el flujo de muerte + checkpoints del spec.
    /// </summary>
    public class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] private PlayerHealthConfig config;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private CharacterController body;
        [SerializeField] private Transform spawnPoint;

        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied() => StartCoroutine(RespawnAfterDelay());

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(config.respawnDelay);
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            body.enabled = true;
            health.Restore();
            foreach (var enemy in ProjectRidge.AI.EnemyController.Active)
                enemy.ResetAfterPlayerRespawn(spawnPoint.position, config.respawnEnemyClearRadius, config.respawnGraceSeconds);
        }
    }
}
