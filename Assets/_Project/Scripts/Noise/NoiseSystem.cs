using System;
using UnityEngine;

namespace ProjectRidge.Noise
{
    /// <summary>
    /// Bus central de ruido. Todo lo que hace ruido llama a Emit; toda IA que escucha se suscribe a NoiseEmitted.
    /// </summary>
    public static class NoiseSystem
    {
        public static event Action<NoiseEvent> NoiseEmitted;

        public static void Emit(Vector3 position, float radius, NoiseType type, GameObject source = null)
        {
            if (radius <= 0f) return;
            NoiseEmitted?.Invoke(new NoiseEvent(position, radius, type, source));
        }

        // Con "Enter Play Mode Options" sin domain reload, los estáticos sobreviven entre sesiones de play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => NoiseEmitted = null;
    }
}
