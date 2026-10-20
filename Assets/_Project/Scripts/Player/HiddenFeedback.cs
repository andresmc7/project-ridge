using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Estar oculto se siente, no se lee (POLISH_PASS 3.2): sin texto "HIDDEN". La pantalla se oscurece un poco en
    /// los bordes (un Volume con viñeta), el sonido se amortigua (paso bajo en el listener) y el papá queda en
    /// silueta (su color base se oscurece). Todo con un fundido suave.
    /// </summary>
    public class HiddenFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerVisibility visibility;
        [Tooltip("Volume global con la viñeta de 'oculto' (su peso va de 0 a 1).")]
        [SerializeField] private Volume vignette;
        [SerializeField] private AudioLowPassFilter lowPass;
        [Tooltip("Renderers del papá que pasan a silueta.")]
        [SerializeField] private Renderer[] body;
        [Tooltip("Segundos del fundido al entrar o salir del escondite.")]
        [Min(0.01f)] [SerializeField] private float fade = 0.35f;
        [Tooltip("Corte del paso bajo (Hz) estando oculto; fuera, 22 kHz (sin efecto).")]
        [SerializeField] private float hiddenCutoff = 2600f;
        [Tooltip("Brillo del papá estando oculto (1 = normal).")]
        [Range(0f, 1f)] [SerializeField] private float silhouette = 0.35f;

        private const float OpenCutoff = 22000f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private Color[][] baseColors;
        private float amount;
        private float applied = -1f;

        /// <summary>0 = a la vista, 1 = oculto del todo.</summary>
        public float Amount => amount;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            baseColors = new Color[body.Length][];
            for (int r = 0; r < body.Length; r++)
            {
                var mats = body[r].sharedMaterials;
                baseColors[r] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                    baseColors[r][m] = mats[m] != null && mats[m].HasProperty(BaseColor) ? mats[m].GetColor(BaseColor) : Color.white;
            }
        }

        private void Update()
        {
            amount = Mathf.MoveTowards(amount, visibility.IsHidden ? 1f : 0f, Time.deltaTime / fade);
            if (Mathf.Abs(amount - applied) < 0.001f) return;
            applied = amount;
            if (vignette != null) vignette.weight = amount;
            if (lowPass != null)
            {
                lowPass.enabled = amount > 0.001f;
                lowPass.cutoffFrequency = Mathf.Lerp(OpenCutoff, hiddenCutoff, amount);
            }
            float k = Mathf.Lerp(1f, silhouette, amount);
            for (int r = 0; r < body.Length; r++)
            {
                if (body[r] == null) continue;
                for (int m = 0; m < baseColors[r].Length; m++)
                {
                    body[r].GetPropertyBlock(block, m);
                    Color c = baseColors[r][m];
                    block.SetColor(BaseColor, new Color(c.r * k, c.g * k, c.b * k, c.a));
                    body[r].SetPropertyBlock(block, m);
                }
            }
        }
    }
}
