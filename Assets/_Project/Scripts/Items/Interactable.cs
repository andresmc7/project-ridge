using System.Collections.Generic;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Base de todo lo que se usa con Interact (recoger, registrar, inspeccionar).</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [Tooltip("Punto al que se mide la distancia (si está vacío, el propio transform).")]
        [SerializeField] private Transform focus;

        public Vector3 FocusPosition => focus != null ? focus.position : transform.position;
        public abstract string Prompt { get; }

        /// <summary>Qué hace el botón ACCIÓN aquí. El orden es la prioridad (POLISH_PASS 3.1): inspeccionar › usar › recoger › registrar.</summary>
        public enum Kind { Inspect, Use, PickUp, Search }
        public virtual Kind ActionKind => Kind.Use;
        public virtual bool CanInteract => isActiveAndEnabled;

        public abstract void Interact(ProjectRidge.Player.PlayerInteractor who);

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);
    }
}
