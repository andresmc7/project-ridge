namespace ProjectRidge.Player
{
    /// <summary>Modo de locomoción actual del jugador. El sistema de ruido (M2) lo usa para decidir cuánto ruido emite.</summary>
    public enum MovementMode
    {
        Idle,
        Crouch,
        Walk,
        Jog,
        Sprint
    }
}
