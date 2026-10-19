using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>Sigue la posición de un objetivo (sin rotar). Para emisores de partículas alrededor del jugador.</summary>
    public class FollowTarget : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset;

        private void LateUpdate()
        {
            if (target != null) transform.position = target.position + offset;
        }
    }
}
