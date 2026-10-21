using ProjectRidge.AI;
using ProjectRidge.Items;
using ProjectRidge.Player;
using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Hápticos del juego (POLISH_PASS P8): fuerte al recibir daño y al ser detectado (un Lurker empieza a cazar), leve
    /// al conectar un golpe, al disparar la bengala, al recoger algo y en el checkpoint. La captura del OVNI usa su
    /// zumbido continuo aparte (CaptureSequence). En el editor solo cuenta (Haptics.ImpactCount).
    /// </summary>
    public class HapticEvents : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerInventory inventory;

        private readonly System.Collections.Generic.Dictionary<ItemDefinition, int> counts = new System.Collections.Generic.Dictionary<ItemDefinition, int>();
        private readonly System.Collections.Generic.HashSet<EnemyController> hooked = new System.Collections.Generic.HashSet<EnemyController>();

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            combat.ClubHit += OnClubHit;
            combat.Fired += Haptics.Light;
            inventory.Changed += OnInventory;
        }

        private void Start()
        {
            if (GameFlow.Instance != null) GameFlow.Instance.CheckpointReached += Haptics.Light;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            combat.ClubHit -= OnClubHit;
            combat.Fired -= Haptics.Light;
            inventory.Changed -= OnInventory;
            if (GameFlow.Instance != null) GameFlow.Instance.CheckpointReached -= Haptics.Light;
            foreach (var e in hooked) if (e != null) e.StateChanged -= OnEnemyState;
            hooked.Clear();
        }

        private void Update()
        {
            // Los Lurker pueden activarse después (salas): se enganchan al aparecer.
            foreach (var e in EnemyController.Active)
                if (hooked.Add(e)) e.StateChanged += OnEnemyState;
        }

        private void OnDamaged(float amount) => Haptics.Heavy();
        private void OnClubHit(Vector3 at) => Haptics.Light();
        private void OnEnemyState(EnemyState s) { if (s == EnemyState.Chase) Haptics.Heavy(); }

        private void OnInventory(ItemDefinition item, int count)
        {
            counts.TryGetValue(item, out int before);
            counts[item] = count;
            if (count > before) Haptics.Light();
        }
    }
}
