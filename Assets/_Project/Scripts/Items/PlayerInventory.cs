using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Inventario mínimo del PoC: cantidades por tipo de objeto, con tope por tipo.</summary>
    public class PlayerInventory : MonoBehaviour
    {
        private readonly Dictionary<ItemDefinition, int> counts = new Dictionary<ItemDefinition, int>();

        public event Action<ItemDefinition, int> Changed;

        public int Count(ItemDefinition item) => item != null && counts.TryGetValue(item, out int n) ? n : 0;

        /// <summary>Agrega hasta el tope; devuelve cuántos entraron.</summary>
        public int Add(ItemDefinition item, int amount)
        {
            int current = Count(item);
            int accepted = Mathf.Clamp(item.maxCarry - current, 0, amount);
            if (accepted <= 0) return 0;
            counts[item] = current + accepted;
            Changed?.Invoke(item, counts[item]);
            return accepted;
        }

        public bool TryRemove(ItemDefinition item, int amount = 1)
        {
            int current = Count(item);
            if (current < amount) return false;
            counts[item] = current - amount;
            Changed?.Invoke(item, counts[item]);
            return true;
        }
    }
}
