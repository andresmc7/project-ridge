using ProjectRidge.Items;
using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>
    /// HUD mínimo (POLISH_PASS 3.12): corazón arriba a la izquierda que solo aparece con la vida por debajo del 100%
    /// (o al tocarlo), viñeta roja con poca vida, aviso de recogida (ícono + "+1" junto al botón OBJETO, 1.2 s),
    /// aviso de interacción y mensajes breves. Sin contadores permanentes ni textos de estado.
    /// </summary>
    public class SurvivalHud : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private HoldToHeal healHold;

        [SerializeField] private CanvasGroup heart;
        [SerializeField] private Image heartImage;
        [SerializeField] private CanvasGroup lowHealthVignette;
        [SerializeField] private CanvasGroup pickup;
        [SerializeField] private Image pickupIcon;
        [SerializeField] private Text pickupLabel;
        [SerializeField] private Text promptLabel;
        [SerializeField] private Text toastLabel;
        [SerializeField] private CanvasGroup damageFlash;

        [Tooltip("Segundos que se ve el corazón tras tocarlo con la vida llena.")]
        [Min(0f)] [SerializeField] private float heartAfterTouch = 2f;
        [Tooltip("Por debajo de esta fracción de vida aparece la viñeta roja (y el corazón late).")]
        [Range(0f, 1f)] [SerializeField] private float lowHealth = 0.5f;
        [Tooltip("Opacidad máxima de la viñeta (vida casi cero).")]
        [Range(0f, 1f)] [SerializeField] private float vignetteMax = 0.85f;
        [Tooltip("Segundos del aviso de recogida.")]
        [Min(0.1f)] [SerializeField] private float pickupSeconds = 1.2f;
        [Min(0.1f)] [SerializeField] private float toastSeconds = 2.2f;
        [Min(0.1f)] [SerializeField] private float damageFlashFade = 2.5f;
        [SerializeField] private string interactKeyHint = "E";

        private readonly System.Collections.Generic.Dictionary<ItemDefinition, int> counts = new System.Collections.Generic.Dictionary<ItemDefinition, int>();
        private float toastUntil, pickupUntil;
        private Interactable shownPrompt;

        /// <summary>Opacidad actual del corazón (para las pruebas).</summary>
        public float HeartAlpha => heart != null ? heart.alpha : 0f;
        /// <summary>Aviso de recogida visible (para las pruebas).</summary>
        public bool PickupShown => pickup != null && pickup.alpha > 0.5f;

        private void OnEnable()
        {
            inventory.Changed += OnInventory;
            interactor.Notified += OnNotify;
            health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            inventory.Changed -= OnInventory;
            interactor.Notified -= OnNotify;
            health.Damaged -= OnDamaged;
        }

        private void OnInventory(ItemDefinition item, int count)
        {
            counts.TryGetValue(item, out int before);
            counts[item] = count;
            if (count <= before) return; // solo al ganar
            pickupLabel.text = "+" + (count - before);
            pickupIcon.sprite = item.icon;
            pickupIcon.enabled = item.icon != null;
            if (item.icon == null) pickupLabel.text = "+" + (count - before) + " " + item.shortLabel;
            pickupUntil = Time.time + pickupSeconds;
        }

        private void OnNotify(string message)
        {
            toastLabel.text = message;
            toastUntil = Time.time + toastSeconds;
        }

        private void OnDamaged(float amount)
        {
            if (damageFlash != null) damageFlash.alpha = 1f;
        }

        private void Update()
        {
            float frac = health.Current / health.Max;
            bool touched = healHold != null && Time.unscaledTime - healHold.LastTouch < heartAfterTouch;
            float want = frac < 0.999f || touched ? 1f : 0f;
            heart.alpha = Mathf.MoveTowards(heart.alpha, want, Time.unscaledDeltaTime * 4f);
            // Late con poca vida.
            float beat = frac < lowHealth ? 1f + 0.12f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(5f, 3f, frac / lowHealth))), 8f) : 1f;
            heartImage.rectTransform.localScale = Vector3.one * beat;
            heartImage.color = Color.Lerp(new Color(0.85f, 0.12f, 0.1f, 1f), new Color(0.92f, 0.88f, 0.82f, 0.9f), Mathf.InverseLerp(0.2f, 0.8f, frac));
            lowHealthVignette.alpha = health.IsDead ? 0f : Mathf.InverseLerp(lowHealth, 0f, frac) * vignetteMax;

            pickup.alpha = Mathf.Clamp01((pickupUntil - Time.time) / 0.25f);

            var current = interactor.Current;
            if (current != shownPrompt)
            {
                shownPrompt = current;
                promptLabel.text = current != null ? "[" + interactKeyHint + "]  " + current.Prompt : "";
            }

            float toastAlpha = Mathf.Clamp01((toastUntil - Time.time) / 0.4f);
            var c = toastLabel.color; c.a = toastAlpha; toastLabel.color = c;

            if (damageFlash != null && damageFlash.alpha > 0f)
                damageFlash.alpha = Mathf.MoveTowards(damageFlash.alpha, 0f, damageFlashFade * Time.deltaTime);
        }
    }
}
