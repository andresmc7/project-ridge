using ProjectRidge.Items;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>Usar un botiquín con la acción Heal.</summary>
    public class PlayerHealing : MonoBehaviour
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private ItemDefinition medkit;

        public event System.Action Healed;

        private void OnEnable() => input.HealPressed += OnHeal;
        private void OnDisable() => input.HealPressed -= OnHeal;

        private void OnHeal()
        {
            if (health.IsDead || GetComponent<PlayerController>().InputLocked) return;
            if (health.Current >= health.Max) { interactor.Notify("Health is full"); return; }
            if (!inventory.TryRemove(medkit)) { interactor.Notify("No medkits"); return; }
            health.Heal(config.medkitHeal);
            Healed?.Invoke();
            interactor.Notify("Used Medkit");
        }
    }
}
