using System;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Detecta si el jugador está dentro de un volumen de pasto alto (triggers en la capa TallGrass).
    /// En M1 solo detecta; la percepción (M2) decidirá qué efecto tiene.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TallGrassDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask tallGrassMask;

        private readonly Collider[] hits = new Collider[4];
        private CharacterController controller;

        public bool IsInTallGrass { get; private set; }
        public event Action<bool> Changed;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void FixedUpdate()
        {
            float radius = controller.radius;
            Vector3 origin = transform.position + controller.center;
            Vector3 half = Vector3.up * Mathf.Max(0f, controller.height * 0.5f - radius);
            int count = Physics.OverlapCapsuleNonAlloc(origin - half, origin + half, radius, hits, tallGrassMask, QueryTriggerInteraction.Collide);

            bool inside = count > 0;
            if (inside == IsInTallGrass) return;
            IsInTallGrass = inside;
            Changed?.Invoke(inside);
        }
    }
}
