using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>
    /// Botón ACCIÓN táctil (POLISH_PASS 3.1): el ícono dice qué hará según el contexto; sin contexto casi desaparece.
    /// El toque lo envía un OnScreenButton (botón A del Gamepad virtual), igual que el control y el teclado.
    /// </summary>
    public class ActionButton : MonoBehaviour
    {
        [SerializeField] private PlayerActions actions;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text label;
        [Tooltip("Alfa sin contexto (el botón sigue ahí pero no llama la atención).")]
        [Range(0f, 1f)] [SerializeField] private float idleAlpha = 0.15f;
        [Min(0.01f)] [SerializeField] private float fade = 0.15f;

        private PlayerActions.Context shown = (PlayerActions.Context)(-1);

        /// <summary>Texto corto de cada contexto (el ícono definitivo llega con la tipografía del HUD en P8).</summary>
        public static string Glyph(PlayerActions.Context c)
        {
            switch (c)
            {
                case PlayerActions.Context.Inspect: return "LOOK";
                case PlayerActions.Context.Use: return "USE";
                case PlayerActions.Context.PickUp: return "TAKE";
                case PlayerActions.Context.Search: return "SEARCH";
                case PlayerActions.Context.Takedown: return "TAKE\nDOWN";
                case PlayerActions.Context.Strike: return "STRIKE";
                case PlayerActions.Context.Shove: return "SHOVE";
                default: return "";
            }
        }

        private void Update()
        {
            var c = actions.Current;
            if (c != shown)
            {
                shown = c;
                label.text = Glyph(c);
            }
            float target = c == PlayerActions.Context.None ? idleAlpha : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / fade);
        }
    }
}
