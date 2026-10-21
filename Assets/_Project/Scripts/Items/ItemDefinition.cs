using UnityEngine;

namespace ProjectRidge.Items
{
    [CreateAssetMenu(menuName = "Project Ridge/Item Definition", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName = "Item";
        [Tooltip("Texto corto para el HUD.")]
        public string shortLabel = "ITEM";
        [Min(1)] public int maxCarry = 3;
        [Tooltip("Ícono del HUD (botón OBJETO y aviso de recogida).")]
        public Sprite icon;
    }
}
