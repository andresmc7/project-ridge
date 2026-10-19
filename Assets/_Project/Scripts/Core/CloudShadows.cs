using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectRidge.Core
{
    /// <summary>Desplaza la "cookie" de nubes de la luna: sombras de nubes que cruzan el bosque lentamente.</summary>
    [RequireComponent(typeof(Light))]
    public class CloudShadows : MonoBehaviour
    {
        [SerializeField] private Vector2 velocity = new Vector2(0.6f, 0.25f);
        [SerializeField] private float cookieSize = 60f;

        private UniversalAdditionalLightData data;
        private Vector2 offset;

        private void Awake()
        {
            data = GetComponent<UniversalAdditionalLightData>();
            if (data != null) data.lightCookieSize = new Vector2(cookieSize, cookieSize);
        }

        private void Update()
        {
            if (data == null) return;
            offset += velocity * Time.deltaTime;
            data.lightCookieOffset = offset;
        }
    }
}
