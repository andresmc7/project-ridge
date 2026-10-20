using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace ProjectRidge.UI
{
    /// <summary>
    /// Joystick flotante (POLISH_PASS 3.1): aparece donde se apoya el pulgar dentro de la zona izquierda de la pantalla
    /// y escribe en el stick izquierdo del Gamepad virtual. Solo se ve un anillo tenue. Arrastrar más allá del anillo
    /// fija el sprint (el anillo se ilumina); empujar al borde y mantener también (lo decide el controlador).
    /// </summary>
    public class FloatingStick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [InputControl(layout = "Vector2")]
        [SerializeField] private string stickPath = "<Gamepad>/leftStick";
        [SerializeField] private PlayerInputHandler input;
        [Tooltip("Anillo visible (se mueve al punto de apoyo).")]
        [SerializeField] private RectTransform ring;
        [SerializeField] private RectTransform knob;
        [SerializeField] private UnityEngine.UI.Image ringImage;
        [Tooltip("Radio del anillo en unidades del canvas.")]
        [Min(10f)] [SerializeField] private float range = 80f;
        [Tooltip("Arrastrar más allá de range × esto fija el sprint.")]
        [Min(1f)] [SerializeField] private float sprintOverdrag = 1.3f;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] private Color sprintColor = new Color(1f, 0.85f, 0.55f, 0.75f);

        private RectTransform area;
        private Vector2 origin;
        private bool held;

        protected override string controlPathInternal { get => stickPath; set => stickPath = value; }

        private void Awake()
        {
            area = (RectTransform)transform;
            if (ring != null) ring.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out origin);
            held = true;
            if (ring != null)
            {
                ring.gameObject.SetActive(true);
                ring.anchoredPosition = origin;
                ring.sizeDelta = Vector2.one * range * 2f;
            }
            Move(Vector2.zero);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!held) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out Vector2 p);
            Vector2 d = p - origin;
            if (d.magnitude > range * sprintOverdrag && input != null) input.LatchSprint();
            Move(Vector2.ClampMagnitude(d / range, 1f));
        }

        public void OnPointerUp(PointerEventData e)
        {
            held = false;
            Move(Vector2.zero);
            if (ring != null) ring.gameObject.SetActive(false);
        }

        private void Move(Vector2 v)
        {
            SendValueToControl(v);
            if (knob != null) knob.anchoredPosition = v * range;
        }

        private void Update()
        {
            if (ringImage != null && input != null)
                ringImage.color = input.SprintRequested ? sprintColor : idleColor;
        }
    }
}
