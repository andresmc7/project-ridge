namespace ProjectRidge.AI
{
    /// <summary>Estados del spec: PATROL → INVESTIGATE → SEARCH → CHASE → ATTACK → SEARCH → PATROL.</summary>
    public enum EnemyState
    {
        Patrol,
        Investigate,
        Search,
        Chase,
        Attack
    }
}
