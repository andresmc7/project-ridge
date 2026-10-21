using UnityEngine;

namespace ProjectRidge.CameraRig
{
    /// <summary>
    /// Pone el plano focal del tilt-shift a la altura del papá en pantalla (POLISH_PASS 3.6): con la cámara que mira
    /// adelante o encuadra una amenaza, el papá ya no está siempre en el mismo sitio y debe seguir nítido.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TiltShiftFocus : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Altura del punto enfocado sobre los pies (m).")]
        [SerializeField] private float height = 0.9f;
        [Tooltip("Suavizado (1/s).")]
        [SerializeField] private float follow = 8f;

        private static readonly int FocusId = Shader.PropertyToID("_TiltFocusY");
        private Camera cam;
        private float focus = -1f;

        private void Awake() => cam = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (target == null) return;
            float y = Mathf.Clamp(cam.WorldToViewportPoint(target.position + Vector3.up * height).y, 0.05f, 0.95f);
            focus = focus < 0f ? y : Mathf.Lerp(focus, y, 1f - Mathf.Exp(-follow * Time.deltaTime));
            Shader.SetGlobalFloat(FocusId, focus);
        }

        private void OnDisable() => Shader.SetGlobalFloat(FocusId, 0f);
    }
}
