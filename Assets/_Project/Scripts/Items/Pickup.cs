using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Objeto visible que se recoge con Interact. Hace un poco de ruido.</summary>
    public class Pickup : Interactable
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private ItemDefinition item;
        [Min(1)] [SerializeField] private int amount = 1;

        public ItemDefinition Item => item;
        public override string Prompt => "Pick up " + item.displayName + (amount > 1 ? " x" + amount : "");

        public override void Interact(ProjectRidge.Player.PlayerInteractor who)
        {
            int taken = who.Inventory.Add(item, amount);
            if (taken <= 0)
            {
                who.Notify("Can't carry more " + item.displayName);
                return;
            }
            NoiseSystem.Emit(transform.position, config.pickupNoiseRadius, NoiseType.Interaction, who.gameObject);
            who.Notify("+" + taken + " " + item.displayName);
            amount -= taken;
            if (amount <= 0) gameObject.SetActive(false);
        }
    }
}
