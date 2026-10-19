using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>Ojos rojos del Lurker: tenues patrullando, más intensos al sospechar, máximos al cazar.</summary>
    public class EnemyStateFeedback : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private EnemyConfig config;
        [SerializeField] private EnemyController enemy;
        [SerializeField] private EnemyPerception perception;
        [SerializeField] private Renderer[] eyes;

        // Se crea perezosamente: tras un domain reload en play mode Awake no vuelve a correr.
        private MaterialPropertyBlock block;

        private void LateUpdate()
        {
            block ??= new MaterialPropertyBlock();
            Color color;
            switch (enemy.State)
            {
                case EnemyState.Chase:
                case EnemyState.Attack:
                    color = config.eyesHunt;
                    break;
                case EnemyState.Investigate:
                case EnemyState.Search:
                    color = config.eyesAlert;
                    break;
                default:
                    color = Color.Lerp(config.eyesCalm, config.eyesAlert, perception.Suspicion);
                    break;
            }

            foreach (var r in eyes)
            {
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColor, color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
