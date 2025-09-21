using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class ChaserEnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, Return }

    #region Refs / Animator
    [Header("Refs")]
    public Transform player;                 // auto-found by tag if empty
    public Animator animator;                // optional
    public Transform attackPoint;            // optional melee origin

    [Header("Animator Param Names")]
    public string speedParam = "speed";      // float
    public string attackTrigger = "attack1"; // trigger
    public string dieTrigger = "die";        // trigger (not used here)
    #endregion

    #region AoI / Vision
    [Header("Area of Interest")]
    public float interestRadius = 25f;
    Vector3 homePos;

    [Header("Vision")]
    public float sightRadius = 15f;
    [Range(0, 360)] public float sightFOV = 120f;
    public LayerMask visionBlockers = ~0;
    public bool requireLineOfSight = true;

    [Header("Vision – tuning")]
    public float eyeHeight = 1.6f;
    public float targetHeight = 1.0f;
    public float memoryTime = 1.0f;
    public bool debugVision = false;

    float lastSeenTime = -999f;
    #endregion

    #region Combat
    [Header("Attack")]
    public float attackRange = 2.2f;
    public float attackCooldown = 1.2f;
    public float attackWindup = 0.25f;
    public float damage = 20f;
    public float faceSpeed = 540f;
    #endregion

    #region Patrol / Agent
    [Header("Patrol")]
    public Transform[] patrolPoints;
    public bool loopPatrol = true;           // if false, ping-pong
    public float waypointTolerance = 0.6f;
    public float idlePauseAtWaypoint = 0.5f;

    [Header("Agent Tuning")]
    public float chaseSpeed = 3.5f;
    public float patrolSpeed = 2.25f;

    NavMeshAgent agent;
    Health health;
    State state = State.Patrol;

    // patrol bookkeeping
    int patrolIndex;
    int patrolDir = 1;                        // for ping-pong
    bool waitingAtWaypoint;
    Coroutine waitRoutine;

    float lastAttackTime = -999f;
    float retargetTimer;                      // reduce SetDestination spam

    // death gate
    bool isDead;
    #endregion

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        health.onDeath.AddListener(OnDied);

        homePos = transform.position;

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    void OnDestroy()
    {
        if (health) health.onDeath.RemoveListener(OnDied);
    }

    void OnEnable()
    {
        if (isDead) return; // if re-enabled after death, do nothing
        state = State.Patrol;
        if (agent) { agent.isStopped = false; agent.speed = patrolSpeed; }
        waitingAtWaypoint = false;
        patrolIndex = 0;
        patrolDir = 1;
        GoToCurrentPatrolPointOrHome();
    }

    void OnDied()
    {
        isDead = true;

        StopAllCoroutines();
        state = State.Return;

        if (agent)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        // Zero out animator speed so it doesn't try to blend into locomotion
        if (animator && !string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, 0f);

        // You can also disable this component here if you prefer:
        // enabled = false;
    }

    void Update()
    {
        if (isDead)
        {
            // Keep animator speed at 0
            if (animator && !string.IsNullOrEmpty(speedParam))
                animator.SetFloat(speedParam, 0f);
            return;
        }

        // Feed animator speed (safe if animator is null)
        if (animator && agent)
            animator.SetFloat(speedParam, agent.velocity.magnitude);

        if (!player) { PatrolUpdate(); return; }

        bool inAoI = (player.position - homePos).sqrMagnitude <= interestRadius * interestRadius;

        // Vision with memory
        bool visibleNow = inAoI && IsPlayerVisible();
        if (visibleNow) lastSeenTime = Time.time;
        bool canSee = visibleNow || (Time.time < lastSeenTime + memoryTime);

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        bool inAttack = canSee && distToPlayer <= attackRange;

        switch (state)
        {
            case State.Patrol:
                if (canSee) { state = State.Chase; agent.speed = chaseSpeed; waitingAtWaypoint = false; StopWait(); }
                else PatrolUpdate();
                break;

            case State.Chase:
                if (!inAoI) { state = State.Return; SetDestination(homePos, patrolSpeed); break; }
                if (!canSee) { state = State.Patrol; agent.speed = patrolSpeed; GoToNearestPatrolOrHome(); break; }
                if (inAttack) { state = State.Attack; StartCoroutine(AttackRoutine()); break; }

                // Smooth re-targeting
                retargetTimer -= Time.deltaTime;
                if (retargetTimer <= 0f)
                {
                    retargetTimer = 0.1f;
                    agent.isStopped = false;
                    agent.speed = chaseSpeed;
                    agent.SetDestination(player.position);
                }
                break;

            case State.Attack:
                // coroutine drives it
                break;

            case State.Return:
                if (inAoI && canSee) { state = State.Chase; agent.speed = chaseSpeed; break; }
                if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
                {
                    state = State.Patrol;
                    agent.speed = patrolSpeed;
                    GoToNearestPatrolOrHome();
                }
                break;
        }
    }

    // ----------------- Vision -----------------
    bool IsPlayerVisible()
    {
        if (!player) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * targetHeight;

        float dist = (target - eye).magnitude;
        if (dist > sightRadius) return false;

        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 toHoriz = (target - eye); toHoriz.y = 0f; toHoriz.Normalize();
        if (fwd.sqrMagnitude > 1e-6f)
        {
            float ang = Vector3.Angle(fwd, toHoriz);
            if (ang > sightFOV * 0.5f) return false;
        }

        if (!requireLineOfSight) return true;

        int selfMask = 1 << gameObject.layer;
        int blockers = visionBlockers & ~selfMask;

        if (Physics.Linecast(eye, target, out var hit, blockers, QueryTriggerInteraction.Ignore))
        {
            bool ok = (hit.transform == player) || hit.transform.IsChildOf(player);
            if (debugVision) Debug.DrawLine(eye, hit.point, ok ? Color.green : Color.red, 0.05f);
            return ok;
        }

        if (debugVision) Debug.DrawLine(eye, target, Color.green, 0.05f);
        return true;
    }

    // ----------------- Attack -----------------
    IEnumerator AttackRoutine()
    {
        if (isDead) { state = State.Patrol; yield break; }

        if (Time.time < lastAttackTime + attackCooldown)
        {
            state = State.Chase;
            yield break;
        }

        lastAttackTime = Time.time;
        if (agent) agent.isStopped = true;

        if (animator && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        float t = 0f;
        while (t < attackWindup)
        {
            if (isDead) { yield break; }            // <- death gate
            t += Time.deltaTime;

            bool inAoI = (player.position - homePos).sqrMagnitude <= interestRadius * interestRadius;
            if (!inAoI || !IsPlayerVisible())
            {
                if (agent) agent.isStopped = false;
                state = State.Chase;
                yield break;
            }

            FaceTowards(player.position);
            yield return null;
        }

        if (isDead) yield break;                     // <- death gate

        // Damage if still in range and visible
        if (Vector3.Distance(transform.position, player.position) <= attackRange && IsPlayerVisible())
        {
            Vector3 hitPos = attackPoint ? attackPoint.position
                                         : (transform.position + transform.forward * (attackRange * 0.7f) + Vector3.up * 0.8f);
            const float hitRadius = 1.2f;
            var hits = Physics.OverlapSphere(hitPos, hitRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var hp = h.GetComponentInParent<Health>();
                if (hp && (h.transform == player || h.transform.IsChildOf(player)))
                {
                    hp.Damage(damage);
                    break;
                }
            }
        }

        yield return new WaitForSeconds(0.05f);

        if (agent) agent.isStopped = false;
        state = IsPlayerVisible() ? State.Chase : State.Patrol;
        if (state == State.Patrol) { agent.speed = patrolSpeed; GoToNearestPatrolOrHome(); }
    }

    void FaceTowards(Vector3 worldPos)
    {
        Vector3 to = worldPos - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 1e-6f) return;
        Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, faceSpeed * Time.deltaTime);
    }

    // ----------------- Patrol -----------------
    void PatrolUpdate()
    {
        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
            {
                if (!waitingAtWaypoint)
                {
                    waitingAtWaypoint = true;
                    waitRoutine = StartCoroutine(WaitThenAdvance(idlePauseAtWaypoint));
                }
            }
        }
        else
        {
            if (!agent.hasPath) agent.SetDestination(homePos);
        }
    }

    IEnumerator WaitThenAdvance(float delay)
    {
        agent.isStopped = true;
        yield return new WaitForSeconds(delay);
        agent.isStopped = false;
        AdvancePatrolIndex();
        GoToCurrentPatrolPointOrHome();
        waitingAtWaypoint = false;
    }

    void AdvancePatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (loopPatrol)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            if (patrolPoints.Length == 1) return;
            patrolIndex += patrolDir;
            if (patrolIndex >= patrolPoints.Length - 1) { patrolIndex = patrolPoints.Length - 1; patrolDir = -1; }
            else if (patrolIndex <= 0) { patrolIndex = 0; patrolDir = 1; }
        }
    }

    void GoToCurrentPatrolPointOrHome()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
            SetDestination(patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)].position, patrolSpeed);
        else
            SetDestination(homePos, patrolSpeed);
    }

    void GoToNearestPatrolOrHome()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            SetDestination(homePos, patrolSpeed);
            return;
        }

        int best = 0; float bestDist = float.MaxValue;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            float d = (transform.position - patrolPoints[i].position).sqrMagnitude;
            if (d < bestDist) { best = i; bestDist = d; }
        }

        patrolIndex = best; // go to the nearest, then continue
        SetDestination(patrolPoints[best].position, patrolSpeed);
    }

    void SetDestination(Vector3 pos, float speed)
    {
        if (!agent) return;
        agent.speed = speed;
        agent.isStopped = false;
        agent.SetDestination(pos);
    }

    void StopWait()
    {
        if (waitRoutine != null) StopCoroutine(waitRoutine);
        waitRoutine = null;
        waitingAtWaypoint = false;
    }

    // ----------------- Gizmos -----------------
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
        Gizmos.DrawWireSphere(Application.isPlaying ? homePos : transform.position, interestRadius);

        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, sightRadius);

        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.25f);
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Quaternion L = Quaternion.AngleAxis(-sightFOV * 0.5f, Vector3.up);
        Quaternion R = Quaternion.AngleAxis(+sightFOV * 0.5f, Vector3.up);
        Gizmos.DrawLine(eye, eye + (L * fwd) * sightRadius);
        Gizmos.DrawLine(eye, eye + (R * fwd) * sightRadius);

        Gizmos.color = Color.red;
        Vector3 p = attackPoint ? attackPoint.position : (transform.position + transform.forward * (attackRange * 0.7f));
        Gizmos.DrawWireSphere(p, 0.25f);
    }
}
