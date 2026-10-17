using UnityEngine;

namespace ProjectRidge.Noise
{
    /// <summary>Un ruido en el mundo. Radio = distancia máxima a la que se oye sin obstáculos.</summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseType Type;
        /// <summary>Quién lo hizo (para que una IA ignore sus propios ruidos). Puede ser null.</summary>
        public readonly GameObject Source;

        public NoiseEvent(Vector3 position, float radius, NoiseType type, GameObject source = null)
        {
            Position = position;
            Radius = radius;
            Type = type;
            Source = source;
        }
    }
}
