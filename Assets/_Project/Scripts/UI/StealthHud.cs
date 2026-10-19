using ProjectRidge.AI;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>
    /// Indicador discreto de detección: SAFE / ALERT / HUNTED y una barra que se llena con la sospecha del Lurker
    /// o la exposición al reflector del OVNI. En SAFE con la barra vacía casi desaparece.
    /// </summary>
    public class StealthHud : MonoBehaviour
    {
        private enum Level { Safe, Hidden, Alert, Hunted }

        [SerializeField] private Text stateLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Image meterFill;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Opcional: si hay OVNI en la escena.")]
        [SerializeField] private UFOExposure ufoExposure;

        [Header("Estilo")]
        [SerializeField] private Color safeColor = new Color(0.72f, 0.78f, 0.76f, 1f);
        [SerializeField] private Color hiddenColor = new Color(0.55f, 0.88f, 0.62f, 1f);
        [SerializeField] private Color alertColor = new Color(1f, 0.74f, 0.32f, 1f);
        [SerializeField] private Color huntedColor = new Color(0.86f, 0.14f, 0.1f, 1f);
        [Range(0f, 1f)] [SerializeField] private float idleAlpha = 0.25f;
        [Min(0.1f)] [SerializeField] private float meterSmoothing = 8f;
        [Min(0.1f)] [SerializeField] private float huntedPulseSpeed = 5f;

        private Level shown = (Level)(-1);
        private string shownDetail;
        private float meter;
        private ProjectRidge.Player.PlayerVisibility player;

        private void Update()
        {
            float suspicion = 0f;
            bool hunted = false, alert = false;
            foreach (var enemy in EnemyController.Active)
            {
                suspicion = Mathf.Max(suspicion, enemy.Perception.Suspicion);
                if (enemy.State == EnemyState.Chase || enemy.State == EnemyState.Attack) hunted = true;
                else if (enemy.State == EnemyState.Investigate || enemy.State == EnemyState.Search) alert = true;
            }

            float exposure = ufoExposure != null ? ufoExposure.Exposure : 0f;
            bool lit = ufoExposure != null && ufoExposure.IsLit;
            foreach (var s in Searchlight.Active)
            {
                exposure = Mathf.Max(exposure, s.Exposure);
                lit |= s.PlayerLit;
            }
            if (exposure >= 1f) hunted = true;
            if (player == null) player = FindFirstObjectByType<ProjectRidge.Player.PlayerVisibility>();
            bool hidden = player != null && player.IsHidden;

            Level level = hunted ? Level.Hunted : (alert || lit || suspicion > 0.05f || exposure > 0.05f) ? Level.Alert : hidden ? Level.Hidden : Level.Safe;
            float target = hunted ? 1f : Mathf.Max(suspicion, exposure);
            meter = Mathf.Lerp(meter, target, 1f - Mathf.Exp(-meterSmoothing * Time.deltaTime));
            meterFill.fillAmount = meter;

            string detail = lit ? "IN THE LIGHT. HIDE!" : level == Level.Hunted ? "RUN. HIDE." : level == Level.Alert ? "SOMETHING NOTICED YOU" : level == Level.Hidden ? "THEY CAN'T SEE YOU" : "";
            if (level != shown || detail != shownDetail)
            {
                shown = level;
                shownDetail = detail;
                stateLabel.text = level == Level.Hunted ? "HUNTED" : level == Level.Alert ? "ALERT" : level == Level.Hidden ? "HIDDEN" : "SAFE";
                detailLabel.text = detail;
            }

            Color c = level == Level.Hunted ? huntedColor : level == Level.Alert ? alertColor : level == Level.Hidden ? hiddenColor : safeColor;
            stateLabel.color = c;
            detailLabel.color = new Color(c.r, c.g, c.b, 0.75f);
            meterFill.color = c;

            float alpha = level == Level.Safe && meter < 0.02f ? idleAlpha : 1f;
            if (level == Level.Hunted) alpha *= 0.75f + 0.25f * Mathf.Sin(Time.time * huntedPulseSpeed);
            group.alpha = Mathf.MoveTowards(group.alpha, alpha, Time.deltaTime * 3f);
        }
    }
}
