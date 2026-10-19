using System;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Animación procedural del Lurker: quietud total con tics violentos (la cabeza se quiebra de lado, un dedo se
    /// estira), andar desigual de arranques y frenadas, persecución encorvada con los brazos largos por delante,
    /// embestida con la mandíbula abierta, convulsiones al aturdirse y muerte retorcida. Todo se calcula por frame
    /// sobre un rig de articulaciones (sin Animator).
    /// </summary>
    public class LurkerAnimator : MonoBehaviour
    {
        [Serializable]
        public class Joints
        {
            public Transform root, hips, spine, chest, neck, head, jaw;
            public Transform shoulderL, elbowL, handL, shoulderR, elbowR, handR;
            public Transform[] fingersL, fingersR;
            public Transform hipL, kneeL, ankleL, hipR, kneeR, ankleR;
        }

        [SerializeField] private EnemyController enemy;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private EnemyConfig config;
        [SerializeField] private Joints j = new Joints();

        [Header("Medidas del rig (m)")]
        [SerializeField] private float thighLength = 0.58f;
        [SerializeField] private float shinLength = 0.62f;
        [SerializeField] private float footLength = 0.32f;

        /// <summary>Un pie tocó el suelo (audio).</summary>
        public event Action Footstep;
        /// <summary>Tic violento (audio: chasquido).</summary>
        public event Action Twitched;

        private float phase;
        private float stateTime;
        private EnemyState state;
        private float speedBlend;
        private float chaseBlend;
        private float deathBlend;
        private float stunBlend;

        // Tics: articulación objetivo, desplazamiento y tiempos.
        private enum TicTarget { Head, Neck, ShoulderL, ShoulderR, Chest, FingersL, FingersR }
        private TicTarget ticTarget;
        private Vector3 ticOffset;
        private float ticAmount;
        private float ticHold;
        private float nextTic;
        private Vector3 headSnap;
        private float nextHeadSnap;
        private float glitchTimer;
        private Vector3 glitch;

        private System.Random rng;

        private void Awake() => rng = new System.Random(GetInstanceID());

        private float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        private void OnEnable() => enemy.StateChanged += OnState;
        private void OnDisable() => enemy.StateChanged -= OnState;

        private void OnState(EnemyState s)
        {
            state = s;
            stateTime = 0f;
            if (s == EnemyState.Investigate || s == EnemyState.Chase) ForceTic(1.3f); // sobresalto al detectar algo
        }

        private static Quaternion E(float x, float y, float z) => Quaternion.Euler(x, y, z);

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            state = enemy.State;
            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            speedBlend = Mathf.MoveTowards(speedBlend, Mathf.Clamp01(speed / 1.4f), dt * 5f);
            bool hunting = state == EnemyState.Chase || state == EnemyState.Attack;
            chaseBlend = Mathf.MoveTowards(chaseBlend, hunting ? 1f : 0f, dt * 3f);
            deathBlend = Mathf.MoveTowards(deathBlend, state == EnemyState.Dead ? 1f : 0f, dt * 1.8f);
            stunBlend = Mathf.MoveTowards(stunBlend, state == EnemyState.Stunned ? 1f : 0f, dt * 8f);

            UpdateTics(dt, hunting);

            // ---------- Paso: irregular (frena y arranca), más largo y rápido al cazar.
            float stride = Mathf.Lerp(1.5f, 2.6f, chaseBlend);
            float stutter = hunting ? 1f : 0.55f + 0.9f * Mathf.PerlinNoise(Time.time * 0.9f, 3.1f);
            float prev = phase;
            phase += speed / stride * Mathf.PI * 2f * dt * stutter;
            if (Mathf.Floor(prev / Mathf.PI) != Mathf.Floor(phase / Mathf.PI) && speedBlend > 0.15f) Footstep?.Invoke();
            if (phase > 1000f) phase -= Mathf.PI * 200f;

            // ---------- Piernas digitígradas (rodilla hacia atrás) con zancada asimétrica.
            float amp = speedBlend * Mathf.Lerp(22f, 38f, chaseBlend);
            float crouchDown = Mathf.Max(chaseBlend * 0.5f, stunBlend);
            float thighBase = Mathf.Lerp(-32f, -55f, crouchDown);
            float kneeBase = Mathf.Lerp(78f, 115f, crouchDown);
            Leg(phase, amp * 1.0f, thighBase, kneeBase, out float tL, out float kL, out float aL);
            Leg(phase + Mathf.PI, amp * 0.78f, thighBase, kneeBase, out float tR, out float kR, out float aR);
            j.hipL.localRotation = E(tL, 0f, -4f);
            j.kneeL.localRotation = E(kL, 0f, 0f);
            j.ankleL.localRotation = E(aL, 0f, 0f);
            j.hipR.localRotation = E(tR, 0f, 5f);
            j.kneeR.localRotation = E(kR, 0f, 0f);
            j.ankleR.localRotation = E(aR, 0f, 0f);
            float hipY = Mathf.Max(LegHeight(tL, kL, aL), LegHeight(tR, kR, aR));
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.06f * speedBlend;
            j.hips.localPosition = new Vector3(0f, hipY + bob, 0f);
            j.hips.localRotation = E(0f, Mathf.Sin(phase) * 8f * speedBlend, Mathf.Sin(phase * 0.5f) * 4f * speedBlend);

            // ---------- Torso encorvado; al cazar casi horizontal.
            float hunch = Mathf.Lerp(40f, 62f, chaseBlend);
            float breathe = Mathf.Sin(Time.time * 2.3f) * 2f * (1f - speedBlend);
            j.spine.localRotation = E(hunch * 0.45f, Mathf.Sin(phase) * 7f * speedBlend, Mathf.Sin(Time.time * 0.7f) * 3f);
            j.chest.localRotation = E(hunch * 0.55f + breathe, -Mathf.Sin(phase) * 10f * speedBlend, 0f);
            // El cuello compensa para mirar al frente; la cabeza ladeada (inquietante).
            j.neck.localRotation = E(-hunch * 0.6f + 10f, 0f, 0f);
            j.head.localRotation = E(-hunch * 0.25f, 0f, 14f);

            // ---------- Brazos largos: colgando y balanceándose con retraso; al cazar, por delante y agitándose.
            float sway = Mathf.Sin(phase - 0.6f) * 18f * speedBlend;
            float flail = Mathf.Sin(Time.time * 11f) * 28f * chaseBlend;
            Quaternion sL = E(Mathf.Lerp(8f + sway, -70f + flail, chaseBlend), 0f, Mathf.Lerp(-10f, -25f, chaseBlend));
            Quaternion sR = E(Mathf.Lerp(8f - sway, -75f - flail, chaseBlend), 0f, Mathf.Lerp(12f, 28f, chaseBlend));
            Quaternion eL = E(Mathf.Lerp(-18f, -35f, chaseBlend), 0f, 0f);
            Quaternion eR = E(Mathf.Lerp(-12f, -40f, chaseBlend), 0f, 0f);
            float fingerCurl = Mathf.Lerp(25f, -10f, chaseBlend); // garras abiertas al cazar
            float jaw = chaseBlend * 12f + Mathf.Max(0f, Mathf.Sin(Time.time * 3.1f)) * 6f * chaseBlend;

            // ---------- Investigar: congelado, solo la cabeza gira a saltos.
            if (state == EnemyState.Investigate && stateTime < config.alertFreeze + 0.2f)
            {
                if (Time.time > nextHeadSnap)
                {
                    nextHeadSnap = Time.time + R(0.18f, 0.4f);
                    headSnap = new Vector3(R(-15f, 15f), R(-55f, 55f), R(-40f, 40f));
                }
                j.head.localRotation *= E(headSnap.x, headSnap.y, headSnap.z);
            }

            // ---------- Ataque: se yergue con los brazos abiertos y embiste con un tajo.
            if (state == EnemyState.Attack)
            {
                float w = config.attackWindup, a = config.attackActive, r = config.attackRecovery;
                if (stateTime < w)
                {
                    float t = Smooth(stateTime / w);
                    j.spine.localRotation = Quaternion.Slerp(j.spine.localRotation, E(-15f, 0f, 0f), t);
                    j.chest.localRotation = Quaternion.Slerp(j.chest.localRotation, E(-20f, 0f, 0f), t);
                    sL = Quaternion.Slerp(sL, E(-165f, 0f, -45f), t);
                    sR = Quaternion.Slerp(sR, E(-170f, 0f, 50f), t);
                    eL = eR = E(-20f, 0f, 0f);
                    jaw = Mathf.Lerp(jaw, 45f, t);
                    fingerCurl = -25f;
                }
                else if (stateTime < w + a + 0.1f)
                {
                    float t = Smooth(Mathf.Clamp01((stateTime - w) / (a + 0.1f)));
                    j.spine.localRotation = E(Mathf.Lerp(-15f, 40f, t), 0f, 0f);
                    j.chest.localRotation = E(Mathf.Lerp(-20f, 45f, t), 0f, 0f);
                    sL = Quaternion.Slerp(E(-165f, 0f, -45f), E(-20f, 30f, 10f), t);
                    sR = Quaternion.Slerp(E(-170f, 0f, 50f), E(-15f, -35f, -10f), t);
                    eL = eR = E(-5f, 0f, 0f);
                    jaw = 55f;
                    fingerCurl = -30f;
                }
                else
                {
                    float t = Smooth(Mathf.Clamp01((stateTime - w - a - 0.1f) / Mathf.Max(0.1f, r)));
                    j.spine.localRotation = Quaternion.Slerp(E(40f, 0f, 0f), j.spine.localRotation, t);
                    j.chest.localRotation = Quaternion.Slerp(E(45f, 0f, 0f), j.chest.localRotation, t);
                    sL = Quaternion.Slerp(E(-20f, 30f, 10f), sL, t);
                    sR = Quaternion.Slerp(E(-15f, -35f, -10f), sR, t);
                    jaw = Mathf.Lerp(55f, jaw, t);
                }
            }

            // ---------- Tic activo (se suma a la pose).
            float tic = ticAmount;
            switch (ticTarget)
            {
                case TicTarget.Head: j.head.localRotation *= E(ticOffset.x * tic, ticOffset.y * tic, ticOffset.z * tic); break;
                case TicTarget.Neck: j.neck.localRotation *= E(ticOffset.x * tic, ticOffset.y * tic, ticOffset.z * tic); break;
                case TicTarget.ShoulderL: sL *= E(ticOffset.x * tic, 0f, ticOffset.z * tic); break;
                case TicTarget.ShoulderR: sR *= E(ticOffset.x * tic, 0f, ticOffset.z * tic); break;
                case TicTarget.Chest: j.chest.localRotation *= E(ticOffset.x * tic * 0.5f, ticOffset.y * tic * 0.5f, 0f); break;
                case TicTarget.FingersL: fingerCurl -= 40f * tic; break;
                case TicTarget.FingersR: fingerCurl -= 40f * tic; break;
            }

            // ---------- Aturdido: convulsiones.
            if (stunBlend > 0.01f)
            {
                float c = stunBlend;
                float n(float s) => (Mathf.PerlinNoise(Time.time * 22f, s) - 0.5f) * 2f;
                j.chest.localRotation *= E(n(1f) * 20f * c, n(2f) * 25f * c, n(3f) * 15f * c);
                j.head.localRotation *= E(n(4f) * 40f * c, n(5f) * 50f * c, n(6f) * 40f * c);
                sL *= E(n(7f) * 50f * c, 0f, n(8f) * 30f * c);
                sR *= E(n(9f) * 50f * c, 0f, n(10f) * 30f * c);
                jaw = Mathf.Max(jaw, 30f + n(11f) * 25f) * c + jaw * (1f - c);
                fingerCurl = Mathf.Lerp(fingerCurl, 60f + n(12f) * 30f, c);
            }

            j.shoulderL.localRotation = sL;
            j.shoulderR.localRotation = sR;
            j.elbowL.localRotation = eL;
            j.elbowR.localRotation = eR;
            j.handL.localRotation = E(fingerCurl * 0.3f, 0f, 0f);
            j.handR.localRotation = E(fingerCurl * 0.3f, 0f, 0f);
            CurlFingers(j.fingersL, fingerCurl);
            CurlFingers(j.fingersR, fingerCurl * 0.85f + 6f);
            if (j.jaw != null) j.jaw.localRotation = E(jaw, 0f, 0f);

            // ---------- Glitch: saltos laterales mínimos del cuerpo (movimiento "que no debería poder hacer").
            glitchTimer -= dt;
            if (glitchTimer <= 0f)
            {
                glitchTimer = hunting ? R(0.08f, 0.35f) : R(1.5f, 5f);
                glitch = hunting || R(0f, 1f) < 0.3f ? new Vector3(R(-0.07f, 0.07f), 0f, R(-0.05f, 0.05f)) : Vector3.zero;
            }
            glitch = Vector3.Lerp(glitch, Vector3.zero, dt * 10f);

            // ---------- Muerte: se desploma hacia adelante, retorcido.
            if (deathBlend > 0.001f)
            {
                float d = Smooth(deathBlend);
                float lastTwitch = stateTime < 4f && Mathf.PerlinNoise(Time.time * 6f, 9f) > 0.72f ? 1f : 0f;
                j.root.localRotation = E(82f * d, 25f * d, -12f * d);
                j.root.localPosition = new Vector3(0f, -0.2f * d, 0.6f * d);
                j.head.localRotation = Quaternion.Slerp(j.head.localRotation, E(-30f, 70f, 60f + lastTwitch * 25f), d);
                j.shoulderL.localRotation = Quaternion.Slerp(j.shoulderL.localRotation, E(-150f, 0f, -70f), d);
                j.shoulderR.localRotation = Quaternion.Slerp(j.shoulderR.localRotation, E(-40f + lastTwitch * 30f, 0f, 80f), d);
                CurlFingers(j.fingersL, Mathf.Lerp(fingerCurl, 80f, d));
            }
            else
            {
                j.root.localRotation = Quaternion.identity;
                j.root.localPosition = glitch;
            }
        }

        private void UpdateTics(float dt, bool hunting)
        {
            if (state == EnemyState.Dead) { ticAmount = 0f; return; }
            if (Time.time >= nextTic)
            {
                ForceTic(hunting ? 0.7f : 1f);
                nextTic = Time.time + (hunting ? R(0.25f, 0.9f) : R(0.6f, 3.2f));
            }
            // Llega de golpe (≈50 ms), se sostiene y vuelve despacio.
            if (ticHold > 0f)
            {
                ticHold -= dt;
                ticAmount = Mathf.MoveTowards(ticAmount, 1f, dt * 22f);
            }
            else
            {
                ticAmount = Mathf.MoveTowards(ticAmount, 0f, dt * 2.5f);
            }
        }

        private void ForceTic(float strength)
        {
            ticTarget = (TicTarget)rng.Next(7);
            ticOffset = new Vector3(R(-45f, 45f), R(-60f, 60f), R(-55f, 55f)) * strength;
            ticHold = R(0.15f, 1.1f);
            Twitched?.Invoke();
        }

        private static void CurlFingers(Transform[] fingers, float curl)
        {
            if (fingers == null) return;
            for (int i = 0; i < fingers.Length; i++)
                if (fingers[i] != null) fingers[i].localRotation = E(curl + i * 4f, (i - 1) * 14f, 0f);
        }

        private static void Leg(float p, float amp, float thighBase, float kneeBase, out float thigh, out float knee, out float ankle)
        {
            float swing = Mathf.Sin(p);
            float lift = Mathf.Max(0f, Mathf.Sin(p - Mathf.PI * 0.5f));
            thigh = thighBase - swing * amp;
            knee = kneeBase + lift * amp * 1.2f;
            // Metatarso (digitígrado) casi vertical, inclinado hacia adelante.
            ankle = -(thigh + knee) - 8f + lift * 20f;
        }

        private float LegHeight(float thighDeg, float kneeDeg, float ankleDeg)
        {
            float a1 = thighDeg * Mathf.Deg2Rad;
            float a2 = (thighDeg + kneeDeg) * Mathf.Deg2Rad;
            float a3 = (thighDeg + kneeDeg + ankleDeg) * Mathf.Deg2Rad;
            return thighLength * Mathf.Cos(a1) + shinLength * Mathf.Cos(a2) + footLength * Mathf.Cos(a3);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
