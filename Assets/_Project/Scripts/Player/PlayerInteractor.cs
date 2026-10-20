using System;
using ProjectRidge.Items;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Elige el interactuable en rango por prioridad (inspeccionar › usar › recoger › registrar) y, a igual prioridad,
    /// el más cercano. Lo usa el botón ACCIÓN a través de PlayerActions.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private PlayerInventory inventory;

        public PlayerInventory Inventory => inventory;
        public Interactable Current { get; private set; }

        private PlayerController controller;

        /// <summary>Mensaje breve para el HUD ("+1 Medkit").</summary>
        public event Action<string> Notified;
        /// <summary>Se usó un interactuable (la animación elige recoger, registrar o inspeccionar).</summary>
        public event Action<Interactable> Interacted;

        public void Notify(string message) => Notified?.Invoke(message);

        private void Update() => Current = FindBest();

        private Interactable FindBest()
        {
            Interactable best = null;
            float radiusSqr = config.radius * config.radius, bestSqr = float.MaxValue;
            Vector3 p = transform.position;
            foreach (var it in Interactable.All)
            {
                if (!it.CanInteract) continue;
                Vector3 d = it.FocusPosition - p;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr > radiusSqr) continue;
                if (best == null || it.ActionKind < best.ActionKind || (it.ActionKind == best.ActionKind && sqr < bestSqr))
                {
                    best = it;
                    bestSqr = sqr;
                }
            }
            return best;
        }

        /// <summary>Usa el interactuable actual (se recalcula: el input llega antes del Update de este frame).</summary>
        public bool UseCurrent()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (controller != null && controller.InputLocked) return false;
            Current = FindBest();
            if (Current == null) return false;
            var used = Current;
            used.Interact(this);
            Interacted?.Invoke(used);
            return true;
        }

        /// <summary>El interactuable que ganaría ahora mismo (para el ícono del botón ACCIÓN).</summary>
        public Interactable Peek() => FindBest();
    }
}
