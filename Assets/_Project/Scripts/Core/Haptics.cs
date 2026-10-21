namespace ProjectRidge.Core
{
    /// <summary>
    /// Hápticos (POLISH_PASS 3.1): pulso leve, fuerte y zumbido continuo. En iOS llama al plugin nativo (P8); en el
    /// editor y en otras plataformas no hace nada. La API ya la usan la captura del OVNI y el Lurker.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Zumbido continuo encendido (la captura).</summary>
        public static bool ContinuousOn { get; private set; }

        /// <summary>Impactos pedidos desde que arrancó (para las pruebas: en el editor no vibra nada).</summary>
        public static int ImpactCount { get; private set; }

        public static void Light() { ImpactCount++; Native.Impact(0); }
        public static void Heavy() { ImpactCount++; Native.Impact(2); }

        public static void Continuous(bool on)
        {
            if (ContinuousOn == on) return;
            ContinuousOn = on;
            Native.Continuous(on);
        }

        /// <summary>Puente con el plugin de iOS; vacío fuera del dispositivo.</summary>
        static class Native
        {
#if UNITY_IOS && !UNITY_EDITOR
            [System.Runtime.InteropServices.DllImport("__Internal")] static extern void RidgeHapticImpact(int style);
            [System.Runtime.InteropServices.DllImport("__Internal")] static extern void RidgeHapticContinuous(bool on);
            public static void Impact(int style) => RidgeHapticImpact(style);
            public static void Continuous(bool on) => RidgeHapticContinuous(on);
#else
            public static void Impact(int style) { }
            public static void Continuous(bool on) { }
#endif
        }
    }
}
