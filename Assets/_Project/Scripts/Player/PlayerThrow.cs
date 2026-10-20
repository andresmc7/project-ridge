using ProjectRidge.Items;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Lanzar una piedra (botón OBJETO con la piedra activa): distracción. Cae en el punto pedido y su ruido atrae a los
    /// Lurker allí, lejos del papá. La piedra sale de la mano tras el arranque de la animación.
    /// </summary>
    public class PlayerThrow : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private FatherAnimator animator;
        [SerializeField] private ItemDefinition stoneItem;
        [SerializeField] private StoneProjectile stonePrefab;
        [SerializeField] private Transform hand;
        [Tooltip("Altura de la parábola sobre el punto más alto (mano o destino): salva bordes y matorrales.")]
        [Min(0.5f)] [SerializeField] private float arcHeight = 2.4f;
        [Min(0f)] [SerializeField] private float releaseDelay = 0.42f;
        [Min(0f)] [SerializeField] private float cooldown = 1.1f;

        private float nextThrow;
        private float releaseAt = -1f;
        private Vector3 target;

        public ItemDefinition StoneItem => stoneItem;

        /// <summary>Lanza una piedra hacia el punto (se corrige al suelo real). Devuelve false si no se pudo.</summary>
        public bool ThrowAt(Vector3 point)
        {
            if (controller.InputLocked || health.IsDead || combat.IsBusy || Time.time < nextThrow || releaseAt > 0f) return false;
            if (!inventory.TryRemove(stoneItem))
            {
                interactor.Notify("No stones");
                return false;
            }
            nextThrow = Time.time + cooldown;
            controller.FaceTowards(point);
            target = point;
            // Apunta al suelo real del punto de caída (pendientes, la meseta), no a la altura del papá.
            if (Physics.Raycast(target + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 40f, 1, QueryTriggerInteraction.Ignore))
                target = ground.point;
            releaseAt = Time.time + releaseDelay;
            controller.ActionLocked = true;
            if (animator != null) animator.PlayThrow();
            return true;
        }

        private void Update()
        {
            if (releaseAt < 0f || Time.time < releaseAt) return;
            releaseAt = -1f;
            controller.ActionLocked = false;
            Vector3 origin = hand != null ? hand.position : transform.position + Vector3.up * 1.6f;
            var stone = Instantiate(stonePrefab, origin, Random.rotation);
            Vector3 flat = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
            float d = Mathf.Max(0.5f, flat.magnitude);
            // Tiro parabólico con ápice fijo: sube hasta apex y cae sobre el destino.
            float g = -stone.Gravity;
            float apex = Mathf.Max(origin.y, target.y) + arcHeight;
            float vy = Mathf.Sqrt(2f * g * (apex - origin.y));
            float t = vy / g + Mathf.Sqrt(2f * (apex - target.y) / g);
            stone.Launch(flat.normalized * (d / t) + Vector3.up * vy, gameObject);
        }
    }
}
