using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Un garrote tirado en el suelo: lo repone con durabilidad completa.</summary>
    public class ClubPickup : Interactable
    {
        public override Kind ActionKind => Kind.PickUp;
        [SerializeField] private InteractionConfig config;

        public override string Prompt => "Take the wooden club";

        public override void Interact(ProjectRidge.Player.PlayerInteractor who)
        {
            if (!who.TryGetComponent(out ProjectRidge.Player.PlayerCombat combat)) return;
            if (combat.HasClub && combat.ClubDurability == combat.ClubMaxDurability)
            {
                who.Notify("Your club is fine");
                return;
            }
            combat.RestoreClub();
            NoiseSystem.Emit(transform.position, config.pickupNoiseRadius, NoiseType.Interaction, who.gameObject);
            who.Notify("New wooden club");
            gameObject.SetActive(false);
        }
    }
}
