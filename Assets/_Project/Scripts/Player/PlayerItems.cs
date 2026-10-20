using System;
using ProjectRidge.AI;
using ProjectRidge.Items;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Botón OBJETO (POLISH_PASS 3.1): un objeto activo (piedra o bengala) con su cantidad.
    /// - Tocar (Q / RB): lo lanza al objetivo inteligente: el Lurker más relevante delante (la piedra cae más allá de
    ///   él, para alejarlo; la bengala a quemarropa lo aturde) o, si no hay, unos metros hacia donde mira el papá.
    /// - Mantener y arrastrar (táctil): apuntar con un arco visible; soltar lanza; volver al botón cancela.
    /// - Cambiar de objeto: Tab / LB / deslizar sobre el botón.
    /// </summary>
    public class PlayerItems : MonoBehaviour
    {
        [SerializeField] private CombatConfig config;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerThrow thrower;
        [SerializeField] private PlayerCombat combat;
        [Tooltip("Objetos que puede tener activos el botón, en el orden en que se recorren.")]
        [SerializeField] private ItemDefinition[] items;
        [Tooltip("Arco de puntería (se muestra solo al apuntar en táctil).")]
        [SerializeField] private LineRenderer aimArc;
        [SerializeField] private ItemDefinition flareItem;

        private int active;

        public ItemDefinition Active => items != null && items.Length > 0 ? items[active] : null;
        public int ActiveCount => inventory.Count(Active);
        public bool Aiming { get; private set; }
        public Vector3 AimPoint { get; private set; }

        public event Action<ItemDefinition> ActiveChanged;
        public event Action<ItemDefinition> Used;

        private void OnEnable()
        {
            input.ObjectPressed += UseSmart;
            input.CycleObjectPressed += Cycle;
            inventory.Changed += OnInventoryChanged;
        }

        private void OnDisable()
        {
            input.ObjectPressed -= UseSmart;
            input.CycleObjectPressed -= Cycle;
            inventory.Changed -= OnInventoryChanged;
            CancelAim();
        }

        private void OnInventoryChanged(ItemDefinition item, int count)
        {
            // Recoger algo con el activo vacío lo vuelve activo; vaciar el activo salta al siguiente con cantidad.
            if (ActiveCount == 0 && count > 0) Select(Array.IndexOf(items, item));
            else if (ActiveCount == 0) Cycle();
        }

        /// <summary>Siguiente objeto con cantidad (si ninguno tiene, el siguiente de la lista).</summary>
        public void Cycle()
        {
            if (items == null || items.Length == 0) return;
            for (int i = 1; i <= items.Length; i++)
            {
                int k = (active + i) % items.Length;
                if (inventory.Count(items[k]) > 0) { Select(k); return; }
            }
        }

        private void Select(int index)
        {
            if (index < 0 || index == active) return;
            active = index;
            ActiveChanged?.Invoke(Active);
        }

        /// <summary>Tocar el botón: lanzar al objetivo inteligente.</summary>
        public void UseSmart()
        {
            if (Active == null || ActiveCount == 0) return;
            UseAt(SmartTarget());
        }

        /// <summary>Punto al que iría un toque ahora mismo (también lo usa el HUD).</summary>
        public Vector3 SmartTarget()
        {
            Vector3 p = transform.position;
            Vector3 facing = controller.DesiredDirection.sqrMagnitude > 0.1f ? controller.DesiredDirection : transform.forward;
            var lurker = RelevantLurker(p, facing);
            if (lurker != null)
            {
                Vector3 to = Flat(lurker.transform.position - p);
                float d = to.magnitude;
                if (Active == flareItem) return lurker.transform.position;
                Vector3 dir = d > 0.01f ? to / d : facing;
                return Ground(p + dir * Mathf.Min(config.objectMaxRange, d + config.stoneOvershoot));
            }
            return Ground(p + facing * config.objectDefaultRange);
        }

        // ---------- Apuntar (táctil)

        /// <summary>Empieza a apuntar (mantener el botón).</summary>
        public void BeginAim()
        {
            if (Active == null || ActiveCount == 0) return;
            Aiming = true;
            AimPoint = SmartTarget();
            DrawArc();
        }

        /// <summary>Arrastre en pantalla desde el botón (pt): dirección relativa a la cámara y alcance por longitud.</summary>
        public void UpdateAim(Vector2 drag)
        {
            if (!Aiming) return;
            var cam = Camera.main;
            Vector3 fwd = cam != null ? Flat(cam.transform.forward).normalized : Vector3.forward;
            Vector3 right = cam != null ? Flat(cam.transform.right).normalized : Vector3.right;
            Vector3 dir = (fwd * drag.y + right * drag.x);
            if (dir.sqrMagnitude < 0.0001f) return;
            float range = Mathf.Lerp(config.aimMinRange, config.objectMaxRange, Mathf.Clamp01(drag.magnitude / config.aimDragForMaxRange));
            AimPoint = Ground(transform.position + dir.normalized * range);
            DrawArc();
        }

        /// <summary>Soltar: lanza al punto apuntado.</summary>
        public void ReleaseAim()
        {
            if (!Aiming) return;
            Aiming = false;
            if (aimArc != null) aimArc.enabled = false;
            UseAt(AimPoint);
        }

        /// <summary>Volver al botón: cancela sin gastar nada.</summary>
        public void CancelAim()
        {
            Aiming = false;
            if (aimArc != null) aimArc.enabled = false;
        }

        // ---------- Lanzamiento

        private void UseAt(Vector3 point)
        {
            bool ok = Active == flareItem ? combat.FireFlareAt(point) : thrower.ThrowAt(point);
            if (ok) Used?.Invoke(Active);
        }

        private EnemyController RelevantLurker(Vector3 p, Vector3 facing)
        {
            EnemyController best = null;
            float bestScore = float.MaxValue;
            foreach (var e in EnemyController.Active)
            {
                if (e.IsDead || e.IsDown) continue;
                Vector3 to = Flat(e.transform.position - p);
                float d = to.magnitude;
                if (d > config.objectMaxRange) continue;
                if (d > 1f && Vector3.Angle(facing, to) > config.objectAheadAngle * 0.5f) continue;
                // Más relevante: el que caza y el más cercano.
                float score = d * (e.IsHunting ? 0.5f : 1f);
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        private void DrawArc()
        {
            if (aimArc == null) return;
            aimArc.enabled = true;
            Vector3 a = transform.position + Vector3.up * 1.5f, b = AimPoint;
            float h = Mathf.Max(a.y, b.y) + 1.5f;
            int n = aimArc.positionCount = 16;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                Vector3 q = Vector3.Lerp(a, b, t);
                q.y = Mathf.Lerp(a.y, b.y, t) + 4f * t * (1f - t) * (h - Mathf.Lerp(a.y, b.y, t));
                aimArc.SetPosition(i, q);
            }
        }

        private static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, 1, QueryTriggerInteraction.Ignore))
                return hit.point;
            return p;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
