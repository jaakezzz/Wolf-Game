using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
// -----------------------------------------------------------------------------
// A Finite State Machine (FSM) AI that uses Unity's NavMesh system. 
// It patrols a set of points, chases the player when seen, attacks when in range, 
// and remembers the player's position briefly after losing line of sight.
// -----------------------------------------------------------------------------
public class ChaserEnemyAI : MonoBehaviour
{
    // ----------------------------------------
    // AI State Definition
    // ----------------------------------------
    // The four core behaviors the FSM can switch between.
    public enum State { Patrol, Chase, Attack, Return }

    #region Refs / Animator
    // ----------------------------------------
    // Component References
    // ----------------------------------------
    [Header("Refs")]
    public Transform player;                 // Target to hunt (auto-found by tag if empty)
    public Animator animator;                // Controls physical mesh animations
    public Transform attackPoint;            // The physical location where melee damage originates (e.g., the axe head)

    [Header("Animator Param Names")]
    public string speedParam = "speed";      // Used to blend idle/walk/run animations based on NavMesh velocity
    public string attackTrigger = "attack1"; // Fires the attack animation
    public string dieTrigger = "die";        // Fires the death animation
    #endregion

    #region AoI / Vision
    // ----------------------------------------
    // Tethering / Area of Interest (AoI)
    // ----------------------------------------
    [Header("Area of Interest")]
    [Tooltip("The absolute maximum distance the AI will travel from its spawn point before giving up.")]
    public float interestRadius = 25f;       // Prevents the AI from being kited across the entire map
    Vector3 homePos;                         // The anchor point the AI is tethered to

    // ----------------------------------------
    // Sensory System (Vision)
    // ----------------------------------------
    [Header("Vision")]
    [Tooltip("How far the AI can see.")]
    public float sightRadius = 15f;          // Maximum distance of the vision cone
    [Range(0, 360)]
    public float sightFOV = 120f;            // Field of View angle (e.g., human is ~210, standard game AI is 90-120)
    public LayerMask visionBlockers = ~0;    // Layers that block line of sight (like walls/trees)
    public bool requireLineOfSight = true;   // If false, the AI has "x-ray" vision within its radius

    [Header("Vision – tuning")]
    public float eyeHeight = 1.6f;           // Where the raycast originates (prevents seeing through floor bumps)
    public float targetHeight = 1.0f;        // Where the raycast aims (aims at chest, not feet)

    [Tooltip("How many seconds the AI remembers the player's position after they break line of sight.")]
    public float memoryTime = 3.0f;          // Allows the AI to round corners after the player breaks LoS
    public bool debugVision = false;         // Toggles Editor gizmo lines for the vision cone

    float lastSeenTime = -999f;              // Tracks the exact exact Time.time the player was last visible
    #endregion

    #region Combat
    // ----------------------------------------
    // Combat Tuning
    // ----------------------------------------
    [Header("Attack")]
    public float attackRange = 2.2f;         // Distance required to trigger the Attack state
    public float attackCooldown = 1.2f;      // Minimum time between swings
    public float attackWindup = 0.25f;       // Time before damage is actually applied (matches animation impact frame)
    public float damage = 20f;               // Amount of health subtracted from the player
    public float faceSpeed = 540f;           // How fast the AI rotates to track the player during the windup
    #endregion

    #region Patrol / Agent
    // ----------------------------------------
    // Pathfinding & Patrol Route
    // ----------------------------------------
    [Header("Patrol")]
    public Transform[] patrolPoints;         // Array of waypoints defining the patrol route
    public bool loopPatrol = true;           // If true: 1->2->3->1. If false (ping-pong): 1->2->3->2->1.
    public float waypointTolerance = 0.6f;   // How close the AI needs to get to a point before it counts as "arrived"
    public float idlePauseAtWaypoint = 0.5f; // How long the AI stands still at a waypoint before moving on

    [Header("Agent Tuning")]
    public float chaseSpeed = 3.5f;          // Speed when hunting the player
    public float patrolSpeed = 2.25f;        // Speed when casually walking the route

    // ----------------------------------------
    // Internal State Tracking
    // ----------------------------------------
    MinotaurAudio audioFx;                    // Reference to the audio component for playing sounds
    NavMeshAgent agent;                       // Unity's built-in pathfinding component
    Health health;                            // The AI's own health pool
    State state = State.Patrol;               // Initializes the FSM into the Patrol state

    // Patrol bookkeeping
    int patrolIndex;                          // Which waypoint we are currently walking toward
    int patrolDir = 1;                        // +1 for moving forward through the array, -1 for reverse (ping-pong)
    bool waitingAtWaypoint;                   // Prevents the AI from spamming the wait routine
    Coroutine waitRoutine;                    // Reference to the active pause timer

    float lastAttackTime = -999f;             // Tracks cooldowns
    float retargetTimer;                      // Used to throttle NavMesh calculations for performance optimization

    // Death gate
    bool isDead;                              // Locks the FSM entirely if true
    #endregion


    // -----------------------------------------------------------------------------
    // Initialization & Event Subscriptions
    // -----------------------------------------------------------------------------
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        audioFx = GetComponent<MinotaurAudio>();

        // Observer Pattern: Instead of checking health every frame, we "subscribe" to the 
        // death event. When Health reaches 0, it automatically calls our OnDied() function.
        health.onDeath.AddListener(OnDied);

        // Record the spawn location to establish the exact center of the Area of Interest
        homePos = transform.position;

        // Failsafe: Find player by tag if the designer forgot to drag it into the inspector
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // Failsafe: Find animator on child object if not manually assigned
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    void OnDestroy()
    {
        // Memory Management: Always unsubscribe from events when the object is destroyed 
        // to prevent null reference errors and memory leaks.
        if (health) health.onDeath.RemoveListener(OnDied);
    }

    void OnEnable()
    {
        if (isDead) return; // If an object pooler re-enables a dead body, ignore it

        // Reset the FSM to default patrol state
        state = State.Patrol;
        if (agent) { agent.isStopped = false; agent.speed = patrolSpeed; }

        // Reset patrol bookkeeping
        waitingAtWaypoint = false;
        patrolIndex = 0;
        patrolDir = 1;
        GoToCurrentPatrolPointOrHome();
    }

    // -----------------------------------------------------------------------------
    // Death Handling
    // -----------------------------------------------------------------------------
    void OnDied()
    {
        isDead = true; // Closes the "Death Gate", locking out the Update loop

        // Immediately cancel any active attacks or waiting routines so a dead AI doesn't suddenly swing its axe
        StopAllCoroutines();

        // Technically not needed since Update is locked, but good practice for FSM hygiene
        state = State.Return;

        if (agent)
        {
            agent.ResetPath();        // Delete the current path
            agent.isStopped = true;   // Tell the NavMesh engine to halt physical movement
        }

        // Zero out animator speed so the blend tree returns to an idle pose before playing the death animation
        if (animator && !string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, 0f);
    }


    // -----------------------------------------------------------------------------
    // THE AI BRAIN (Executed every frame)
    // -----------------------------------------------------------------------------
    void Update()
    {
        // Death Gate: If dead, do absolutely nothing but ensure animation speed is 0
        if (isDead)
        {
            if (animator && !string.IsNullOrEmpty(speedParam))
                animator.SetFloat(speedParam, 0f);
            return;
        }

        // Animation Sync: Feed the NavMeshAgent's actual physical velocity into the Animator.
        // This ensures the walk/run animations perfectly match the movement speed without foot sliding.
        if (animator && agent)
            animator.SetFloat(speedParam, agent.velocity.magnitude);

        // Fail-safe: If no player exists, default to patrolling
        if (!player) { PatrolUpdate(); return; }

        // ----------------------------------------
        // Environmental & Sensory Polling
        // ----------------------------------------

        // 1. Check Tether (Area of Interest)
        // Optimization: We use sqrMagnitude (distance without the expensive square root operation) 
        // compared against radius squared. This is much faster for the CPU to calculate.
        bool inAoI = (player.position - homePos).sqrMagnitude <= interestRadius * interestRadius;

        // 2. Check Vision
        bool visibleNow = inAoI && IsPlayerVisible();
        if (visibleNow) lastSeenTime = Time.time; // Update memory timestamp

        // 3. Object Permanence (Memory)
        // The AI "sees" the player if they are physically visible right now, OR if the memory timer hasn't expired.
        // This allows the AI to keep chasing for a few seconds if the player runs behind a tree.
        bool canSee = visibleNow || (Time.time < lastSeenTime + memoryTime);

        // 4. Attack Range
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        bool inAttack = canSee && distToPlayer <= attackRange;

        // ----------------------------------------
        // The Finite State Machine (FSM) Router
        // ----------------------------------------
        switch (state)
        {
            case State.Patrol:
                // TRANSITION OUT: Player spotted -> Switch to Chase
                if (canSee) { state = State.Chase; agent.speed = chaseSpeed; waitingAtWaypoint = false; StopWait(); }

                // MAINTAIN STATE: Walk the path
                else PatrolUpdate();
                break;

            case State.Chase:
                // TRANSITION OUT: Player left the tether boundary (Leashing) -> Return Home
                if (!inAoI) { state = State.Return; SetDestination(homePos, patrolSpeed); break; }

                // TRANSITION OUT: Player completely lost (memory expired) -> Resume Patrol
                if (!canSee) { state = State.Patrol; agent.speed = patrolSpeed; GoToNearestPatrolOrHome(); break; }

                // TRANSITION OUT: Close enough to hit the player -> Switch to Attack
                if (inAttack) { state = State.Attack; StartCoroutine(AttackRoutine()); break; }

                // MAINTAIN STATE: Chase the player
                // Pathfinding Optimization: Calculating a NavMesh path is CPU-intensive.
                // Instead of recalculating the path 60 times a second, we use a timer to only update 
                // the destination 10 times a second (0.1f). The player won't notice the difference, 
                // but the CPU is saved a massive amount of work.
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
                // MAINTAIN STATE: Behavior is entirely handled by the AttackRoutine coroutine below.
                // The coroutine will manually switch the state back to Chase when it finishes.
                break;

            case State.Return:
                // TRANSITION OUT: Player re-enters boundary and is spotted while returning home -> Resume Chase
                if (inAoI && canSee) { state = State.Chase; agent.speed = chaseSpeed; break; }

                // TRANSITION OUT: Arrived back at home/patrol route -> Resume Patrol
                // Check if we have a path, and if the remaining distance is within our acceptable tolerance
                if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
                {
                    state = State.Patrol;
                    agent.speed = patrolSpeed;
                    GoToNearestPatrolOrHome();
                }
                break;
        }
    }

    // -----------------------------------------------------------------------------
    // Sensory Math: Field of View & Line of Sight
    // -----------------------------------------------------------------------------
    bool IsPlayerVisible()
    {
        if (!player) return false;

        // Define starting point (enemy eyes) and end point (player chest)
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * targetHeight;

        // 1. Distance Check (Is the player too far away?)
        float dist = (target - eye).magnitude;
        if (dist > sightRadius) return false;

        // 2. Field of View (FOV) Cone Check (Is the player behind the AI?)
        // Flatten the Y axis so the AI doesn't lose sight if the player jumps or goes down a hill
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 toHoriz = (target - eye); toHoriz.y = 0f; toHoriz.Normalize();

        if (fwd.sqrMagnitude > 1e-6f) // Prevent math errors if the forward vector is zero (which can happen if the AI is exactly upside down for some reason)
        {
            // Calculate the angle between where the AI is looking and where the player is.
            // If the angle is greater than half our total FOV, the player is outside the cone.
            float ang = Vector3.Angle(fwd, toHoriz);
            if (ang > sightFOV * 0.5f) return false;
        }

        // If disabled walls blocking vision, skip the raycast entirely
        if (!requireLineOfSight) return true;

        // 3. Linecast Check (Are there walls in the way?)
        // Bitwise manipulation: Get the AI's own layer, invert it (~), and use AND (&) to remove 
        // the AI's layer from the visionBlockers mask. This stops the AI from "seeing" its own body.
        int selfMask = 1 << gameObject.layer; // Get a bitmask for the AI's own layer (e.g., if layer is 8, this becomes 000100000)
        int blockers = visionBlockers & ~selfMask; // Invert selfMask (111011111) and AND with blockers to remove the AI's layer from the blockers

        // Cast an invisible line. If it hits something on the blockers mask...
        if (Physics.Linecast(eye, target, out var hit, blockers, QueryTriggerInteraction.Ignore))
        {
            // It's a valid hit ONLY if the line touched the player (or a child object like an armor piece)
            bool ok = (hit.transform == player) || hit.transform.IsChildOf(player);

            if (debugVision) Debug.DrawLine(eye, hit.point, ok ? Color.green : Color.red, 0.05f);
            return ok;
        }

        // If the line hit absolutely nothing, the view is clear
        if (debugVision) Debug.DrawLine(eye, target, Color.green, 0.05f);
        return true;
    }

    // -----------------------------------------------------------------------------
    // Combat Action Routine
    // -----------------------------------------------------------------------------
    /// <summary>
    /// Handles the timing, animation, and damage application for an attack.
    /// Runs as a Coroutine so it can pause execution and wait for animations to play out.
    /// </summary>
    // -----------------------------------------------------------------------------
    IEnumerator AttackRoutine()
    {
        // Death Gate Check
        if (isDead) { state = State.Patrol; yield break; }

        // Abort attack if it's still on cooldown (return to chase state)
        if (Time.time < lastAttackTime + attackCooldown)
        {
            state = State.Chase;
            yield break;
        }

        // Lock in the attack
        lastAttackTime = Time.time;

        // Stop NavMesh movement so the AI plants its feet while swinging
        if (agent) agent.isStopped = true;

        // Fire the animation and sound effect
        if (animator && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);
        audioFx?.PlayAttack();

        // ----------------------------------------
        // The Windup Phase
        // ----------------------------------------
        // Wait for the exact moment the weapon physically hits the target in the animation
        float t = 0f;
        while (t < attackWindup)
        {
            if (isDead) { yield break; } // Death gate

            t += Time.deltaTime;

            // Re-evaluate AoI and Vision every single frame of the windup
            bool inAoI = (player.position - homePos).sqrMagnitude <= interestRadius * interestRadius;
            bool canSeeWithMemory = IsPlayerVisible() || (Time.time < lastSeenTime + memoryTime);

            // If the player fully escapes memory or the boundary *during* the backswing, cancel the attack!
            if (!inAoI || !canSeeWithMemory)
            {
                if (agent) agent.isStopped = false;
                state = State.Chase;
                yield break;
            }

            // Keep rotating to track the player so they can't just step slightly to the left to dodge
            FaceTowards(player.position);

            // Wait for the next frame
            yield return null;
        }

        if (isDead) yield break; // Final death gate before applying damage

        // ----------------------------------------
        // Hit Detection (The Strike)
        // ----------------------------------------
        // Uses a physics sphere to grab anything near the axe/weapon at the exact frame of impact.
        // We use an OverlapSphere instead of an FOV check here because if the player is physically 
        // touching the axe, they should get hit, regardless of where the AI's "eyes" are looking.
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            // Determine origin of the damage sphere (fallback to math if physical point isn't assigned)
            Vector3 hitPos = attackPoint ? attackPoint.position
                                         : (transform.position + transform.forward * (attackRange * 0.7f) + Vector3.up * 0.8f);
            const float hitRadius = 1.2f;

            // Gather all colliders in the damage zone
            var hits = Physics.OverlapSphere(hitPos, hitRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                // Look up the hierarchy to find the root Health component
                var hp = h.GetComponentInParent<Health>();

                // Apply damage if the hit object actually belongs to the player
                if (hp && (h.transform == player || h.transform.IsChildOf(player)))
                {
                    hp.Damage(damage);
                    break; // Only hit the player once, even if we hit two of their colliders
                }
            }
        }

        // Brief pause after swinging for visual weight/impact
        yield return new WaitForSeconds(0.05f);

        // Resume movement
        if (agent) agent.isStopped = false;

        // FSM Loop Closure: Always return to Chase. 
        // The main Update() loop will evaluate the memoryTime and safely drop the AI 
        // into Patrol next frame if the player actually escaped.
        state = State.Chase;
    }

    // --------------------------------------------------------------------------------
    // Smoothly rotates the enemy to face a specific world coordinate.
    // Used during the attack windup to track a dodging player.
    // --------------------------------------------------------------------------------
    void FaceTowards(Vector3 worldPos)
    {
        // Find the direction, ignoring height differences
        Vector3 to = worldPos - transform.position; to.y = 0f;

        // Prevent math errors if the positions are identical
        if (to.sqrMagnitude < 1e-6f) return;

        // Calculate the ideal rotation, and smoothly step towards it
        Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, faceSpeed * Time.deltaTime);
    }

    // -----------------------------------------------------------------------------
    // Patrol Logic
    // -----------------------------------------------------------------------------
    void PatrolUpdate()
    {
        agent.speed = patrolSpeed;
        agent.isStopped = false;

        // Check if patrol points actually exist in the array
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            // If the agent has arrived at the current waypoint (NavMesh math)
            if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
            {
                // If we aren't already waiting, start the idle timer
                if (!waitingAtWaypoint)
                {
                    waitingAtWaypoint = true;
                    waitRoutine = StartCoroutine(WaitThenAdvance(idlePauseAtWaypoint));
                }
            }
        }
        else
        {
            // Fallback: If no waypoints exist, just walk back to the exact spawn position and stand there
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

        // Allow the timer to be triggered again at the next point
        waitingAtWaypoint = false;
    }

    // --------------------------------------------------------------------------------
    // Calculates the next index in the patrol array depending on the loop mode.
    // --------------------------------------------------------------------------------
    void AdvancePatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (loopPatrol)
        {
            // Modulo Math for Looping: (2 + 1) % 3 = 0.
            // Result: 0, 1, 2, 0, 1, 2...
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            // Ping-Pong Math: Reverses direction when hitting the ends of the array.
            // Result: 0, 1, 2, 1, 0, 1...
            if (patrolPoints.Length == 1) return;
            patrolIndex += patrolDir; // Add the direction modifier (+1 or -1)

            // Bounce off the end
            if (patrolIndex >= patrolPoints.Length - 1) { patrolIndex = patrolPoints.Length - 1; patrolDir = -1; }
            // Bounce off the beginning
            else if (patrolIndex <= 0) { patrolIndex = 0; patrolDir = 1; }
        }
    }

    void GoToCurrentPatrolPointOrHome()
    {
        // Mathf.Clamp ensures we never try to access an index outside the array bounds, preventing crashes
        if (patrolPoints != null && patrolPoints.Length > 0)
            SetDestination(patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)].position, patrolSpeed);
        else
            SetDestination(homePos, patrolSpeed);
    }

    // --------------------------------------------------------------------------------
    // Finds the closest patrol point in the array and sets it as the active destination.
    // Essential for cleanly resuming a patrol after breaking away to chase the player.
    // --------------------------------------------------------------------------------
    void GoToNearestPatrolOrHome()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            SetDestination(homePos, patrolSpeed);
            return;
        }

        // Initialize variables to track the best option
        int best = 0;
        float bestDist = float.MaxValue; // Start impossibly high

        // Loop through all points to find the shortest distance
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            float d = (transform.position - patrolPoints[i].position).sqrMagnitude;
            if (d < bestDist)
            {
                best = i;
                bestDist = d;
            }
        }

        // Update the active index and tell the NavMesh to go there
        patrolIndex = best;
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
        // Cleanly interrupts the idle waiting Coroutine if the AI is suddenly provoked
        if (waitRoutine != null) StopCoroutine(waitRoutine);
        waitRoutine = null;
        waitingAtWaypoint = false;
    }

    // -----------------------------------------------------------------------------
    // Editor Gizmos (Visual Debugging)
    // -----------------------------------------------------------------------------
    // Draws colored lines and spheres in the Unity Editor to help visualize ranges
    void OnDrawGizmosSelected()
    {
        // Draw the Area of Interest (Tether boundary)
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
        Gizmos.DrawWireSphere(Application.isPlaying ? homePos : transform.position, interestRadius);

        // Draw the maximum vision distance
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, sightRadius);

        // Draw the Field of View (FOV) Cone
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.25f);
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();

        // Calculate the left and right angles of the cone using Quaternions
        Quaternion L = Quaternion.AngleAxis(-sightFOV * 0.5f, Vector3.up);
        Quaternion R = Quaternion.AngleAxis(+sightFOV * 0.5f, Vector3.up);

        Gizmos.DrawLine(eye, eye + (L * fwd) * sightRadius);
        Gizmos.DrawLine(eye, eye + (R * fwd) * sightRadius);

        // Draw the exact attack hit sphere location
        Gizmos.color = Color.red;
        Vector3 p = attackPoint ? attackPoint.position : (transform.position + transform.forward * (attackRange * 0.7f));
        Gizmos.DrawWireSphere(p, 0.25f); // Uses 0.25 for the visual, even though logic uses 1.2
    }
}