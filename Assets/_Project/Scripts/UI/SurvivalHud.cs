using System.Text;
using ProjectRidge.Items;
using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>HUD sobrio: vida, arma, recursos, aviso de interacción y mensajes breves ("+1 Medkit").</summary>
    public class SurvivalHud : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private ItemDefinition[] shownItems;

        [SerializeField] private Image healthFill;
        [SerializeField] private Text resourcesLabel;
        [SerializeField] private Text weaponLabel;
        [SerializeField] private Text promptLabel;
        [SerializeField] private Text toastLabel;
        [SerializeField] private CanvasGroup damageFlash;

        [Min(0.1f)] [SerializeField] private float toastSeconds = 2.2f;
        [Min(0.1f)] [SerializeField] private float damageFlashFade = 2.5f;
        [SerializeField] private string interactKeyHint = "E";

        private readonly StringBuilder builder = new StringBuilder(64);
        private float toastUntil;
        private Interactable shownPrompt;
        private int shownDurability = -1;
        private bool dirty = true;

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

        private void OnInventory(ItemDefinition item, int count) => dirty = true;

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
            healthFill.fillAmount = health.Current / health.Max;
            // Vida baja: la barra se enrojece.
            healthFill.color = Color.Lerp(new Color(0.75f, 0.12f, 0.08f, 0.95f), new Color(0.86f, 0.82f, 0.74f, 0.9f), Mathf.InverseLerp(0.2f, 0.5f, health.Current / health.Max));

            if (dirty)
            {
                dirty = false;
                builder.Clear();
                foreach (var item in shownItems)
                {
                    if (builder.Length > 0) builder.Append("     ");
                    builder.Append(item.shortLabel).Append("  ").Append(inventory.Count(item));
                }
                resourcesLabel.text = builder.ToString();
            }

            if (combat != null && weaponLabel != null && combat.ClubDurability != shownDurability)
            {
                shownDurability = combat.ClubDurability;
                builder.Clear();
                if (combat.HasClub)
                {
                    builder.Append("CLUB  ");
                    for (int i = 0; i < combat.ClubMaxDurability; i++) builder.Append(i < shownDurability ? '|' : '.');
                }
                else
                {
                    builder.Append("NO CLUB");
                }
                weaponLabel.text = builder.ToString();
                dirty = true;
            }

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
