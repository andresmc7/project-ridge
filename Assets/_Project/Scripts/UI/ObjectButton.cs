using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>
    /// Botón OBJETO táctil (POLISH_PASS 3.1): muestra el objeto activo y su cantidad.
    /// Tocar = lanzar al objetivo inteligente. Mantener y arrastrar = apuntar con arco; soltar lanza; volver al botón
    /// cancela. Deslizar rápido arriba o abajo sobre el botón = cambiar de objeto.
    /// </summary>
    public class ObjectButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private PlayerItems items;
        [SerializeField] private Text label;
        [SerializeField] private Text count;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Arrastre (unidades del canvas) a partir del cual deja de ser un toque y pasa a apuntar.")]
        [Min(1f)] [SerializeField] private float aimDeadzone = 24f;
        [Tooltip("Mantener este tiempo (s) también pasa a apuntar.")]
        [Min(0f)] [SerializeField] private float holdToAim = 0.3f;
        [Tooltip("Deslizar vertical (unidades) en menos de swipeTime cambia de objeto.")]
        [Min(1f)] [SerializeField] private float swipeDistance = 60f;
        [Min(0.05f)] [SerializeField] private float swipeTime = 0.3f;
        [Tooltip("Radio del botón: soltar dentro cancela la puntería.")]
        [Min(1f)] [SerializeField] private float cancelRadius = 46f;
        [Range(0f, 1f)] [SerializeField] private float emptyAlpha = 0.3f;

        private RectTransform rect;
        private Vector2 start;
        private float downTime;
        private bool down, swiped;

        private void Awake() => rect = (RectTransform)transform;

        public void OnPointerDown(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out start);
            downTime = Time.unscaledTime;
            down = true;
            swiped = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!down) return;
            Vector2 d = Local(e) - start;
            float held = Time.unscaledTime - downTime;
            if (!items.Aiming && !swiped && held < swipeTime && Mathf.Abs(d.y) > swipeDistance && Mathf.Abs(d.x) < Mathf.Abs(d.y) * 0.5f)
            {
                swiped = true;
                items.Cycle();
                return;
            }
            if (swiped) return;
            if (!items.Aiming && (d.magnitude > aimDeadzone || held > holdToAim)) items.BeginAim();
            if (items.Aiming) items.UpdateAim(d);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!down) return;
            down = false;
            if (swiped) return;
            if (items.Aiming)
            {
                if (Local(e).magnitude < cancelRadius) items.CancelAim();
                else items.ReleaseAim();
                return;
            }
            items.UseSmart();
        }

        private void Update()
        {
            // Mantener sin moverse también apunta.
            if (down && !swiped && !items.Aiming && Time.unscaledTime - downTime > holdToAim) items.BeginAim();
            var a = items.Active;
            label.text = a != null ? a.displayName.ToUpperInvariant() : "";
            int n = items.ActiveCount;
            count.text = n.ToString();
            group.alpha = n > 0 ? 1f : emptyAlpha;
        }

        private Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 p);
            return p;
        }
    }
}
