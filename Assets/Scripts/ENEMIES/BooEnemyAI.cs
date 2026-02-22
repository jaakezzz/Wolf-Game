using UnityEngine;
using System.Collections;

/// <summary>
/// Controls a stationary "turret" style enemy that remains hidden until the player 
/// enters its line of sight and range. Once visible, it fires projectiles at a set interval.
/// </summary>
public class BooEnemyAI : MonoBehaviour
{
    [Tooltip("The target the enemy will look for and shoot at.")]
    public Transform player;
    [Tooltip("Renderers to disable when hiding, used as a fallback if no dissolve script is found.")]
    public Renderer[] renderersToHide;     // fallback only
    [Tooltip("Script handling the smooth fade in/out visual effect.")]
    public GhostDissolve visuals;

    [Header("Perception")]
    [Range(10f, 100f)] public float visibilityRange = 25f;   // how far Boo can see
    [Range(10f, 179f)] public float visibilityConeDegrees = 120f; // total cone angle
    [Tooltip("Maximum distance at which the enemy will actually fire a projectile.")]
    public float shootRange = 18f;
    [Tooltip("Time in seconds between each fired projectile.")]
    public float fireInterval = 1.5f;

    [Header("Attack")]
    [Tooltip("The object to spawn when attacking.")]
    public GameObject projectilePrefab;
    [Tooltip("The exact position the projectile spawns from (e.g., the tip of a wand or mouth).")]
    public Transform firePoint;

    // State tracking variables
    bool visibleToPlayer = true;
    bool lastVisible = true;    // debounce (used to only trigger visual changes once when the state shifts)
    Coroutine fireLoop;

    void Start()
    {
        // Automatically find the visuals script if it wasn't assigned in the Unity Editor
        if (!visuals) visuals = GetComponentInChildren<GhostDissolve>();

        // Automatically find the player by looking for the "Player" tag
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // If no specific renderers were assigned, grab all of them on this object and its children
        if (renderersToHide == null || renderersToHide.Length == 0)
            renderersToHide = GetComponentsInChildren<Renderer>(true);

        // Start the infinite loop that handles shooting
        fireLoop = StartCoroutine(FireRoutine());
    }

    void Update()
    {
        // Stop calculating if the player doesn't exist or was destroyed
        if (!player) return;

        // Direction & distance to player (flatten Y so height doesn't skew angle)
        // Flattening the Y-axis ensures the cone check works like a flat pie slice on the ground, 
        // preventing the enemy from losing sight just because the player jumped.
        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;
        toPlayer.y = 0f;

        // Avoid division by zero errors if the player is standing exactly inside the enemy
        if (toPlayer.sqrMagnitude < 0.0001f) return;
        toPlayer.Normalize();

        // Angle test (cone): dot >= cos(half-angle)
        // Converts the total cone angle into radians, cuts it in half, and gets the cosine value.
        // This math trick allows for a very fast directional check without using expensive physics casts.
        float halfAngleRad = 0.5f * visibilityConeDegrees * Mathf.Deg2Rad;
        float dotThreshold = Mathf.Cos(halfAngleRad);         // 120° -> 60° half-angle -> 0.5
        float dot = Vector3.Dot(transform.forward, toPlayer);

        // Must be inside both the directional cone AND close enough in distance
        bool nowVisible = (dot >= dotThreshold) && (dist <= visibilityRange);

        // Update visuals only on change (this prevents spamming the fade animation every single frame)
        if (nowVisible != lastVisible)
        {
            // If the dissolve script exists, use it. Otherwise, instantly snap the renderers on/off.
            if (visuals) visuals.SetVisible(nowVisible);
            else foreach (var r in renderersToHide) if (r) r.enabled = nowVisible;

            visibleToPlayer = nowVisible;
            lastVisible = nowVisible;
        }
        else
        {
            visibleToPlayer = nowVisible;
        }
    }

    /// <summary>
    /// An independent loop that runs in the background, firing projectiles 
    /// as long as the player is visible and in range.
    /// </summary>
    IEnumerator FireRoutine()
    {
        // Create the wait instruction once to save memory
        var wait = new WaitForSeconds(fireInterval);

        // This loop runs forever as long as the GameObject exists
        while (true)
        {
            // Pause the loop for the set interval before continuing
            yield return wait;

            // Safety check: ensure all necessary references still exist before trying to shoot
            if (!player || !projectilePrefab || !firePoint) continue;

            float dist = Vector3.Distance(transform.position, player.position);

            // Only shoot if the enemy is currently materialized and the player is close enough
            if (visibleToPlayer && dist <= shootRange)
            {
                // Calculate the rotation needed to look directly at the player's chest/center
                var aim = Quaternion.LookRotation((player.position + Vector3.up / 3f) - firePoint.position);

                // Spawn the projectile
                Instantiate(projectilePrefab, firePoint.position, aim);
            }
        }
    }
}