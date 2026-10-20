using System;
using System.Collections;
using System.Collections.Generic;
using ProjectRidge.AI;
using ProjectRidge.CameraRig;
using ProjectRidge.Core;
using ProjectRidge.Items;
using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Garrote (Attack): arranque → ventana activa → recuperación, un golpe por enemigo por swing, durabilidad y rotura.
    /// Sin garrote, Attack fabrica uno con chatarra. Pistola de bengalas (Fire, o Aim + Attack): a quemarropa aturde;
    /// si no, dispara una bengala que arde con luz y ruido y atrae a los enemigos.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        public enum SwingPhase { None, Windup, Active, Recovery, Crafting }

        [SerializeField] private CombatConfig config;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private ItemDefinition flareItem;
        [SerializeField] private ItemDefinition scrapItem;
        [SerializeField] private FlareProjectile flarePrefab;
        [Tooltip("Desde dónde sale la bengala (mano).")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask enemyMask = 1 << 10;
        [Tooltip("Salpicadura al conectar (icor oscuro del Lurker).")]
        [SerializeField] private ParticleSystem hitFx;

        private readonly HashSet<EnemyController> hitThisSwing = new HashSet<EnemyController>();
        private readonly Collider[] overlap = new Collider[16];
        private float phaseEnd;
        private float phaseDuration;
        private float nextFire;

        public SwingPhase Phase { get; private set; }
        /// <summary>Avance 0–1 dentro de la fase actual (para la animación).</summary>
        public float PhaseT => phaseDuration > 0f ? Mathf.Clamp01(1f - (phaseEnd - Time.time) / phaseDuration) : 0f;
        public int ClubDurability { get; private set; }
        public int ClubMaxDurability => config.clubDurability;
        public bool HasClub => ClubDurability > 0;
        public bool IsBusy => Phase != SwingPhase.None;

        public event Action Swung;
        public event Action<Vector3> ClubHit;
        public event Action ClubBroke;
        public event Action Crafted;
        public event Action Fired;
        public event Action DryFire;

        private void Awake() => ClubDurability = config.clubDurability;

        private void OnEnable()
        {
            input.AttackPressed += OnAttack;
            if (config.flareGun) input.FirePressed += TryFire;
        }

        private void OnDisable()
        {
            input.AttackPressed -= OnAttack;
            input.FirePressed -= TryFire;
            controller.SpeedMultiplier = 1f;
            controller.InputLocked = false;
            Phase = SwingPhase.None;
        }

        private void OnAttack()
        {
            if (controller.InputLocked) return;
            if (input.AimHeld && config.flareGun) { TryFire(); return; }
            if (health.IsDead || IsBusy) return;
            if (!HasClub) { TryCraft(); return; }

            var target = NearestEnemy(config.clubRange + 1.2f, config.clubArc + 60f);
            if (target != null) controller.FaceTowards(target.transform.position);
            hitThisSwing.Clear();
            Phase = SwingPhase.Windup;
            phaseEnd = Time.time + (phaseDuration = config.clubWindup);
            controller.SpeedMultiplier = config.swingMoveFactor;
            Swung?.Invoke();
        }

        private void Update()
        {
            switch (Phase)
            {
                case SwingPhase.Windup:
                    if (Time.time >= phaseEnd)
                    {
                        Phase = SwingPhase.Active;
                        phaseEnd = Time.time + (phaseDuration = config.clubActive);
                        controller.AddKnockback(transform.forward * config.clubLunge); // el peso del cuerpo va detrás del garrote
                    }
                    break;
                case SwingPhase.Active:
                    DetectHits();
                    if (Time.time >= phaseEnd) { Phase = SwingPhase.Recovery; phaseEnd = Time.time + (phaseDuration = config.clubRecovery); }
                    break;
                case SwingPhase.Recovery:
                    if (Time.time >= phaseEnd) EndAction();
                    break;
                case SwingPhase.Crafting:
                    if (Time.time >= phaseEnd) FinishCraft();
                    break;
            }
        }

        private void EndAction()
        {
            Phase = SwingPhase.None;
            controller.SpeedMultiplier = 1f;
            controller.InputLocked = false;
        }

        // ---------- Garrote

        private void DetectHits()
        {
            if (!HasClub) return;
            Vector3 center = transform.position + Vector3.up * 1f;
            int n = Physics.OverlapSphereNonAlloc(center, config.clubRange, overlap, enemyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var enemy = overlap[i].GetComponentInParent<EnemyController>();
                if (enemy == null || enemy.IsDead || hitThisSwing.Contains(enemy)) continue;
                Vector3 to = enemy.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > config.clubArc * 0.5f) continue;

                hitThisSwing.Add(enemy); // un golpe por enemigo por swing
                float damage = config.clubDamage * (enemy.IsHunting ? 1f : config.sneakMultiplier);
                bool sneak = !enemy.IsHunting;
                enemy.Interrupt(config.clubStagger, transform.position, config.clubKnockback);
                Vector3 hitDir = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
                var reaction = enemy.GetComponentInChildren<HitReaction>();
                if (reaction != null) reaction.Impulse(hitDir, config.clubHitReaction * (sneak ? config.sneakMultiplier * 0.75f : 1f));
                CameraShake.Kick(hitDir, config.clubCameraKick * (sneak ? 1.4f : 1f));
                if (enemy.TryGetComponent(out EnemyHealth eh)) eh.TakeDamage(damage);
                Vector3 impact = enemy.transform.position + Vector3.up * 1.3f;
                NoiseSystem.Emit(impact, config.clubHitNoise, NoiseType.Impact, gameObject);
                ClubHit?.Invoke(impact);
                if (hitFx != null)
                {
                    var fx = Instantiate(hitFx, impact, Quaternion.LookRotation(hitDir + Vector3.up * 0.3f));
                    Destroy(fx.gameObject, 2f);
                }
                HitStop.Trigger(config.hitStop, config.hitStopScale);

                if (--ClubDurability <= 0)
                {
                    NoiseSystem.Emit(transform.position, config.clubBreakNoise, NoiseType.Breakage, gameObject);
                    interactor.Notify("The club broke");
                    ClubBroke?.Invoke();
                    return;
                }
            }
        }

        private void TryCraft()
        {
            if (inventory.Count(scrapItem) < config.craftScrapCost)
            {
                interactor.Notify("Need " + config.craftScrapCost + " scrap to make a club");
                return;
            }
            Phase = SwingPhase.Crafting;
            phaseEnd = Time.time + (phaseDuration = config.craftTime);
            controller.InputLocked = true;
            NoiseSystem.Emit(transform.position, config.craftNoise, NoiseType.Interaction, gameObject);
            interactor.Notify("Making a club...");
        }

        private void FinishCraft()
        {
            EndAction();
            if (!inventory.TryRemove(scrapItem, config.craftScrapCost)) return;
            ClubDurability = config.clubDurability;
            interactor.Notify("New club  (-" + config.craftScrapCost + " Scrap)");
            Crafted?.Invoke();
        }

        /// <summary>Repone el garrote (p. ej. al recoger uno nuevo).</summary>
        public void RestoreClub()
        {
            ClubDurability = config.clubDurability;
            Crafted?.Invoke();
        }

        // ---------- Pistola de bengalas

        private void TryFire()
        {
            if (controller.InputLocked) return;
            if (health.IsDead || IsBusy || Time.time < nextFire) return;
            if (!inventory.TryRemove(flareItem))
            {
                interactor.Notify("No flares");
                DryFire?.Invoke();
                return;
            }
            nextFire = Time.time + config.fireCooldown;

            var target = NearestEnemy(config.autoAimRange, config.autoAimAngle);
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.3f;
            NoiseSystem.Emit(transform.position, config.shotNoise, NoiseType.Flare, gameObject);
            Fired?.Invoke();

            var flare = Instantiate(flarePrefab, origin, Quaternion.identity);
            if (target != null)
            {
                controller.FaceTowards(target.transform.position);
                float distance = Vector3.Distance(Flat(transform.position), Flat(target.transform.position));
                if (distance <= config.pointBlankRange)
                {
                    // A quemarropa: aturde y la bengala cae ardiendo a sus pies.
                    target.Interrupt(config.pointBlankStun, transform.position, 4f);
                    if (target.TryGetComponent(out EnemyHealth eh)) eh.TakeDamage(config.pointBlankDamage);
                    flare.Ignite(target.transform.position + Vector3.up * 0.05f, gameObject);
                    return;
                }
                flare.Launch(BallisticVelocity(origin, target.transform.position + Vector3.up * 1.2f), gameObject);
                return;
            }

            Vector3 forward = controller.DesiredDirection.sqrMagnitude > 0.1f ? controller.DesiredDirection : transform.forward;
            controller.FaceTowards(transform.position + forward);
            flare.Launch(BallisticVelocity(origin, transform.position + forward * config.freeShotDistance), gameObject);
        }

        /// <summary>Velocidad para llegar al punto con velocidad horizontal fija y la gravedad de la bengala.</summary>
        private Vector3 BallisticVelocity(Vector3 from, Vector3 to)
        {
            Vector3 flat = Flat(to) - Flat(from);
            float d = Mathf.Max(0.5f, flat.magnitude);
            float t = d / config.flareSpeed;
            float vy = (to.y - from.y - 0.5f * config.flareGravity * t * t) / t;
            return flat.normalized * config.flareSpeed + Vector3.up * vy;
        }

        private EnemyController NearestEnemy(float range, float arc)
        {
            EnemyController best = null;
            float bestSqr = range * range;
            Vector3 facing = controller.DesiredDirection.sqrMagnitude > 0.1f ? controller.DesiredDirection : transform.forward;
            foreach (var e in EnemyController.Active)
            {
                if (e.IsDead) continue;
                Vector3 to = e.transform.position - transform.position;
                to.y = 0f;
                float sqr = to.sqrMagnitude;
                if (sqr > bestSqr) continue;
                if (sqr > 1f && Vector3.Angle(facing, to) > arc * 0.5f) continue;
                bestSqr = sqr;
                best = e;
            }
            return best;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
