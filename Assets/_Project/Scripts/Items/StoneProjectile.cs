using System;
using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Piedra lanzada: vuela en arco y al tocar algo hace un ruido seco que atrae a los Lurker a ese punto.</summary>
    public class StoneProjectile : MonoBehaviour
    {
        [SerializeField] private float gravity = -12f;
        [SerializeField] private float impactNoiseRadius = 10f;
        [SerializeField] private LayerMask hitMask = 1;
        [SerializeField] private float lifeAfterImpact = 4f;

        public static event Action<Vector3> Impacted;
        public float Gravity => gravity;

        private Vector3 velocity;
        private GameObject owner;
        private bool flying;

        public void Launch(Vector3 initialVelocity, GameObject thrower)
        {
            velocity = initialVelocity;
            owner = thrower;
            flying = true;
        }

        private void Update()
        {
            if (!flying) return;
            float dt = Time.deltaTime;
            velocity.y += gravity * dt;
            Vector3 step = velocity * dt;
            if (Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, step.magnitude + 0.05f, hitMask, QueryTriggerInteraction.Ignore))
            {
                flying = false;
                transform.position = hit.point + hit.normal * 0.05f;
                NoiseSystem.Emit(hit.point, impactNoiseRadius, NoiseType.Impact, owner);
                Impacted?.Invoke(hit.point);
                Destroy(gameObject, lifeAfterImpact);
                return;
            }
            transform.position += step;
            transform.Rotate(540f * dt, 210f * dt, 0f, Space.Self);
        }
    }
}
