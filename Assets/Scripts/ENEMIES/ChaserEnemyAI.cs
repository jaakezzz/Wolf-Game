using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
/// <summary>
/// A finite state machine (FSM) AI that uses Unity's NavMesh system. 
/// It patrols a set of points, chases the player when seen, attacks when in range, 
/// and remembers the player's position briefly after losing line of sight.
/// </summary>
public class ChaserEnemyAI : MonoBehaviour
{
    MinotaurAudio audioFx;

    // The four core behaviors the AI can switch between
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
    [Tooltip("The absolute maximum distance the AI will travel from its spawn point before giving up.")]
    public float interestRadius = 25f;
    Vector3 homePos;

    [Header("Vision")]
    [Tooltip("How far the AI can see.")]
    public float sightRadius = 15f;
    [Range(0, 360)] public float sightFOV = 120f;
    public LayerMask visionBlockers = ~0; // Layers that block line of sight (like walls)
    public bool requireLineOfSight = true;

    [Header("Vision – tuning")]
    public float eyeHeight = 1.6f;
    public float targetHeight = 1.0f;
    [Tooltip("How many seconds the AI remembers the player's position after they break line of sight.")]
    public float memoryTime = 3.0f;
    public bool debugVision = false;

    float lastSeenTime = -999f; // Initialized to a very negative number so it doesn't trigger immediately
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
    public bool loopPatrol = true;           // if false, ping-pong (go back and forth)
    public float waypointTolerance = 0.6f;
    public float idlePauseAtWaypoint = 0.5f;

    [Header("Agent Tuning")]
    public float chaseSpeed = 3.5f;
    public float patrolSpeed = 2.25f;

    // Component references
    NavMeshAgent agent;
    Health health;
    State state = State.Patrol;

    // patrol bookkeeping
    int patrolIndex;
    int patrolDir = 1;                        // for ping-pong
    bool waitingAtWaypoint;
    Coroutine waitRoutine;

    float lastAttackTime = -999f;
    float retargetTimer;                      // reduce SetDestination spam for better performance

    // death gate
    bool isDead;
    #endregion

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        // Listen for the health component's death event
        health.onDeath.AddListener(OnDied);

        // Record the spawn location to establish the Area of Interest center
        homePos = transform.position;

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!animator) animator = GetComponentInChildren<Animator>();

        audioFx = GetComponent<MinotaurAudio>();
    }

    void OnDestroy()
    {
        // Clean up event listeners to prevent memory leaks when this object is destroyed
        if (health) health.onDeath.RemoveListener(OnDied);
    }

    void OnEnable()
    {
        if (isDead) return; // if re-enabled after death, do nothing

        // Reset to default patrol state when enabled
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

        // Immediately cancel any active attacks or waiting routines
        StopAllCoroutines();
        state = State.Return;

        if (agent)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        // Zero out animator speed so it doesn't try to play walking animations while dying
        if (animator && !string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, 0f);
    }

    void Update()
    {
        if (isDead)
        {
            // Keep animator speed at 0 while dead
            if (animator && !string.IsNullOrEmpty(speedParam))
                animator.SetFloat(speedParam, 0f);
            return;
        }

        // Feed current movement speed to the animator to blend between idle/walk/run
        if (animator && agent)
            animator.SetFloat(speedParam, agent.velocity.magnitude);

        // If no player exists, default to patrolling
        if (!player) { PatrolUpdate(); return; }

        // Check if the player is inside the designated boundary
        bool inAoI = (player.position - homePos).sqrMagnitude <= interestRadius * interestRadius;

        // Check vision and update memory
        bool visibleNow = inAoI && IsPlayerVisible();
        if (visibleNow) lastSeenTime = Time.time;

        // The AI "sees" the player if they are currently visible, OR if the memory timer hasn't expired yet
        bool canSee = visibleNow || (Time.time < lastSeenTime + memoryTime);

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        bool inAttack = canSee && distToPlayer <= attackRange;

        // --- THE STATE MACHINE ---
        switch (state)
        {
            case State.Patrol:
                // Transition: Player spotted
                if (canSee) { state = State.Chase; agent.speed = chaseSpeed; waitingAtWaypoint = false; StopWait(); }
                // Otherwise, keep walking the path
                else PatrolUpdate();
                break;

            case State.Chase:
                // Transition: Player left the boundary
                if (!inAoI) { state = State.Return; SetDestination(homePos, patrolSpeed); break; }
                // Transition: Player completely lost (memory expired)
                if (!canSee) { state = State.Patrol; agent.speed = patrolSpeed; GoToNearestPatrolOrHome(); break; }
                // Transition: Close enough to hit the player
                if (inAttack) { state = State.Attack; StartCoroutine(AttackRoutine()); break; }

                // Smooth re-targeting (updates pathfinding every 0.1s instead of every single frame)
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
                // Behavior is entirely handled by the AttackRoutine coroutine below
                break;

            case State.Return:
                // Transition: Player re-enters boundary and is spotted while returning home
                if (inAoI && canSee) { state = State.Chase; agent.speed = chaseSpeed; break; }

                // Transition: Arrived back at home/patrol route
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
    /// <summary>
    /// Calculates if the player is physically visible using FOV math and Raycasting.
    /// </summary>
    bool IsPlayerVisible()
    {
        if (!player) return false;

        // Define starting point (enemy eyes) and end point (player chest)
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * targetHeight;

        float dist = (target - eye).magnitude;
        if (dist > sightRadius) return false;

        // Check if the player is inside the FOV cone
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 toHoriz = (target - eye); toHoriz.y = 0f; toHoriz.Normalize();
        if (fwd.sqrMagnitude > 1e-6f)
        {
            float ang = Vector3.Angle(fwd, toHoriz);
            if (ang > sightFOV * 0.5f) return false;
        }

        if (!requireLineOfSight) return true;

        // Ignore the enemy's own body colliders during the line of sight check
        int selfMask = 1 << gameObject.layer;
        int blockers = visionBlockers & ~selfMask;

        // Cast a line from the eye to the player to see if a wall is in the way
        if (Physics.Linecast(eye, target, out var hit, blockers, QueryTriggerInteraction.Ignore))
        {
            // Valid hit if the ray touched the player or a child object of the player
            bool ok = (hit.transform == player) || hit.transform.IsChildOf(player);
            if (debugVision) Debug.DrawLine(eye, hit.point, ok ? Color.green : Color.red, 0.05f);
            return ok;
        }

        if (debugVision) Debug.DrawLine(eye, target, Color.green, 0.05f);
        return true;
    }

    // ----------------- Attack -----------------
    /// <summary>
    /// Handles the timing, animation, and damage application for an attack.
    /// </summary>
    IEnumerator AttackRoutine()
    {
        if (isDead) { state = State.Patrol; yield break; }

        // Abort attack if it's still on cooldown
        if (Time.time < lastAttackTime + attackCooldown)
        {
            state = State.Chase;
            yield break;
        }

        lastAttackTime = Time.time;

        // Stop moving while swinging
        if (agent) agent.isStopped = true;

        if (animator && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);
        audioFx?.PlayAttack();

        // Windup phase (wait for the animation to look like it connects)
        float t = 0f;
        while (t < attackWindup)
        {
            if (isDead) { yield break; }            // <- death gate
            t += Time.deltaTime;

            bool inAoI = (player.position - homePos).sqrMagnitude <= interestRadius * interestRadius;

            // Check if the player is in our memory and also visible right this frame
            bool canSeeWithMemory = IsPlayerVisible() || (Time.time < lastSeenTime + memoryTime);

            // Cancel the attack windup if the player fully escapes memory or the boundary
            if (!inAoI || !canSeeWithMemory)
            {
                if (agent) agent.isStopped = false;
                state = State.Chase;
                yield break;
            }

            // Keep rotating to track the player during the windup
            FaceTowards(player.position);
            yield return null;
        }

        if (isDead) yield break;                     // <- death gate

        // Hit Detection: Uses a physics sphere to grab anything near the axe/weapon
        // If the player is inside the physical hitbox but just dodged out of the 120-degree FOV, 
        // the attack should still hit them!
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            // Determine origin of the damage sphere
            Vector3 hitPos = attackPoint ? attackPoint.position
                                         : (transform.position + transform.forward * (attackRange * 0.7f) + Vector3.up * 0.8f);
            const float hitRadius = 1.2f;

            // Gather all colliders in the damage zone
            var hits = Physics.OverlapSphere(hitPos, hitRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var hp = h.GetComponentInParent<Health>();
                // Apply damage if the hit object belongs to the player
                if (hp && (h.transform == player || h.transform.IsChildOf(player)))
                {
                    hp.Damage(damage);
                    break;
                }
            }
        }

        // Brief pause after swinging
        yield return new WaitForSeconds(0.05f);

        if (agent) agent.isStopped = false;

        // Always return to Chase. The main Update() loop will evaluate the memoryTime 
        // and safely drop the AI into Patrol if the memory timer has actually expired.
        state = State.Chase;
    }

    /// <summary>
    /// Smoothly rotates the enemy to face a specific world coordinate.
    /// </summary>
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

        // Check if patrol points are assigned
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            // If the agent has arrived at the current waypoint
            if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
            {
                if (!waitingAtWaypoint)
                {
                    // Start the idle pause timer
                    waitingAtWaypoint = true;
                    waitRoutine = StartCoroutine(WaitThenAdvance(idlePauseAtWaypoint));
                }
            }
        }
        else
        {
            // If no waypoints exist, just walk back to the exact spawn position
            if (!agent.hasPath) agent.SetDestination(homePos);
        }
    }

    IEnumerator WaitThenAdvance(float delay)
    {
        // Stop moving, wait, then pick the next waypoint
        agent.isStopped = true;
        yield return new WaitForSeconds(delay);
        agent.isStopped = false;
        AdvancePatrolIndex();
        GoToCurrentPatrolPointOrHome();
        waitingAtWaypoint = false;
    }

    /// <summary>
    /// Calculates the next index in the patrol array depending on the loop mode.
    /// </summary>
    void AdvancePatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (loopPatrol)
        {
            // 0, 1, 2, 0, 1, 2...
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            // Ping-pong: 0, 1, 2, 1, 0, 1...
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

    /// <summary>
    /// Finds the closest patrol point in the array and sets it as the active destination.
    /// Useful for resuming a patrol after chasing the player.
    /// </summary>
    void GoToNearestPatrolOrHome()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            SetDestination(homePos, patrolSpeed);
            return;
        }

        int best = 0; float bestDist = float.MaxValue;

        // Loop through all points to find the shortest distance
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            float d = (transform.position - patrolPoints[i].position).sqrMagnitude;
            if (d < bestDist) { best = i; bestDist = d; }
        }

        patrolIndex = best; // go to the nearest, then continue the sequence from there
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
    // Draws colored lines and spheres in the Unity Editor to help visualize ranges
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