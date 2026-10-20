using ProjectRidge.Items;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Usar bengala (Q / RB / botón FLARE): el papá se agacha y la deja en el suelo delante de él. Arde a los pocos
    /// segundos y atrae a los Lurker del área: tiempo para llegar a un escondite y pasar mientras la miran.
    /// </summary>
    public class PlayerFlareTool : MonoBehaviour
    {
        [SerializeField] private CombatConfig config;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private FatherAnimator animator;
        [SerializeField] private ItemDefinition flareItem;
        [SerializeField] private HandFlare prefab;
        [Tooltip("Capas del suelo donde se apoya la bengala.")]
        [SerializeField] private LayerMask groundMask = 1;

        private float placeAt = -1f;

        private void OnEnable() => input.FirePressed += OnUse;
        private void OnDisable() => input.FirePressed -= OnUse;

        private void OnUse()
        {
            if (controller.InputLocked || controller.ActionLocked || health.IsDead || combat.IsBusy || placeAt > 0f) return;
            if (!inventory.TryRemove(flareItem))
            {
                interactor.Notify("No flares");
                return;
            }
            placeAt = Time.time + config.handFlarePlaceDelay;
            if (animator != null) animator.PlayPlace();
            interactor.Notify("Flare lit. Get to cover!");
        }

        private void Update()
        {
            if (placeAt < 0f || Time.time < placeAt) return;
            placeAt = -1f;
            Vector3 p = transform.position + transform.forward * config.handFlarePlaceDistance;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, groundMask, QueryTriggerInteraction.Ignore))
                p = hit.point;
            var flare = Instantiate(prefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            flare.Arm(gameObject);
        }
    }
}
