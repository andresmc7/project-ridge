using System;
using System.Text;
using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Caja/mochila que se registra una vez con Interact. Más ruidosa que recoger un objeto.</summary>
    public class SearchableContainer : Interactable
    {
        [Serializable]
        public struct Entry
        {
            public ItemDefinition item;
            [Min(1)] public int amount;
        }

        [SerializeField] private InteractionConfig config;
        [SerializeField] private string displayName = "Crate";
        [SerializeField] private Entry[] contents;
        [Tooltip("Opcional: se gira al abrir (tapa).")]
        [SerializeField] private Transform lid;
        [SerializeField] private Vector3 lidOpenEuler = new Vector3(-100f, 0f, 0f);

        private bool searched;

        public override string Prompt => "Search " + displayName;
        public override bool CanInteract => base.CanInteract && !searched;

        public override void Interact(ProjectRidge.Player.PlayerInteractor who)
        {
            searched = true;
            NoiseSystem.Emit(transform.position, config.searchNoiseRadius, NoiseType.Interaction, who.gameObject);
            var sb = new StringBuilder();
            foreach (var e in contents)
            {
                int taken = who.Inventory.Add(e.item, e.amount);
                if (taken > 0) sb.Append(sb.Length > 0 ? "   " : "").Append('+').Append(taken).Append(' ').Append(e.item.displayName);
            }
            who.Notify(sb.Length > 0 ? sb.ToString() : "Nothing useful");
            if (lid != null) lid.localRotation = Quaternion.Euler(lidOpenEuler);
        }
    }
}
