using System;
using ProjectRidge.Items;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Conecta el juego con el Animator humanoide del papá (clips de la Universal Animation Library):
    /// locomoción por velocidad real, agachado, linterna en la mano izquierda (capa de brazo), golpe, disparo,
    /// lanzar, recoger, registrar, curarse, fabricar, reacción al daño, muerte y levantarse.
    /// Además: pasos detectados por la altura de los pies, la luz de la linterna sigue la mano con suavizado,
    /// y la cabeza mira a lo interesante cercano (IK).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class FatherAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerHealing healing;
        [SerializeField] private FlashlightController flashlight;
        [Tooltip("Luz de la linterna: se coloca en la mano y apunta hacia adelante con suavizado.")]
        [SerializeField] private Transform flashlightLight;
        [SerializeField] private GameObject club;
        [Tooltip("Funda del garrote en la mochila (el garrote se cuelga aquí cuando no está en la mano).")]
        [SerializeField] private Transform clubSheath;
        [SerializeField] private GameObject flareGun;
        [SerializeField] private GameObject torchProp;

        [Header("Ajustes")]
        [Tooltip("Inclinación hacia abajo de la linterna (grados).")]
        [SerializeField] private float flashlightPitch = 16f;
        [SerializeField] private float flashlightFollow = 10f;
        [Tooltip("Altura del pie (m) por debajo de la cual cuenta como apoyado.")]
        [SerializeField] private float footContact = 0.11f;
        [SerializeField] private float lookRadius = 6f;
        [Tooltip("Grados de inclinación del torso por m/s² de aceleración (adelante al arrancar, atrás al frenar).")]
        [SerializeField] private float leanPerAccel = 0.9f;
        [SerializeField] private float maxLean = 9f;
        [SerializeField] private float leanSmoothing = 10f;
        [Tooltip("Segundos sin moverse mientras recoge algo del suelo (dura lo que el gesto de agacharse y volver).")]
        [SerializeField] private float pickUpLock = 1.3f;

        public event Action<bool> Footstep;

        private Animator animator;
        private Transform footL, footR, handL;
        private bool footLDown, footRDown;
        private bool wasDead;
        private float actionUnlock;
        private float fireTimer = 99f;
        private Vector3 lookTarget;
        private float lookWeight;
        private float torchWeight;
        private Transform spine, chest;
        private Transform handR;
        private Vector3 clubHandPos;
        private Quaternion clubHandRot;
        private bool clubOnBack;
        private float lastSpeed, lean;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int CrouchId = Animator.StringToHash("Crouch");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int FireId = Animator.StringToHash("Fire");
        private static readonly int ThrowId = Animator.StringToHash("Throw");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int PickUpId = Animator.StringToHash("PickUp");
        private static readonly int SearchId = Animator.StringToHash("Search");
        private static readonly int InteractId = Animator.StringToHash("Interact");
        private static readonly int HealId = Animator.StringToHash("Heal");
        private static readonly int ReviveId = Animator.StringToHash("Revive");
        private static readonly int RollId = Animator.StringToHash("Roll");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            footR = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            handL = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            handR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (club != null)
            {
                clubHandPos = club.transform.localPosition;
                clubHandRot = club.transform.localRotation;
            }
        }

        private void OnEnable()
        {
            combat.Swung += OnSwung;
            combat.Fired += OnFired;
            health.Damaged += OnDamaged;
            interactor.Interacted += OnInteracted;
            if (healing != null) healing.Healed += OnHealed;
        }

        private void OnDisable()
        {
            combat.Swung -= OnSwung;
            combat.Fired -= OnFired;
            health.Damaged -= OnDamaged;
            interactor.Interacted -= OnInteracted;
            if (healing != null) healing.Healed -= OnHealed;
            controller.ActionLocked = false;
        }

        /// <summary>Lanzar (piedra): lo llama el sistema de lanzamiento.</summary>
        public void PlayThrow() => animator.SetTrigger(ThrowId);

        /// <summary>Congela el cuerpo en la pose actual (parálisis de la captura). La capa procedural sigue encima.</summary>
        public void Freeze(bool frozen) => animator.speed = frozen ? 0f : 1f;

        /// <summary>Se suelta del haz: rueda por el suelo.</summary>
        public void PlayRoll() => animator.SetTrigger(RollId);

        /// <summary>Agacharse a dejar algo en el suelo (bengala de mano).</summary>
        public void PlayPlace() => Lock(PickUpId, pickUpLock);

        private void OnSwung() => animator.SetTrigger(AttackId);
        private void OnFired() { animator.SetTrigger(FireId); fireTimer = 0f; }
        private void OnDamaged(float amount) { if (!health.IsDead) animator.SetTrigger(HitId); }
        private void OnHealed() => Lock(HealId, 1.1f);

        private void OnInteracted(Interactable used)
        {
            if (used is SearchableContainer) Lock(SearchId, 1.2f);
            else if (used is Pickup || used is ClubPickup) Lock(PickUpId, pickUpLock);
            else Lock(InteractId, 0.9f);
        }

        private void Lock(int trigger, float seconds)
        {
            animator.SetTrigger(trigger);
            controller.ActionLocked = true;
            actionUnlock = Time.time + seconds;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            animator.SetFloat(SpeedId, controller.CurrentSpeed, 0.08f, dt);
            animator.SetBool(CrouchId, controller.IsCrouching);
            // Capa del brazo izquierdo (sostener la linterna): solo de pie, sin acción en curso.
            bool torch = flashlight != null && flashlight.IsOn && !controller.IsCrouching && !controller.ActionLocked
                         && combat.Phase == PlayerCombat.SwingPhase.None && fireTimer > 0.8f && !health.IsDead;
            torchWeight = Mathf.MoveTowards(torchWeight, torch ? 1f : 0f, dt * 4f);
            animator.SetLayerWeight(1, torchWeight);
            animator.SetBool(DeadId, health.IsDead);
            if (wasDead && !health.IsDead) animator.SetTrigger(ReviveId);
            wasDead = health.IsDead;
            if (controller.ActionLocked && Time.time >= actionUnlock) controller.ActionLocked = false;

            fireTimer += dt;
            if (flareGun != null) flareGun.SetActive(fireTimer < 0.8f);
            UpdateClub();
            if (torchProp != null) torchProp.SetActive(flashlight != null && flashlight.IsOn);
        }

        /// <summary>
        /// Garrote: en la funda de la mochila o en la mano derecha según PlayerCombat.DrawProgress. Cambia de padre a
        /// mitad del gesto, cuando la mano (IK) llega a la espalda.
        /// </summary>
        private void UpdateClub()
        {
            if (club == null) return;
            club.SetActive(combat.HasClub);
            bool back = clubSheath != null && combat.DrawProgress < 0.5f;
            if (back == clubOnBack && club.transform.parent != null) return;
            clubOnBack = back;
            if (back)
            {
                club.transform.SetParent(clubSheath, false);
                club.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            else
            {
                club.transform.SetParent(handR, false);
                club.transform.SetLocalPositionAndRotation(clubHandPos, clubHandRot);
            }
        }

        private void LateUpdate()
        {
            // Peso: el torso se adelanta al arrancar y se echa atrás al frenar (inercia del cuerpo).
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                float accel = (controller.CurrentSpeed - lastSpeed) / dt;
                lastSpeed = controller.CurrentSpeed;
                float target = health.IsDead ? 0f : Mathf.Clamp(accel * leanPerAccel, -maxLean, maxLean);
                lean = Mathf.Lerp(lean, target, 1f - Mathf.Exp(-leanSmoothing * dt));
                if (spine != null) spine.rotation = Quaternion.AngleAxis(lean * 0.55f, transform.right) * spine.rotation;
                if (chest != null) chest.rotation = Quaternion.AngleAxis(lean * 0.45f, transform.right) * chest.rotation;
            }

            // Pasos por contacto del pie con el suelo (sincronizados con la animación real).
            float groundY = transform.position.y;
            bool l = footL.position.y - groundY < footContact, r = footR.position.y - groundY < footContact;
            if (controller.CurrentSpeed > 0.3f)
            {
                if (l && !footLDown) Footstep?.Invoke(true);
                if (r && !footRDown) Footstep?.Invoke(false);
            }
            footLDown = l;
            footRDown = r;

            // Linterna: en la mano, apuntando hacia adelante con suavizado (no tiembla con la animación).
            if (flashlightLight != null)
            {
                flashlightLight.position = handL.position + transform.forward * 0.35f; // por delante del cuerpo: no ilumina su propia mano a centímetros (eso reventaba el bloom)
                Quaternion target = Quaternion.LookRotation(Quaternion.AngleAxis(flashlightPitch, transform.right) * transform.forward);
                flashlightLight.rotation = Quaternion.Slerp(flashlightLight.rotation, target, 1f - Mathf.Exp(-flashlightFollow * Time.deltaTime));
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            // Mira al interactuable cercano o al Lurker más cercano que lo está cazando.
            Vector3? target = null;
            if (interactor.Current != null) target = interactor.Current.FocusPosition + Vector3.up * 0.3f;
            float best = lookRadius * lookRadius * 4f;
            foreach (var e in ProjectRidge.AI.EnemyController.Active)
            {
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; target = e.transform.position + Vector3.up * 1.8f; }
            }
            float want = target.HasValue && !health.IsDead ? 0.7f : 0f;
            if (target.HasValue) lookTarget = Vector3.Lerp(lookTarget == Vector3.zero ? target.Value : lookTarget, target.Value, 0.1f);
            lookWeight = Mathf.MoveTowards(lookWeight, want, Time.deltaTime * 2f);
            animator.SetLookAtWeight(lookWeight, 0.15f, 0.8f, 0f, 0.6f);
            animator.SetLookAtPosition(lookTarget);

            // Desenfundar / enfundar: la mano derecha va a la funda y vuelve (arco de seno sobre el progreso).
            float p = combat.DrawProgress;
            float reach = clubSheath != null && combat.HasClub && p > 0f && p < 1f ? Mathf.Sin(p * Mathf.PI) : 0f;
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, reach);
            if (reach > 0f) animator.SetIKPosition(AvatarIKGoal.RightHand, clubSheath.position);
        }
    }
}
