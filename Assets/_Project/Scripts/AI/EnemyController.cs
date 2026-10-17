using System;
using ProjectRidge.Core;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Máquina de estados explícita del Lurker sobre NavMeshAgent.
    /// Patrol → Investigate (ruido o sospecha) → Search (llega y no encuentra) → Patrol.
    /// Cualquier estado → Chase (sospecha llena viéndote) → Attack (en rango) → Chase / Search (te pierde).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyConfig config;
        [SerializeField] private EnemyPerception perception;
        [SerializeField] private Transform[] patrolPoints;

        private enum AttackPhase { Windup, Active, Recovery }

        private NavMeshAgent agent;
        private float stateTime;
        private int patrolIndex;
        private float waitTimer;
        private Vector3 investigateTarget;
        private Vector3 searchCenter;
        private bool hasSearchPoint;
        private float lostSightTimer;
        private AttackPhase attackPhase;
        private bool attackLanded;
        private float zigzagSeed;
        private float nextRepath;

        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public event Action<EnemyState> StateChanged;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            zigzagSeed = UnityEngine.Random.value * 10f;
        }

        private void OnEnable() => perception.NoiseHeard += OnNoiseHeard;
        private void OnDisable() => perception.NoiseHeard -= OnNoiseHeard;

        private void Start() => EnterPatrol();

        private void Update()
        {
            stateTime += Time.deltaTime;

            if (State != EnemyState.Chase && State != EnemyState.Attack
                && perception.CanSeeTarget && perception.Suspicion >= config.chaseThreshold)
            {
                EnterChase();
            }
            else if ((State == EnemyState.Patrol || State == EnemyState.Search)
                && perception.CanSeeTarget && perception.Suspicion >= config.investigateThreshold)
            {
                EnterInvestigate(perception.LastKnownPosition);
            }

            switch (State)
            {
                case EnemyState.Patrol: TickPatrol(); break;
                case EnemyState.Investigate: TickInvestigate(); break;
                case EnemyState.Search: TickSearch(); break;
                case EnemyState.Chase: TickChase(); break;
                case EnemyState.Attack: TickAttack(); break;
            }
        }

        // ---------- Patrol

        private void EnterPatrol()
        {
            SetState(EnemyState.Patrol);
            agent.speed = config.patrolSpeed;
            waitTimer = 0f;
            if (patrolPoints.Length > 0)
            {
                patrolIndex = NearestPatrolIndex();
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
        }

        private void TickPatrol()
        {
            if (patrolPoints.Length == 0 || !Arrived()) return;

            waitTimer += Time.deltaTime;
            if (waitTimer < config.patrolPause) return;
            waitTimer = 0f;
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }

        private int NearestPatrolIndex()
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                float d = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        // ---------- Investigate

        private void OnNoiseHeard(Vector3 position)
        {
            if (State == EnemyState.Chase || State == EnemyState.Attack) return;
            if (State == EnemyState.Investigate)
            {
                investigateTarget = position;
                if (stateTime >= config.alertFreeze) agent.SetDestination(SampleOnNavMesh(position));
                return;
            }
            EnterInvestigate(position);
        }

        private void EnterInvestigate(Vector3 target)
        {
            SetState(EnemyState.Investigate);
            investigateTarget = target;
            agent.speed = config.investigateSpeed;
            agent.isStopped = true; // quietud total antes de moverse
            agent.ResetPath();
        }

        private void TickInvestigate()
        {
            if (stateTime < config.alertFreeze)
            {
                FaceTowards(investigateTarget);
                return;
            }

            if (perception.CanSeeTarget && perception.HasLastKnownPosition)
                investigateTarget = perception.LastKnownPosition;

            if (agent.isStopped)
            {
                agent.isStopped = false;
                SetDestinationThrottled(investigateTarget, force: true);
                return;
            }
            SetDestinationThrottled(investigateTarget);

            if (Arrived() || stateTime > config.investigateTimeout)
                EnterSearch(transform.position);
        }

        // ---------- Search

        private void EnterSearch(Vector3 center)
        {
            SetState(EnemyState.Search);
            searchCenter = center;
            agent.isStopped = false;
            agent.speed = config.searchSpeed;
            hasSearchPoint = false;
            waitTimer = 0f;
        }

        private void TickSearch()
        {
            if (stateTime >= config.searchDuration)
            {
                perception.ForgetTarget();
                EnterPatrol();
                return;
            }

            if (!hasSearchPoint)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * config.searchRadius;
                agent.SetDestination(SampleOnNavMesh(searchCenter + new Vector3(offset.x, 0f, offset.y)));
                hasSearchPoint = true;
                waitTimer = 0f;
                return;
            }

            if (!Arrived()) return;

            // Pausa mirando alrededor antes del siguiente punto.
            waitTimer += Time.deltaTime;
            float sweep = Mathf.Sin(waitTimer / config.searchLookPause * Mathf.PI * 2f);
            transform.Rotate(0f, config.searchTurnSpeed * sweep * Time.deltaTime, 0f);
            if (waitTimer >= config.searchLookPause) hasSearchPoint = false;
        }

        // ---------- Chase

        private void EnterChase()
        {
            SetState(EnemyState.Chase);
            agent.isStopped = false;
            agent.speed = config.chaseSpeed;
            lostSightTimer = 0f;
        }

        private void TickChase()
        {
            var target = perception.Target;
            if (perception.CanSeeTarget)
            {
                lostSightTimer = 0f;
                float distance = Vector3.Distance(transform.position, target.transform.position);
                if (distance <= config.attackRange)
                {
                    EnterAttack();
                    return;
                }
                SetDestinationThrottled(ZigzagTowards(target.transform.position, distance));
                return;
            }

            lostSightTimer += Time.deltaTime;
            if (perception.HasLastKnownPosition)
                SetDestinationThrottled(perception.LastKnownPosition);
            if (lostSightTimer >= config.loseSightTime)
                EnterSearch(perception.HasLastKnownPosition ? perception.LastKnownPosition : transform.position);
        }

        /// <summary>Arranques erráticos: destino desplazado a los lados mientras está lejos.</summary>
        private Vector3 ZigzagTowards(Vector3 targetPos, float distance)
        {
            if (distance < config.zigzagMinDistance || config.zigzagAmplitude <= 0f) return targetPos;
            Vector3 dir = (targetPos - transform.position);
            dir.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
            float wave = Mathf.Sign(Mathf.Sin((Time.time + zigzagSeed) * config.zigzagFrequency * Mathf.PI * 2f));
            return targetPos + side * (wave * config.zigzagAmplitude);
        }

        // ---------- Attack

        private void EnterAttack()
        {
            SetState(EnemyState.Attack);
            agent.isStopped = true;
            agent.ResetPath();
            attackPhase = AttackPhase.Windup;
            attackLanded = false;
        }

        private void TickAttack()
        {
            var target = perception.Target;
            switch (attackPhase)
            {
                case AttackPhase.Windup:
                    FaceTowards(target.transform.position);
                    if (stateTime >= config.attackWindup) attackPhase = AttackPhase.Active;
                    break;
                case AttackPhase.Active:
                    if (!attackLanded && InAttackArc(target.transform.position))
                    {
                        attackLanded = true; // un golpe por ataque, no daño por frame
                        if (target.TryGetComponent(out IDamageable damageable))
                            damageable.TakeDamage(config.attackDamage);
                    }
                    if (stateTime >= config.attackWindup + config.attackActive) attackPhase = AttackPhase.Recovery;
                    break;
                case AttackPhase.Recovery:
                    if (stateTime >= config.attackWindup + config.attackActive + config.attackRecovery)
                        EnterChase();
                    break;
            }
        }

        private bool InAttackArc(Vector3 targetPos)
        {
            Vector3 to = targetPos - transform.position;
            to.y = 0f;
            return to.magnitude <= config.attackRange && Vector3.Angle(transform.forward, to) <= config.attackArc * 0.5f;
        }

        // ---------- Utilidades

        private void SetState(EnemyState state)
        {
            stateTime = 0f;
            if (state == State && state != EnemyState.Patrol) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private bool Arrived()
        {
            return !agent.pathPending && agent.remainingDistance <= Mathf.Max(config.arriveDistance, agent.stoppingDistance);
        }

        private void FaceTowards(Vector3 position)
        {
            Vector3 to = position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), agent.angularSpeed * Time.deltaTime);
        }

        private void SetDestinationThrottled(Vector3 position, bool force = false)
        {
            if (!force && Time.time < nextRepath) return;
            nextRepath = Time.time + config.repathInterval;
            agent.SetDestination(SampleOnNavMesh(position));
        }

        private Vector3 SampleOnNavMesh(Vector3 position)
        {
            return NavMesh.SamplePosition(position, out NavMeshHit hit, config.navSampleDistance, NavMesh.AllAreas) ? hit.position : position;
        }
    }
}
