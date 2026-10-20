using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Reacción orgánica a los golpes, encima de la animación (va después del Animator y de las capas procedurales):
    /// un resorte amortiguado inclina columna, cuello y cabeza en la dirección del golpe y rebota; los brazos se
    /// quedan atrás por inercia. Dos golpes seguidos se suman. Sirve para el papá y para el Lurker.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Animator))]
    public class HitReaction : MonoBehaviour
    {
        [SerializeField] private HitReactionConfig config;

        private Transform spine, chest, neck, head, armL, armR;
        private Vector3 angle;     // eje × grados, en espacio de mundo
        private Vector3 velocity;  // grados/s

        private void Awake()
        {
            var a = GetComponent<Animator>();
            if (a.avatar == null || !a.isHuman) { enabled = false; return; }
            spine = a.GetBoneTransform(HumanBodyBones.Spine);
            chest = a.GetBoneTransform(HumanBodyBones.Chest);
            neck = a.GetBoneTransform(HumanBodyBones.Neck);
            head = a.GetBoneTransform(HumanBodyBones.Head);
            armL = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            armR = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        /// <summary>Inclinación actual por golpes (grados): QA.</summary>
        public float Magnitude => angle.magnitude;

        /// <summary>Golpe que viaja en hitDirection (del atacante a la víctima), fuerza ~0.5 (roce) a 2 (brutal).</summary>
        public void Impulse(Vector3 hitDirection, float strength)
        {
            hitDirection.y = 0f;
            if (hitDirection.sqrMagnitude < 0.0001f) hitDirection = -transform.forward;
            hitDirection.Normalize();
            // El torso se dobla alejándose del golpe: rotación alrededor de (arriba × dirección).
            Vector3 axis = Vector3.Cross(Vector3.up, hitDirection);
            velocity += axis * (config.impulseScale * strength);
            velocity += Vector3.up * (config.impulseScale * strength * config.twist * Random.Range(-1f, 1f));
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            Vector3 accel = -config.stiffness * angle - config.damping * velocity;
            velocity += accel * dt;
            angle += velocity * dt;
            if (angle.magnitude > config.maxAngle) angle = angle.normalized * config.maxAngle;
            float deg = angle.magnitude;
            if (deg < 0.05f) return;
            Vector3 axis = angle / deg;
            Bend(spine, axis, deg * config.spine);
            Bend(chest, axis, deg * config.chest);
            Bend(neck, axis, deg * config.neck);
            Bend(head, axis, deg * config.head);
            Bend(armL, axis, -deg * config.arms);
            Bend(armR, axis, -deg * config.arms);
        }

        private static void Bend(Transform bone, Vector3 axis, float degrees)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
