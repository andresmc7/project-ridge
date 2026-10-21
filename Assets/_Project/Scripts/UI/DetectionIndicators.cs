using System.Collections.Generic;
using ProjectRidge.AI;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>
    /// Indicador de detección (POLISH_PASS 3.5, estilo The Last of Us): un arco pequeño sobre la cabeza de cada Lurker
    /// que se llena de blanco a naranja mientras te va detectando; lleno y rojo cuando te caza. Solo aparece con la
    /// sospecha por encima de un mínimo. Si el Lurker está fuera de la pantalla, una flecha en el borde apunta hacia él.
    /// </summary>
    public class DetectionIndicators : MonoBehaviour
    {
        [Tooltip("Plantilla del arco (Image con relleno radial). Se clona por Lurker.")]
        [SerializeField] private Image arcTemplate;
        [Tooltip("Plantilla de la flecha de borde (apunta hacia +Y).")]
        [SerializeField] private Image arrowTemplate;
        [Tooltip("Sospecha mínima para mostrar el indicador.")]
        [Range(0f, 1f)] [SerializeField] private float showAbove = 0.05f;
        [Tooltip("Metros sobre la cabeza.")]
        [SerializeField] private float headOffset = 0.55f;
        [Tooltip("Margen del borde de pantalla para la flecha (unidades del canvas).")]
        [SerializeField] private float edgeInset = 70f;
        [SerializeField] private Color startColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color fullColor = new Color(1f, 0.55f, 0.12f, 1f);
        [SerializeField] private Color huntColor = new Color(1f, 0.12f, 0.06f, 1f);

        private class Item { public Image arc, arrow; public Transform head; }
        private readonly Dictionary<EnemyController, Item> items = new Dictionary<EnemyController, Item>();
        private RectTransform rect;
        private Camera cam;

        /// <summary>QA: el indicador de este Lurker (arco visible, flecha visible, relleno).</summary>
        public (bool arc, bool arrow, float fill) State(EnemyController e)
        {
            if (!items.TryGetValue(e, out var it)) return (false, false, 0f);
            return (it.arc.enabled, it.arrow.enabled, it.arc.fillAmount);
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            arcTemplate.gameObject.SetActive(false);
            arrowTemplate.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            foreach (var e in EnemyController.Active)
            {
                if (!items.TryGetValue(e, out var it)) items[e] = it = Create(e);
                UpdateItem(e, it);
            }
            // Los que ya no están activos (desactivados o muertos) no muestran nada.
            foreach (var kv in items)
                if (kv.Key == null || !kv.Key.isActiveAndEnabled) { kv.Value.arc.enabled = false; kv.Value.arrow.enabled = false; }
        }

        private Item Create(EnemyController e)
        {
            var it = new Item
            {
                arc = Instantiate(arcTemplate, rect),
                arrow = Instantiate(arrowTemplate, rect),
            };
            it.arc.gameObject.SetActive(true);
            it.arrow.gameObject.SetActive(true);
            var anim = e.GetComponentInChildren<Animator>();
            it.head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : e.transform;
            return it;
        }

        private void UpdateItem(EnemyController e, Item it)
        {
            float s = e.Perception.Suspicion;
            bool hunting = e.IsHunting;
            bool show = !e.IsDead && (hunting || s > showAbove);
            if (!show) { it.arc.enabled = false; it.arrow.enabled = false; return; }

            float fill = hunting ? 1f : Mathf.Clamp01(s);
            Color c = hunting ? huntColor : Color.Lerp(startColor, fullColor, fill);
            Vector3 world = it.head.position + Vector3.up * headOffset;
            Vector3 vp = cam.WorldToViewportPoint(world);
            bool onScreen = vp.z > 0f && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f;
            Vector2 size = rect.rect.size;
            if (onScreen)
            {
                it.arrow.enabled = false;
                it.arc.enabled = true;
                it.arc.rectTransform.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
                it.arc.fillAmount = fill;
                it.arc.color = c;
                return;
            }
            // Fuera de pantalla: flecha en el borde, en la dirección del Lurker.
            it.arc.enabled = false;
            it.arrow.enabled = true;
            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z < 0f) dir = -dir; // detrás de la cámara: la proyección se invierte
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.down;
            dir.Normalize();
            Vector2 half = size * 0.5f - Vector2.one * edgeInset;
            float k = Mathf.Min(half.x / Mathf.Max(1e-4f, Mathf.Abs(dir.x)), half.y / Mathf.Max(1e-4f, Mathf.Abs(dir.y)));
            it.arrow.rectTransform.anchoredPosition = dir * k;
            it.arrow.rectTransform.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(dir.x, dir.y, 0f));
            it.arrow.color = c;
        }
    }
}
