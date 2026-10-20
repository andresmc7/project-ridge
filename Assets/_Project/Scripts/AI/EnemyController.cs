using System;
using System.Collections.Generic;
using ProjectRidge.CameraRig;
using ProjectRidge.Core;
using ProjectRidge.Items;
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
        private float stunEnd;
        private bool down;
        private Vector3 knockbackVelocity;
        private HandFlare lure;
        private Vector3 lurePoint;

        /// <summary>Enemigos activos (para el HUD de sigilo y las alertas del OVNI).</summary>
        public static readonly List<EnemyController> Active = new List<EnemyController>();

        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public EnemyPerception Perception => perception;
        public EnemyConfig Config => config;
        public event Action<EnemyState> StateChanged;

        /// <summary>
        /// Vuelve a patrullar sin recuerdo del jugador. Si está cerca del punto de reaparición, salta a su ruta
        /// para no acampar ahí (respawn mínimo del playable; M4 lo reemplaza por checkpoints).
        /// </summary>
        public void ResetAfterPlayerRespawn(Vector3 spawn, float clearRadius, float graceSeconds = 0f)
        {
            down = false;
            perception.ResetAwareness();
            perception.Suppress(graceSeconds);
            if (patrolPoints.Length > 0 && Vector3.Distance(transform.position, spawn) < clearRadius)
            {
                // El punto de su ruta más lejano al checkpoint.
                int far = 0;
                for (int i = 1; i < patrolPoints.Length; i++)
                    if ((patrolPoints[i].position - spawn).sqrMagnitude > (patrolPoints[far].position - spawn).sqrMagnitude) far = i;
                agent.Warp(patrolPoints[far].position);
            }
            agent.isStopped = false;
            EnterPatrol();
        }

        /// <summary>Alerta externa (p. ej. el OVNI te detectó): va a investigar esa posición si no está ya cazando.</summary>
        public void AlertTo(Vector3 position)
        {
            if (State != EnemyState.Patrol && State != EnemyState.Investigate && State != EnemyState.Search) return;
            EnterInvestigate(position);
        }

        public bool IsDead => State == EnemyState.Dead;
        /// <summary>Cazando o atacando: un golpe ahora no es por sorpresa.</summary>
        public bool IsHunting => State == EnemyState.Chase || State == EnemyState.Attack;

        /// <summary>
        /// Lo golpearon o aturdieron: interrumpe lo que hacía (incluido su ataque), retrocede un poco
        /// y al recuperarse sabe dónde estás.
        /// </summary>
        public void Interrupt(float seconds, Vector3 attackerPosition, float knockback)
        {
            if (IsDead) return;
            perception.Notice(attackerPosition);
            stunEnd = Mathf.Max(stunEnd, Time.time + seconds);
            Vector3 away = transform.position - attackerPosition;
            away.y = 0f;
            knockbackVelocity = away.sqrMagnitude > 0.001f ? away.normalized * knockback : Vector3.zero;
            SetState(EnemyState.Stunned);
            agent.isStopped = true;
            agent.ResetPath();
        }

        /// <summary>En el suelo tras un derribo por la espalda (cae y convulsiona).</summary>
        public bool IsDown => State == EnemyState.Stunned && down;

        /// <summary>
        /// Derribo por la espalda: cae y queda en el suelo, sin enterarse de quién fue. Al levantarse busca alrededor.
        /// </summary>
        public void Incapacitate(float seconds)
        {
            if (IsDead) return;
            down = true;
            stunEnd = Time.time + seconds;
            knockbackVelocity = Vector3.zero;
            SetState(EnemyState.Stunned);
            agent.isStopped = true;
            agent.ResetPath();
        }

        public void Die()
        {
            if (IsDead) return;
            SetState(EnemyState.Dead);
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
            perception.enabled = false;
            if (TryGetComponent(out Collider body)) body.enabled = false;
            Active.Remove(this);
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            zigzagSeed = UnityEngine.Random.value * 10f;
        }

        private void OnEnable()
        {
            if (!IsDead) Active.Add(this);
            perception.NoiseHeard += OnNoiseHeard;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            perception.NoiseHeard -= OnNoiseHeard;
        }

        private void Start()
        {
            // El agente viene desactivado en la escena: en el player puede activarse antes de que el NavMeshSurface
            // registre su malla ("Failed to create agent"). En Start la malla ya está registrada.
            if (!agent.enabled) agent.enabled = true;
            else if (!agent.isOnNavMesh) { agent.enabled = false; agent.enabled = true; }
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning(name + " fuera del NavMesh en " + transform.position);
                return;
            }
            Debug.Log(name + " en el NavMesh");
            EnterPatrol();
        }

        private void Update()
        {
            stateTime += Time.deltaTime;
            if (State == EnemyState.Dead) return;
            if (!agent.enabled || !agent.isOnNavMesh) return;
            if (State == EnemyState.Stunned)
            {
                TickStunned();
                return;
            }

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
            else if (State == EnemyState.Patrol || State == EnemyState.Investigate || State == EnemyState.Search)
            {
                var flare = HandFlare.NearestBurning(transform.position);
                if (flare != null) EnterLured(flare);
            }

            switch (State)
            {
                case EnemyState.Patrol: TickPatrol(); break;
                case EnemyState.Investigate: TickInvestigate(); break;
                case EnemyState.Search: TickSearch(); break;
                case EnemyState.Chase: TickChase(); break;
                case EnemyState.Attack: TickAttack(); break;
                case EnemyState.Lured: TickLured(); break;
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

            // Patrón legible: en cada parada mira siempre hacia la misma dirección (la del punto de patrulla).
            var point = patrolPoints[patrolIndex];
            FaceTowards(point.position + point.forward * 5f);
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
            if (State != EnemyState.Patrol && State != EnemyState.Investigate && State != EnemyState.Search) return;
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
                    // Se abalanza: recorre attackLunge durante la ventana activa, sin atravesar al papá.
                    Vector3 toTarget = target.transform.position - transform.position;
                    toTarget.y = 0f;
                    if (toTarget.magnitude > config.attackRange * 0.5f && config.attackActive > 0f)
                        agent.Move(transform.forward * (config.attackLunge / config.attackActive) * Time.deltaTime);
                    if (!attackLanded && InAttackArc(target.transform.position))
                    {
                        attackLanded = true; // un golpe por ataque, no daño por frame
                        if (target.TryGetComponent(out IDamageable damageable))
                            damageable.TakeDamage(config.attackDamage);
                        LandHit(target.gameObject, toTarget);
                    }
                    if (stateTime >= config.attackWindup + config.attackActive) attackPhase = AttackPhase.Recovery;
                    break;
                case AttackPhase.Recovery:
                    if (stateTime >= config.attackWindup + config.attackActive + config.attackRecovery)
                        EnterChase();
                    break;
            }
        }

        /// <summary>El zarpazo pesa: empujón, reacción física del cuerpo, golpe de cámara y pausa breve.</summary>
        private void LandHit(GameObject victim, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();
            if (victim.TryGetComponent(out ProjectRidge.Player.PlayerController pc)) pc.AddKnockback(direction * config.attackKnockback);
            var reaction = victim.GetComponentInChildren<HitReaction>();
            if (reaction != null) reaction.Impulse(direction, config.attackHitReaction);
            CameraShake.Kick(direction, config.attackCameraKick);
            HitStop.Trigger(config.attackHitStop, config.attackHitStopScale);
        }

        private bool InAttackArc(Vector3 targetPos)
        {
            Vector3 to = targetPos - transform.position;
            to.y = 0f;
            return to.magnitude <= config.attackRange && Vector3.Angle(transform.forward, to) <= config.attackArc * 0.5f;
        }

        // ---------- Lured (bengala de mano)

        private void EnterLured(HandFlare flare)
        {
            SetState(EnemyState.Lured);
            lure = flare;
            agent.isStopped = false;
            agent.speed = config.lureSpeed;
            Vector3 from = transform.position - flare.transform.position;
            from.y = 0f;
            if (from.sqrMagnitude < 0.01f) from = transform.forward;
            lurePoint = SampleOnNavMesh(flare.transform.position + from.normalized * config.lureStandOff);
            agent.SetDestination(lurePoint);
        }

        private void TickLured()
        {
            if (lure == null || !lure.IsBurning)
            {
                EnterSearch(lurePoint);
                return;
            }
            // Hipnotizado: quieto, mirando la luz (su cono apunta a la bengala, no al camino).
            if (Arrived()) FaceTowards(lure.transform.position);
        }

        // ---------- Stunned (golpe, bengala)

        private void TickStunned()
        {
            if (knockbackVelocity.sqrMagnitude > 0.0001f)
            {
                agent.Move(knockbackVelocity * Time.deltaTime);
                knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 12f * Time.deltaTime);
            }
            if (Time.time < stunEnd) return;
            agent.isStopped = false;
            if (down) { down = false; EnterSearch(transform.position); return; }
            if (perception.CanSeeTarget) EnterChase();
            else EnterInvestigate(perception.LastKnownPosition);
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
