using UnityEngine;
using System.Collections;

// -----------------------------------------------------------------------------
// Controls a stationary "turret" style enemy that remains hidden until the player 
// enters its line of sight and range. Once visible, it fires projectiles at a set interval.
// -----------------------------------------------------------------------------
public class BooEnemyAI : MonoBehaviour
{
    // ----------------------------------------
    // References & Visuals
    // ----------------------------------------
    [Header("References")]
    [Tooltip("The target the enemy will look for and shoot at.")]
    public Transform player;                 // The player to track and shoot

    [Tooltip("Renderers to disable when hiding, used as a fallback if no dissolve script is found.")]
    public Renderer[] renderersToHide;       // Array of 3D meshes to turn invisible

    [Tooltip("Script handling the smooth fade in/out visual effect.")]
    public GhostDissolve visuals;            // Custom shader/script for a smooth ghostly fade

    // ----------------------------------------
    // Perception Settings
    // ----------------------------------------
    [Header("Perception")]
    [Range(10f, 100f)]
    public float visibilityRange = 25f;      // How far away the Boo can detect the player

    [Range(10f, 179f)]
    public float visibilityConeDegrees = 120f; // The total width of the vision cone (120 degrees total)

    [Tooltip("Maximum distance at which the enemy will actually fire a projectile.")]
    public float shootRange = 18f;           // Usually shorter than visibilityRange to give the player a warning

    [Tooltip("Time in seconds between each fired projectile.")]
    public float fireInterval = 1.5f;        // Cooldown between shots

    // ----------------------------------------
    // Attack Settings
    // ----------------------------------------
    [Header("Attack")]
    [Tooltip("The object to spawn when attacking.")]
    public GameObject projectilePrefab;      // The actual bullet/fireball to instantiate

    [Tooltip("The exact position the projectile spawns from (e.g., the tip of a wand or mouth).")]
    public Transform firePoint;              // Empty GameObject placed exactly where bullets should exit

    // ----------------------------------------
    // State Tracking Variables
    // ----------------------------------------
    bool visibleToPlayer = true;             // Is the Boo currently materialized?
    bool lastVisible = true;                 // Debounce tracker: remembers what the state was LAST frame
    Coroutine fireLoop;                      // Reference to the active shooting routine


    // -----------------------------------------------------------------------------
    // Initialization & Safety Failsafes
    // -----------------------------------------------------------------------------
    void Start()
    {
        // Auto-find the custom fade script if the designer forgot to drag it in
        if (!visuals) visuals = GetComponentInChildren<GhostDissolve>();

        // Auto-find the player by looking for the "Player" tag
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // Fallback: If no specific renderers were assigned, find EVERY renderer attached to 
        // this object and its children. (true) means it will even find disabled ones.
        if (renderersToHide == null || renderersToHide.Length == 0)
            renderersToHide = GetComponentsInChildren<Renderer>(true);

        // Kick off the infinite loop that handles the shooting cadence
        fireLoop = StartCoroutine(FireRoutine());
    }


    // -----------------------------------------------------------------------------
    // The Perception Brain (Runs every frame)
    // -----------------------------------------------------------------------------
    void Update()
    {
        // Safety lock: Stop calculating if the player doesn't exist or was destroyed
        if (!player) return;

        // 1. Calculate Vector to Player
        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude; // Get the raw distance before we flatten it

        // 2. Flatten the Y-Axis
        // This ensures the vision cone acts like a flat pie slice on the ground. 
        // Without this, if the player jumps or goes downhill, the angle changes drastically, 
        // and the Boo might suddenly "lose sight" of them mid-air.
        toPlayer.y = 0f;

        // Epsilon Check: Avoid division by zero errors if the player is standing exactly 
        // inside the enemy's exact center coordinates.
        if (toPlayer.sqrMagnitude < 0.0001f) return;
        toPlayer.Normalize(); // Convert to a directional arrow with a length of exactly 1

        // ----------------------------------------
        // MATH OPTIMIZATION: The Dot Product Cone
        // ----------------------------------------
        // Unlike the Minotaur which used Vector3.Angle (which runs an expensive Acos function),
        // we use the Dot Product here. The Dot Product compares two normalized directional arrows
        // and returns a number between 1 (looking exactly at it) and -1 (looking exactly away).

        // Convert the total cone (e.g. 120) to a half-angle (60), and convert degrees to radians
        float halfAngleRad = 0.5f * visibilityConeDegrees * Mathf.Deg2Rad;

        // Get the Cosine of that half angle. For 60 degrees, Cos(60) = 0.5.
        // This gives us our "Threshold". Any dot product greater than 0.5 means they are inside the cone!
        float dotThreshold = Mathf.Cos(halfAngleRad);

        // Calculate the actual Dot Product between where the Boo is facing, and where the player is
        float dot = Vector3.Dot(transform.forward, toPlayer);

        // The player is "seen" if their dot product passes the math test AND they are close enough
        bool nowVisible = (dot >= dotThreshold) && (dist <= visibilityRange);

        // ----------------------------------------
        // The Debounce Pattern (State Change Logic)
        // ----------------------------------------
        // We only want to trigger the visual fade animation on the EXACT frame the player 
        // enters or exits the vision cone. We do not want to trigger it 60 times a second.
        if (nowVisible != lastVisible)
        {
            // The state just changed
            if (visuals)
                visuals.SetVisible(nowVisible); // Play the smooth shader fade
            else
                foreach (var r in renderersToHide) if (r) r.enabled = nowVisible; // Or hard-snap visibility

            // Update our trackers to match the new reality
            visibleToPlayer = nowVisible;
            lastVisible = nowVisible;
        }
        else
        {
            // The state is exactly the same as last frame, do nothing visually
            visibleToPlayer = nowVisible;
        }
    }


    // -----------------------------------------------------------------------------
    // The Shooting Engine
    // -----------------------------------------------------------------------------
    /// <summary>
    /// An independent loop that runs in the background, firing projectiles 
    /// as long as the player is visible and in range.
    /// </summary>
    /// ----------------------------------------------------------------------------
    IEnumerator FireRoutine()
    {
        // Memory Optimization: Creating a 'new WaitForSeconds' creates "Garbage" in the computer's memory.
        // By creating it exactly once outside the loop and caching it in a variable, 
        // we prevent the Unity Garbage Collector from causing micro-stutters during gameplay.
        var wait = new WaitForSeconds(fireInterval);

        // The Infinite Coroutine Loop
        // This loop runs forever, completely detached from the main Update() loop, 
        // until the enemy is destroyed.
        while (true)
        {
            // Pause the execution of this specific function for the designated interval (e.g. 1.5 seconds)
            yield return wait;

            // Safety check: Ensure all necessary references still exist before trying to shoot
            // Using 'continue' skips the rest of the loop for this iteration, but keeps the loop alive
            if (!player || !projectilePrefab || !firePoint) continue;

            float dist = Vector3.Distance(transform.position, player.position);

            // The Firing Condition:
            // 1. Is the Boo currently materialized (visibleToPlayer)?
            // 2. Is the player actually close enough to hit (dist <= shootRange)?
            if (visibleToPlayer && dist <= shootRange)
            {
                // Calculate the exact rotation needed to aim at the player.
                // We add Vector3.up / 3f to the player's position so the projectile aims 
                // slightly higher (at their chest) rather than aiming at their toes.
                var aim = Quaternion.LookRotation((player.position + Vector3.up / 3f) - firePoint.position);

                // Spawn the projectile into the world at the firePoint location, with the calculated aim
                Instantiate(projectilePrefab, firePoint.position, aim);
            }
        }
    }
}