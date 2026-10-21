using System.Collections.Generic;
using UnityEngine;

namespace ProjectRidge.CameraRig
{
    /// <summary>
    /// Objeto que se desvanece cuando tapa al jugador (árboles, rocas grandes, paredes).
    /// Cambia a materiales "faded" (DitherFade: perforados con dithering, no transparentes) solo mientras está
    /// desvanecido; el resto del tiempo usa los opacos.
    /// </summary>
    public class OcclusionFadeable : MonoBehaviour
    {
        private static readonly int Fade = Shader.PropertyToID("_Fade");
        public static readonly List<OcclusionFadeable> All = new List<OcclusionFadeable>();

        [SerializeField] private Renderer[] renderers;
        [Tooltip("Pares opaco → transparente, alineados por índice.")]
        [SerializeField] private Material[] opaqueMaterials;
        [SerializeField] private Material[] fadedMaterials;

        private Material[][] originals;
        private Material[][] fadedSets;
        private MaterialPropertyBlock block;
        private float alpha = 1f;
        private bool usingFaded;

        public Renderer[] Renderers => renderers;
        /// <summary>Alfa objetivo que el fader pide este frame (1 = opaco).</summary>
        public float TargetAlpha { get; set; } = 1f;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            originals = new Material[renderers.Length][];
            fadedSets = new Material[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
            {
                originals[r] = renderers[r].sharedMaterials;
                fadedSets[r] = new Material[originals[r].Length];
                for (int m = 0; m < originals[r].Length; m++)
                    fadedSets[r][m] = Map(originals[r][m]);
            }
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private Material Map(Material opaque)
        {
            for (int i = 0; i < opaqueMaterials.Length; i++)
                if (opaqueMaterials[i] == opaque) return fadedMaterials[i];
            return opaque;
        }

        public Bounds WorldBounds
        {
            get
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return b;
            }
        }

        public void Step(float speed, float dt)
        {
            if (Mathf.Approximately(alpha, TargetAlpha) && (usingFaded == alpha < 1f)) return;
            alpha = Mathf.MoveTowards(alpha, TargetAlpha, speed * dt);

            bool wantFaded = alpha < 0.999f;
            if (wantFaded != usingFaded)
            {
                usingFaded = wantFaded;
                for (int r = 0; r < renderers.Length; r++)
                {
                    renderers[r].sharedMaterials = usingFaded ? fadedSets[r] : originals[r];
                    if (!usingFaded) renderers[r].SetPropertyBlock(null);
                }
            }
            if (!usingFaded) return;

            block.SetFloat(Fade, alpha);
            foreach (var r in renderers) r.SetPropertyBlock(block);
        }
    }
}
