using System;
using ProjectRidge.Items;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>Elige el interactuable más cercano en rango y lo usa con la acción Interact.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerInventory inventory;

        public PlayerInventory Inventory => inventory;
        public Interactable Current { get; private set; }

        private PlayerController controller;

        /// <summary>Mensaje breve para el HUD ("+1 Medkit").</summary>
        public event Action<string> Notified;

        public void Notify(string message) => Notified?.Invoke(message);

        private void OnEnable() => input.InteractPressed += OnInteract;
        private void OnDisable() => input.InteractPressed -= OnInteract;

        private void Update() => Current = FindNearest();

        private Interactable FindNearest()
        {
            Interactable best = null;
            float bestSqr = config.radius * config.radius;
            Vector3 p = transform.position;
            foreach (var it in Interactable.All)
            {
                if (!it.CanInteract) continue;
                Vector3 d = it.FocusPosition - p;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = it; }
            }
            return best;
        }

        // Se recalcula al pulsar: el input se procesa antes del Update de este frame.
        private void OnInteract()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (controller != null && controller.InputLocked) return;
            Current = FindNearest();
            if (Current != null) Current.Interact(this);
        }
    }
}
