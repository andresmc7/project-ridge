using System;
using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Opción de accesibilidad "Ayudas de sigilo" (POLISH_PASS 3.5): muestra el cono de visión de los Lurker pintado
    /// en el suelo. Apagada por defecto: la información sale del propio Lurker (ojos, cabeza, indicador, sonido).
    /// </summary>
    public static class StealthAids
    {
        private const string Key = "ridge.stealthAids";
        private static bool? enabled;

        public static event Action<bool> Changed;

        public static bool Enabled
        {
            get
            {
                if (!enabled.HasValue)
                {
                    try { enabled = PlayerPrefs.GetInt(Key, 0) == 1; } catch (Exception) { enabled = false; }
                }
                return enabled.Value;
            }
            set
            {
                if (enabled == value) return;
                enabled = value;
                try { PlayerPrefs.SetInt(Key, value ? 1 : 0); } catch (Exception) { }
                Changed?.Invoke(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { enabled = null; Changed = null; }
    }
}
