using System;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Animación del Lurker: clips reales (zombi al patrullar, carrera al cazar, zarpazo, retroceso al golpe,
    /// muerte) sobre un esqueleto humanoide deformado (extremidades largas). Encima, una capa procedural después
    /// del Animator: joroba que crece al cazar, cabeza ladeada, tics violentos (quiebres de cuello, espasmos de
    /// brazo y dedos), giros de cabeza a saltos al investigar, convulsiones al aturdirse y saltos laterales mínimos.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class LurkerAnimator : MonoBehaviour
    {
        [SerializeField] private EnemyController enemy;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private EnemyConfig config;
        [Tooltip("Altura del pie (m) por debajo de la cual cuenta como apoyado.")]
        [SerializeField] private float footContact = 0.14f;

        public event Action Footstep;
        public event Action Twitched;

        private Animator animator;
        private Transform root, spine, chest, upperChest, neck, head, armL, armR, foreL, foreR, footL, footR;
        private Transform[] fingers;
        private bool footLDown, footRDown;
        private EnemyState state;
        private float stateTime;
        private float hunt, stun, dead;
        private System.Random rng;

        private enum TicTarget { Head, Neck, ArmL, ArmR, Chest, Fingers }
        private TicTarget ticTarget;
        private Vector3 ticOffset;
        private float ticAmount, ticHold, nextTic;
        private Vector3 headSnap;
        private float nextHeadSnap;
        private Vector3 glitch;
        private float glitchTimer;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int HuntingId = Animator.StringToHash("Hunting");
        private static readonly int StunnedId = Animator.StringToHash("Stunned");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int AttackId = Animator.StringToHash("Attack");

        // Perezoso: sobrevive a una recarga de dominio en play mode (rng no se serializa).
        private System.Random Rng => rng ??= new System.Random(GetInstanceID());
        private float R(float a, float b) => a + (float)Rng.NextDouble() * (b - a);

        private void Awake()
        {
            animator = GetComponent<Animator>();
            rng = new System.Random(GetInstanceID());
            root = transform;
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            armL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            armR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            foreL = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            foreR = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            footR = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            fingers = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal), animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal),
                animator.GetBoneTransform(HumanBodyBones.LeftRingProximal), animator.GetBoneTransform(HumanBodyBones.RightIndexProximal),
                animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal), animator.GetBoneTransform(HumanBodyBones.RightRingProximal),
            };
        }

        private void OnEnable() => enemy.StateChanged += OnState;
        private void OnDisable() => enemy.StateChanged -= OnState;

        private void OnState(EnemyState s)
        {
            state = s;
            stateTime = 0f;
            if (s == EnemyState.Attack) animator.SetTrigger(AttackId);
            if (s == EnemyState.Investigate || s == EnemyState.Chase) ForceTic(1.3f); // sobresalto
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            state = enemy.State;
            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            bool hunting = state == EnemyState.Chase || state == EnemyState.Attack;
            animator.SetFloat(SpeedId, speed, 0.1f, dt);
            animator.SetBool(HuntingId, hunting);
            animator.SetBool(StunnedId, state == EnemyState.Stunned);
            animator.SetBool(DeadId, state == EnemyState.Dead);
            hunt = Mathf.MoveTowards(hunt, hunting ? 1f : 0f, dt * 3f);
            stun = Mathf.MoveTowards(stun, state == EnemyState.Stunned ? 1f : 0f, dt * 8f);
            dead = Mathf.MoveTowards(dead, state == EnemyState.Dead ? 1f : 0f, dt * 2f);
            UpdateTics(dt, hunting);
        }

        // Rotación en el espacio del personaje (independiente de los ejes locales de cada hueso).
        private void Rotate(Transform bone, float pitch, float yaw, float roll)
        {
            if (bone == null) return;
            bone.rotation = Quaternion.AngleAxis(yaw, root.up) * Quaternion.AngleAxis(pitch, root.right) * Quaternion.AngleAxis(roll, root.forward) * bone.rotation;
        }

        private void LateUpdate()
        {
            float alive = 1f - dead;

            // Joroba: algo encorvado siempre; casi horizontal al cazar (pero no durante el zarpazo).
            float attackLift = state == EnemyState.Attack && stateTime < config.attackWindup ? stateTime / config.attackWindup : 0f;
            float hunch = (Mathf.Lerp(14f, 34f, hunt) - attackLift * 30f) * alive;
            Rotate(spine, hunch * 0.35f, 0f, 0f);
            Rotate(chest, hunch * 0.35f, 0f, 0f);
            Rotate(upperChest, hunch * 0.3f, 0f, 0f);
            // El cuello compensa para mirar al frente; la cabeza va ladeada.
            Rotate(neck, -hunch * 0.5f, 0f, 0f);
            Rotate(head, -hunch * 0.2f, 0f, 16f * alive);

            // Al cazar, los brazos largos se adelantan, abiertos.
            if (hunt > 0.01f && state != EnemyState.Attack)
            {
                float flail = Mathf.Sin(Time.time * 11f) * 20f * hunt;
                Rotate(armL, -40f * hunt + flail, 0f, -10f * hunt);
                Rotate(armR, -45f * hunt - flail, 0f, 12f * hunt);
            }

            // Investigar: quietud, la cabeza gira a saltos.
            if (state == EnemyState.Investigate && stateTime < config.alertFreeze + 0.3f)
            {
                if (Time.time > nextHeadSnap)
                {
                    nextHeadSnap = Time.time + R(0.16f, 0.4f);
                    headSnap = new Vector3(R(-15f, 15f), R(-60f, 60f), R(-45f, 45f));
                }
                Rotate(head, headSnap.x, headSnap.y, headSnap.z);
            }

            // Tic activo.
            if (ticAmount > 0.001f && alive > 0.5f)
            {
                Vector3 o = ticOffset * ticAmount;
                switch (ticTarget)
                {
                    case TicTarget.Head: Rotate(head, o.x, o.y, o.z); break;
                    case TicTarget.Neck: Rotate(neck, o.x * 0.6f, o.y * 0.6f, o.z * 0.6f); break;
                    case TicTarget.ArmL: Rotate(armL, o.x, 0f, o.z); Rotate(foreL, o.y * 0.5f, 0f, 0f); break;
                    case TicTarget.ArmR: Rotate(armR, o.x, 0f, o.z); Rotate(foreR, o.y * 0.5f, 0f, 0f); break;
                    case TicTarget.Chest: Rotate(chest, o.x * 0.4f, o.y * 0.4f, o.z * 0.3f); break;
                    case TicTarget.Fingers: foreach (var f in fingers) Rotate(f, o.x, 0f, 0f); break;
                }
            }

            // Aturdido: convulsiones.
            if (stun > 0.01f)
            {
                float N(float s) => (Mathf.PerlinNoise(Time.time * 22f, s) - 0.5f) * 2f;
                Rotate(chest, N(1f) * 18f * stun, N(2f) * 22f * stun, N(3f) * 12f * stun);
                Rotate(head, N(4f) * 40f * stun, N(5f) * 50f * stun, N(6f) * 40f * stun);
                Rotate(armL, N(7f) * 45f * stun, 0f, N(8f) * 25f * stun);
                Rotate(armR, N(9f) * 45f * stun, 0f, N(10f) * 25f * stun);
            }

            // Saltos laterales mínimos del cuerpo (movimiento "que no debería poder hacer").
            glitchTimer -= Time.deltaTime;
            if (glitchTimer <= 0f)
            {
                bool huntingNow = hunt > 0.5f;
                glitchTimer = huntingNow ? R(0.08f, 0.35f) : R(1.5f, 5f);
                glitch = (huntingNow || R(0f, 1f) < 0.3f) && alive > 0.5f ? new Vector3(R(-0.06f, 0.06f), 0f, R(-0.04f, 0.04f)) : Vector3.zero;
            }
            glitch = Vector3.Lerp(glitch, Vector3.zero, Time.deltaTime * 10f);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            hips.position += root.TransformVector(glitch);

            // Pasos.
            float groundY = root.position.y;
            bool l = footL.position.y - groundY < footContact, r = footR.position.y - groundY < footContact;
            if (agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.1f)
            {
                if (l && !footLDown) Footstep?.Invoke();
                if (r && !footRDown) Footstep?.Invoke();
            }
            footLDown = l;
            footRDown = r;
        }

        private void UpdateTics(float dt, bool hunting)
        {
            if (state == EnemyState.Dead) { ticAmount = 0f; return; }
            if (Time.time >= nextTic)
            {
                ForceTic(hunting ? 0.7f : 1f);
                nextTic = Time.time + (hunting ? R(0.25f, 0.9f) : R(0.7f, 3.2f));
            }
            // Llega de golpe (~50 ms), se sostiene y vuelve despacio.
            if (ticHold > 0f) { ticHold -= dt; ticAmount = Mathf.MoveTowards(ticAmount, 1f, dt * 22f); }
            else ticAmount = Mathf.MoveTowards(ticAmount, 0f, dt * 2.5f);
        }

        private void ForceTic(float strength)
        {
            ticTarget = (TicTarget)Rng.Next(6);
            ticOffset = new Vector3(R(-40f, 40f), R(-55f, 55f), R(-50f, 50f)) * strength;
            ticHold = R(0.15f, 1f);
            Twitched?.Invoke();
        }
    }
}
