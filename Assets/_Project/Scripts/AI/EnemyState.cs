namespace ProjectRidge.AI
{
    /// <summary>
    /// Estados del spec: PATROL → INVESTIGATE → SEARCH → CHASE → ATTACK → SEARCH → PATROL.
    /// Stunned y Dead son interrupciones del combate (garrote y bengala), fuera del ciclo.
    /// </summary>
    public enum EnemyState
    {
        Patrol,
        Investigate,
        Search,
        Chase,
        Attack,
        Stunned,
        Dead,
        /// <summary>Atraído por una bengala de mano: va hacia ella y se queda mirándola mientras arde.</summary>
        Lured
    }
}
