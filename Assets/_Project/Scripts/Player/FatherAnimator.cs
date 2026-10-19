using System;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Animación procedural del papá sobre un rig de articulaciones (sin Animator ni clips externos):
    /// locomoción con ciclo de pasos según la distancia real (camina/trota/corre, agachado), golpe de garrote por fases,
    /// disparo de bengala, fabricar, reacción al daño y muerte. Las piernas calculan la altura de cadera para apoyar los pies.
    /// </summary>
    public class FatherAnimator : MonoBehaviour
    {
        [Serializable]
        public class Joints
        {
            public Transform root, hips, spine, chest, neck, head;
            public Transform shoulderL, elbowL, handL, shoulderR, elbowR, handR;
            public Transform hipL, kneeL, ankleL, hipR, kneeR, ankleR;
        }

        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private Joints j = new Joints();
        [SerializeField] private GameObject club;
        [SerializeField] private GameObject flareGun;

        [Header("Medidas del rig (m)")]
        [SerializeField] private float thighLength = 0.44f;
        [SerializeField] private float shinLength = 0.42f;
        [SerializeField] private float footHeight = 0.09f;

        [Header("Paso")]
        [SerializeField] private float walkStride = 1.25f;
        [SerializeField] private float sprintStride = 2.3f;
        [SerializeField] private float crouchStride = 0.85f;

        /// <summary>Un pie tocó el suelo (para el audio de pasos).</summary>
        public event Action<bool> Footstep;

        private float phase;
        private float crouch;
        private float moveBlend;
        private float fireTimer = 99f;
        private float hitTimer = 99f;
        private float deathBlend;
        private float lastYaw;
        private float turnLean;
        private bool lastFootLeft;

        private void OnEnable()
        {
            combat.Fired += OnFired;
            health.Damaged += OnDamaged;
            lastYaw = transform.eulerAngles.y;
        }

        private void OnDisable()
        {
            combat.Fired -= OnFired;
            health.Damaged -= OnDamaged;
        }

        private void OnFired() => fireTimer = 0f;
        private void OnDamaged(float amount) => hitTimer = 0f;

        private static Quaternion E(float x, float y, float z) => Quaternion.Euler(x, y, z);

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float speed = controller.CurrentSpeed;
            var cfg = controller.Config;
            float run01 = Mathf.Clamp01((speed - cfg.walkSpeed) / Mathf.Max(0.01f, cfg.sprintSpeed - cfg.walkSpeed));
            crouch = Mathf.MoveTowards(crouch, controller.IsCrouching ? 1f : 0f, dt * 4f);
            moveBlend = Mathf.MoveTowards(moveBlend, Mathf.Clamp01(speed / Mathf.Max(0.1f, cfg.walkSpeed)), dt * 6f);
            fireTimer += dt;
            hitTimer += dt;
            deathBlend = Mathf.MoveTowards(deathBlend, health.IsDead ? 1f : 0f, dt * (health.IsDead ? 1.6f : 10f));

            // Inclinación al girar.
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(dt, 0.0001f);
            lastYaw = yaw;
            turnLean = Mathf.Lerp(turnLean, Mathf.Clamp(-yawRate * 0.02f * moveBlend, -10f, 10f), dt * 6f);

            // Ciclo de pasos por distancia recorrida.
            float stride = Mathf.Lerp(Mathf.Lerp(walkStride, sprintStride, run01), crouchStride, crouch);
            float prevPhase = phase;
            phase += speed / stride * Mathf.PI * 2f * dt;
            if (Mathf.Floor(prevPhase / Mathf.PI) != Mathf.Floor(phase / Mathf.PI) && moveBlend > 0.2f)
            {
                lastFootLeft = !lastFootLeft;
                Footstep?.Invoke(lastFootLeft);
            }
            if (phase > 1000f) phase -= Mathf.PI * 200f;

            // ---------- Piernas
            float amp = moveBlend * Mathf.Lerp(Mathf.Lerp(24f, 40f, run01), 18f, crouch);
            float kneeAmp = moveBlend * Mathf.Lerp(Mathf.Lerp(30f, 75f, run01), 25f, crouch);
            float thighBase = Mathf.Lerp(0f, -72f, crouch);
            float kneeBase = Mathf.Lerp(4f, 118f, crouch);
            float ankleBase = Mathf.Lerp(0f, -46f, crouch);
            LegPose(phase, thighBase, kneeBase, ankleBase, amp, kneeAmp, out float tL, out float kL, out float aL);
            LegPose(phase + Mathf.PI, thighBase, kneeBase, ankleBase, amp, kneeAmp, out float tR, out float kR, out float aR);
            j.hipL.localRotation = E(tL, 0f, -2f);
            j.kneeL.localRotation = E(kL, 0f, 0f);
            j.ankleL.localRotation = E(aL, 0f, 0f);
            j.hipR.localRotation = E(tR, 0f, 2f);
            j.kneeR.localRotation = E(kR, 0f, 0f);
            j.ankleR.localRotation = E(aR, 0f, 0f);

            // Altura de cadera: la pierna más extendida apoya el pie en el suelo.
            float hL = LegHeight(tL, kL), hR = LegHeight(tR, kR);
            float hipY = Mathf.Max(hL, hR) + footHeight;
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.025f * moveBlend * (1f - crouch);
            j.hips.localPosition = new Vector3(0f, hipY + bob, 0f);
            j.hips.localRotation = E(0f, Mathf.Sin(phase) * 5f * moveBlend, Mathf.Cos(phase) * 2f * moveBlend);

            // ---------- Torso
            float lean = run01 * 14f + crouch * 22f + moveBlend * 3f;
            float breathe = Mathf.Sin(Time.time * 1.6f) * 1.2f;
            float hitJolt = hitTimer < 0.4f ? Mathf.Sin(hitTimer / 0.4f * Mathf.PI) * -18f : 0f;
            j.spine.localRotation = E(lean * 0.5f + hitJolt * 0.5f, -Mathf.Sin(phase) * 6f * moveBlend, turnLean);
            j.chest.localRotation = E(lean * 0.5f + breathe + hitJolt, -Mathf.Sin(phase) * 4f * moveBlend, 0f);
            j.neck.localRotation = E(-lean * 0.4f, 0f, 0f);
            j.head.localRotation = E(-lean * 0.3f - hitJolt * 0.5f + Mathf.Sin(Time.time * 0.37f) * 4f * (1f - moveBlend), Mathf.Sin(Time.time * 0.23f) * 12f * (1f - moveBlend), 0f);

            // ---------- Brazos (balanceo opuesto a las piernas)
            float armAmp = moveBlend * Mathf.Lerp(Mathf.Lerp(22f, 50f, run01), 12f, crouch);
            float elbow = Mathf.Lerp(Mathf.Lerp(12f, 75f, run01), 60f, crouch);
            Quaternion sL = E(Mathf.Sin(phase) * armAmp + crouch * -20f, 0f, -6f - run01 * 4f);
            Quaternion eL = E(-elbow, 0f, 0f);
            Quaternion sR = E(-Mathf.Sin(phase) * armAmp + crouch * -20f, 0f, 6f + run01 * 4f);
            Quaternion eR = E(-elbow - 10f, 0f, 0f);

            // Garrote: arranque (atrás y arriba) → golpe (adelante y abajo) → recuperación.
            float twist = 0f;
            switch (combat.Phase)
            {
                case PlayerCombat.SwingPhase.Windup:
                {
                    float t = Smooth(combat.PhaseT);
                    sR = Quaternion.Slerp(sR, E(-195f, -15f, 25f), t);
                    eR = Quaternion.Slerp(eR, E(-60f, 0f, 0f), t);
                    twist = 30f * t;
                    break;
                }
                case PlayerCombat.SwingPhase.Active:
                {
                    float t = Smooth(combat.PhaseT);
                    sR = Quaternion.Slerp(E(-195f, -15f, 25f), E(-35f, 40f, -10f), t);
                    eR = Quaternion.Slerp(E(-60f, 0f, 0f), E(-5f, 0f, 0f), t);
                    twist = Mathf.Lerp(30f, -35f, t);
                    break;
                }
                case PlayerCombat.SwingPhase.Recovery:
                {
                    float t = Smooth(combat.PhaseT);
                    sR = Quaternion.Slerp(E(-35f, 40f, -10f), sR, t);
                    eR = Quaternion.Slerp(E(-5f, 0f, 0f), eR, t);
                    twist = Mathf.Lerp(-35f, 0f, t);
                    break;
                }
                case PlayerCombat.SwingPhase.Crafting:
                {
                    float hammer = Mathf.Abs(Mathf.Sin(Time.time * 9f));
                    sR = E(-70f - hammer * 30f, 10f, 10f);
                    eR = E(-80f + hammer * 30f, 0f, 0f);
                    sL = E(-60f, -10f, -10f);
                    eL = E(-70f, 0f, 0f);
                    break;
                }
            }
            j.chest.localRotation *= E(0f, twist, 0f);

            // Bengala: brazo izquierdo al frente con retroceso.
            bool firing = fireTimer < 0.7f;
            if (firing)
            {
                float recoil = fireTimer < 0.12f ? (1f - fireTimer / 0.12f) * 25f : 0f;
                float up = Mathf.Clamp01(fireTimer / 0.08f) * Mathf.Clamp01((0.7f - fireTimer) / 0.2f);
                sL = Quaternion.Slerp(sL, E(-88f - recoil, -15f, 0f), up);
                eL = Quaternion.Slerp(eL, E(-5f - recoil * 0.5f, 0f, 0f), up);
            }
            if (flareGun != null && flareGun.activeSelf != firing) flareGun.SetActive(firing);
            if (club != null && club.activeSelf != combat.HasClub) club.SetActive(combat.HasClub);

            j.shoulderL.localRotation = sL;
            j.elbowL.localRotation = eL;
            j.shoulderR.localRotation = sR;
            j.elbowR.localRotation = eR;
            j.handL.localRotation = Quaternion.identity;
            j.handR.localRotation = E(-20f, 0f, 0f);

            // ---------- Muerte: cae hacia atrás y queda tendido.
            if (deathBlend > 0.001f)
            {
                float d = Smooth(deathBlend);
                j.root.localRotation = E(-84f * d, 0f, 6f * d);
                j.root.localPosition = new Vector3(0f, 0.12f * d, -0.5f * d);
                j.shoulderL.localRotation = Quaternion.Slerp(j.shoulderL.localRotation, E(-20f, 0f, -70f), d);
                j.shoulderR.localRotation = Quaternion.Slerp(j.shoulderR.localRotation, E(-10f, 0f, 75f), d);
                j.head.localRotation = Quaternion.Slerp(j.head.localRotation, E(10f, 35f, 0f), d);
            }
            else
            {
                j.root.localRotation = Quaternion.identity;
                j.root.localPosition = Vector3.zero;
            }
        }

        private static void LegPose(float p, float thighBase, float kneeBase, float ankleBase, float amp, float kneeAmp,
                                    out float thigh, out float knee, out float ankle)
        {
            float swing = Mathf.Sin(p);
            // Rodilla doblada mientras la pierna avanza en el aire (mitad del ciclo).
            float lift = Mathf.Max(0f, Mathf.Sin(p - Mathf.PI * 0.5f));
            thigh = thighBase - swing * amp;
            knee = kneeBase + lift * kneeAmp;
            // Pie casi paralelo al suelo, con algo de punta al despegar.
            ankle = -(thigh + knee) + lift * 12f;
        }

        private float LegHeight(float thighDeg, float kneeDeg)
        {
            float a1 = thighDeg * Mathf.Deg2Rad;
            float a2 = (thighDeg + kneeDeg) * Mathf.Deg2Rad;
            return thighLength * Mathf.Cos(a1) + shinLength * Mathf.Cos(a2);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
