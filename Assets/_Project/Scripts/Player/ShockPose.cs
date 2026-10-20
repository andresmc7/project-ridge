using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Pose de shock procedural para la captura del OVNI (mientras no haya un clip "Electrocuted" de Mixamo): brazos
    /// un poco separados, dedos abiertos y la cabeza hacia atrás. Encima, un temblor de alta frecuencia en columna,
    /// brazos y cabeza, como una corriente eléctrica. Va en LateUpdate, después del Animator.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ShockPose : MonoBehaviour
    {
        [Tooltip("Separación de los brazos del cuerpo (°).")]
        [SerializeField] private float armSpread = 32f;
        [SerializeField] private float elbowBend = 18f;
        [Tooltip("Apertura de los dedos (°).")]
        [SerializeField] private float fingerSpread = 22f;
        [SerializeField] private float headBack = 14f;

        /// <summary>0 = sin efecto, 1 = pose completa.</summary>
        public float Weight { get; set; }
        /// <summary>Amplitud del temblor (°).</summary>
        public float Tremor { get; set; }
        /// <summary>Frecuencia del temblor (Hz).</summary>
        public float TremorHz { get; set; } = 20f;

        private Animator animator;
        private Transform spine, chest, neck, head, upperL, upperR, lowerL, lowerR;
        private Transform[] fingersL, fingersR;
        private float phase;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            upperL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            upperR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lowerL = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lowerR = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            fingersL = Fingers(true);
            fingersR = Fingers(false);
        }

        private Transform[] Fingers(bool left)
        {
            var list = new System.Collections.Generic.List<Transform>();
            var bones = left
                ? new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftThumbProximal }
                : new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightThumbProximal };
            foreach (var b in bones) { var t = animator.GetBoneTransform(b); if (t != null) list.Add(t); }
            return list.ToArray();
        }

        private void LateUpdate()
        {
            if (Weight <= 0.001f && Tremor <= 0.001f) return;
            float w = Weight;
            Vector3 fwd = transform.forward, right = transform.right, up = transform.up;
            // Brazos: se abren hacia afuera (alrededor del eje adelante del cuerpo), codos algo doblados.
            if (upperL != null) upperL.rotation = Quaternion.AngleAxis(-armSpread * w, fwd) * upperL.rotation;
            if (upperR != null) upperR.rotation = Quaternion.AngleAxis(armSpread * w, fwd) * upperR.rotation;
            if (lowerL != null) lowerL.rotation = Quaternion.AngleAxis(-elbowBend * w, up) * lowerL.rotation;
            if (lowerR != null) lowerR.rotation = Quaternion.AngleAxis(elbowBend * w, up) * lowerR.rotation;
            for (int i = 0; i < fingersL.Length; i++) fingersL[i].rotation = Quaternion.AngleAxis(-fingerSpread * w * (i - 1.5f) * 0.5f, fwd) * fingersL[i].rotation;
            for (int i = 0; i < fingersR.Length; i++) fingersR[i].rotation = Quaternion.AngleAxis(fingerSpread * w * (i - 1.5f) * 0.5f, fwd) * fingersR[i].rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(-headBack * w, right) * head.rotation;

            // Temblor: ruido a 18–25 Hz, distinto en cada hueso y eje.
            phase += Time.deltaTime * TremorHz;
            Shake(spine, 0.6f, 1.3f);
            Shake(chest, 0.8f, 2.7f);
            Shake(neck, 1f, 4.1f);
            Shake(head, 1f, 5.9f);
            Shake(upperL, 1.2f, 7.3f);
            Shake(upperR, 1.2f, 8.9f);
            Shake(lowerL, 1.4f, 10.2f);
            Shake(lowerR, 1.4f, 11.6f);
        }

        private void Shake(Transform bone, float scale, float seed)
        {
            if (bone == null) return;
            float a = Tremor * scale;
            float x = (Mathf.PerlinNoise(phase, seed) - 0.5f) * 2f * a;
            float y = (Mathf.PerlinNoise(seed, phase) - 0.5f) * 2f * a;
            float z = (Mathf.PerlinNoise(phase + seed, phase - seed) - 0.5f) * 2f * a;
            bone.rotation = bone.rotation * Quaternion.Euler(x, y, z);
        }
    }
}
