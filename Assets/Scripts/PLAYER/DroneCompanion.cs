using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// -----------------------------------------------------------------------------
// A hovering companion that follows the player, avoids obstacles, and can be commanded 
// to fly ahead and sweep the area with a downward detection cone to locate enemies.
// -----------------------------------------------------------------------------
public class DroneCompanion : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    [Tooltip("A prefab to spawn over the enemy's head when pinged.")]
    public GameObject pingPrefab;
    [Tooltip("Drag the Canvas (UI) here so the drone knows where to spawn the markers.")]
    public Transform uiCanvas;

    [Header("Follow Settings")]
    [Tooltip("Target position relative to the player's facing direction.")]
    public Vector3 followOffset = new Vector3(1.5f, 2.5f, -1.5f);
    public float followSpeed = 5f;
    [Tooltip("How high the drone bobs up and down.")]
    public float hoverAmplitude = 0.5f;
    [Tooltip("How fast the drone bobs up and down.")]
    public float hoverFrequency = 1f;

    [Header("Collision & Avoidance")]
    [Tooltip("Layers the drone should treat as solid walls/ground.")]
    public LayerMask obstacleMask;
    [Tooltip("The physical size of the drone used for collision checks.")]
    public float droneRadius = 0.4f;
    [Tooltip("The absolute lowest distance the drone will allow itself to fly above the ground.")]
    public float minGroundClearance = 1.2f;

    [Header("Scout Flight")]
    public KeyCode scanKey = KeyCode.Q;
    [Tooltip("How high to ascend before flying forward.")]
    public float scoutAscentHeight = 8f;
    [Tooltip("How far ahead the drone flies.")]
    public float scoutDistance = 25f;
    public float scoutFlySpeed = 15f;

    [Header("Cone Scanning")]
    [Tooltip("How far down the cone reaches.")]
    public float scanMaxDepth = 30f;
    [Tooltip("The half-angle of the scanning cone (e.g., 45 means a 90-degree wide cone).")]
    public float scanConeAngle = 45f;
    [Tooltip("How long the ping visual lasts.")]
    public float pingDuration = 3f;
    public LayerMask enemyMask;

    // ----------------------------------------
    // Runtime State
    // ----------------------------------------
    private bool isScanning = false;        // Locks the state machine so we don't trigger multiple scans
    private Vector3 currentVelocity;        // Required by SmoothDamp to track momentum between frames
    private LineRenderer lineRenderer;      // Component used to draw the cyan lasers to detected enemies

    // -----------------------------------------------------------------------------
    // Initialization: Find missing references and build the LineRenderer dynamically
    // -----------------------------------------------------------------------------
    void Awake()
    {
        // Auto-find player if empty to prevent NullReferenceExceptions
        if (!player)
        {
            var pGo = GameObject.FindGameObjectWithTag("Player");
            if (pGo) player = pGo.transform;
        }

        // Auto-setup LineRenderer (creates a basic unlit cyan material automatically)
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.positionCount = 0;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.cyan;
        lineRenderer.endColor = new Color(0f, 1f, 1f, 0f); // Fades out at the target
    }

    // -----------------------------------------------------------------------------
    // Main Update Loop: Routes logic based on the current state (Scanning vs Following)
    // -----------------------------------------------------------------------------
    void Update()
    {
        if (!player) return; // Safety check

        // Trigger the scout sequence
        if (Input.GetKeyDown(scanKey) && !isScanning)
        {
            StartCoroutine(ScoutAndScanRoutine());
        }

        // Default follow behavior
        if (!isScanning)
        {
            HandleMovement();
        }
    }

    // -----------------------------------------------------------------------------
    // Smoothly moves the drone to its target offset position behind the player,
    // actively checking for walls and floors to avoid clipping through terrain.
    // -----------------------------------------------------------------------------
    void HandleMovement()
    {
        // Calculate where the drone *wants* to be (Offset applied relative to player's rotation)
        Vector3 idealPos = player.position + player.TransformDirection(followOffset);

        // Start the check from slightly above the player to avoid clipping the ground immediately
        Vector3 castStart = player.position + (Vector3.up * 1f);
        Vector3 castDir = idealPos - castStart; // Direction and distance to the ideal position

        Vector3 targetPos = idealPos; // This will be modified by the checks below to ensure a safe final position

        // Wall check: Cast a sphere toward the target position. 
        // If a wall is hit, pull the target position back along the normal of the wall.
        if (Physics.SphereCast(castStart, droneRadius, castDir.normalized, out RaycastHit hit, castDir.magnitude, obstacleMask, QueryTriggerInteraction.Ignore)) // This checks for any obstacles in the way of the drone's ideal position, treating them as solid objects to avoid
        {
            targetPos = hit.point + (hit.normal * droneRadius); // Pull the target position back to the surface of the wall, preventing the drone from trying to occupy the same space as the wall and instead sliding along it
        }

        // Floor check: Cast a ray straight down from the resulting position.
        // If the ground is too close, force the target position upwards.
        if (Physics.Raycast(targetPos + (Vector3.up * 2f), Vector3.down, out RaycastHit groundHit, 5f, obstacleMask, QueryTriggerInteraction.Ignore)) // This checks for the ground below the drone's target position to ensure it doesn't try to fly into the ground, especially when following over hills or cliffs. It starts the raycast from above to prevent small bumps in the terrain from interfering with the drone's movement.
        {
            if (targetPos.y - groundHit.point.y < minGroundClearance) // If the drone's target position is too close to the ground...
            {
                targetPos.y = groundHit.point.y + minGroundClearance; // ...then pull it up to the minimum clearance height, ensuring the drone always maintains a safe distance from the terrain and doesn't try to fly through it.
            }
        }

        // Apply a sine wave to the Y-axis to create a breathing/hovering effect
        targetPos.y += Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;

        // SmoothDamp acts like a spring, organically pulling the drone toward the targetPos
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, 1f / followSpeed);

        // Always face the same direction the player is facing
        Quaternion targetRot = Quaternion.LookRotation(player.forward);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followSpeed);
    }

    // -----------------------------------------------------------------------------
    // Calculates a destination coordinate for the drone to fly to during a scout,
    // stopping short if there is a wall in the way so it doesn't fly into a mountain.
    // -----------------------------------------------------------------------------
    Vector3 GetSafeScoutXZ(Vector3 startPos)
    {
        // Use the Wolf's flat forward direction (ignoring looking up or down)
        // This ensures the target distance is measured parallel to the ground, even if the player is looking at the sky
        Vector3 forwardDir = player.forward;
        forwardDir.y = 0f;

        // Fallback in case the player somehow has exactly 0 forward velocity
        if (forwardDir.sqrMagnitude < 0.01f) forwardDir = transform.forward;
        forwardDir.Normalize();

        float actualTravelDist = scoutDistance; // This will be reduced if a wall is detected in the way.

        // Cast forward to check for walls, starting slightly higher to ignore small contours
        Vector3 castStart = startPos + (Vector3.up * 2f); // Start the cast from above to prevent the drone from trying to fly through small hills or bumps in the terrain
        if (Physics.SphereCast(castStart, droneRadius, forwardDir, out RaycastHit hit, scoutDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // If it hits a wall that is relatively steep (not just a minor slope), stop before it
            if (hit.normal.y < 0.5f) // This means the surface is more vertical than horizontal, so it's likely a wall and not just the ground
            {
                actualTravelDist = Mathf.Max(0, hit.distance - droneRadius); // Reduce the travel distance to just before the wall.
            }                                                                // The Max with 0 prevents negative distances in case of very close walls.
        }
        // Calculate the final target position based on the (potentially reduced) travel distance
        return startPos + (forwardDir * actualTravelDist);
    }

    // -----------------------------------------------------------------------------
    // The main sequence that handles the drone ascending, flying out, scanning, and returning.
    // Runs as a Coroutine so it can pause execution (yield return null) and wait for frames.
    // -----------------------------------------------------------------------------
    IEnumerator ScoutAndScanRoutine()
    {
        isScanning = true;
        // Keeps track of which targets already had UI elements created for them
        // A HashSet is used instead of a List because lookups (checking if it exists) are significantly faster
        HashSet<Transform> alreadyPinged = new HashSet<Transform>();

        // ----------------------------------------
        // Phase 1: Ascend relative to the ground directly below
        // ----------------------------------------
        float targetAscentY = transform.position.y + scoutAscentHeight; // Default target ascent if no obstructions are detected
        // Measure ground height to ensure consistency
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit startGroundHit, 20f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            targetAscentY = startGroundHit.point.y + scoutAscentHeight; // Adjust the target ascent to be relative to the ground directly below the drone, ensuring it always ascends to the correct height above the terrain, even if the player is on a hill or cliff.
        }

        Vector3 startAscent = transform.position; // The starting point of the ascent, which is the drone's current position.
        Vector3 ascentTarget = new Vector3(startAscent.x, targetAscentY, startAscent.z); // Target position for the ascent phase

        // Ceiling check: Prevent flying through roofs
        if (Physics.SphereCast(startAscent, droneRadius, Vector3.up, out RaycastHit ceilingHit, scoutAscentHeight, obstacleMask, QueryTriggerInteraction.Ignore)) // This checks for any ceilings or overhangs above the drone's starting position to ensure it doesn't try to fly through them during the ascent phase. It uses a SphereCast to account for the drone's physical size and prevent clipping.
        {
            ascentTarget.y = startAscent.y + Mathf.Max(0, ceilingHit.distance - droneRadius); // If a ceiling is detected, reduce the ascent target to just below the ceiling, ensuring the drone doesn't try to ascend into it. The Max with 0 prevents negative ascent targets in case of very low ceilings.
        }

        // Move upwards until destination is reached
        while (Vector3.Distance(transform.position, ascentTarget) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, ascentTarget, scoutFlySpeed * Time.deltaTime);
            yield return null; // Wait for the next frame before continuing the loop, allowing the ascent to happen smoothly over time rather than instantly.
        }

        // ----------------------------------------
        // Phase 2: Forward Flight (Terrain Hugging)
        // ----------------------------------------
        Vector3 scoutDestXZ = GetSafeScoutXZ(transform.position);

        // Flatten the coordinates to only track horizontal movement distance
        Vector3 currentFlatPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 destFlatPos = new Vector3(scoutDestXZ.x, 0f, scoutDestXZ.z);

        while (Vector3.Distance(currentFlatPos, destFlatPos) > 0.1f) // While the drone is still far from the horizontal destination, keep moving forward. The Y position will be handled separately to allow for dynamic terrain hugging.
        {
            // Move horizontally toward destination
            currentFlatPos = Vector3.MoveTowards(currentFlatPos, destFlatPos, scoutFlySpeed * Time.deltaTime);

            // Dynamically recalculate height based on the terrain immediately beneath the drone
            float desiredY = transform.position.y;
            Vector3 rayStart = new Vector3(currentFlatPos.x, transform.position.y + 100f, currentFlatPos.z); // Start the raycast from high above the drone's current position to ensure it detects the ground even if the drone is flying over a cliff or steep hill.

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit groundHit, 200f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                desiredY = groundHit.point.y + scoutAscentHeight; // Set the desired Y to be a fixed height above the ground directly below the drone, allowing it to hug the terrain and maintain a consistent altitude relative to the ground, even as it flies over hills and valleys.
            }

            // Blend the height changes to create a smooth swooping contour over hills
            float smoothedY = Mathf.Lerp(transform.position.y, desiredY, Time.deltaTime * 5f);

            // Apply the new combined position
            transform.position = new Vector3(currentFlatPos.x, smoothedY, currentFlatPos.z);

            // Spin visually while running the scan logic
            transform.Rotate(Vector3.up, 720f * Time.deltaTime);
            PerformConeScan(alreadyPinged);

            yield return null; // Wait for the next frame before continuing the loop, allowing the forward flight and scanning to happen smoothly over time rather than instantly.
        }

        // ----------------------------------------
        // Phase 3: Hover briefly at the end of the flight path
        // ----------------------------------------
        float lingerTime = 0.5f;
        // Continue scanning and spinning in place while hovering to give the player a moment to see the results at the end of the scout path before it returns.
        while (lingerTime > 0)
        {
            lingerTime -= Time.deltaTime;
            transform.Rotate(Vector3.up, 720f * Time.deltaTime);
            PerformConeScan(alreadyPinged);
            yield return null;
        }

        // ----------------------------------------
        // Phase 4: Clean up lasers before returning
        // ----------------------------------------
        lineRenderer.positionCount = 0;

        // ----------------------------------------
        // Phase 5: Terrain-Hugging Return Flight (With Collision Avoidance)
        // ----------------------------------------
        while (true)
        {
            // Calculate where the drone belongs relative to the player
            Vector3 idealPos = player.position + player.TransformDirection(followOffset);
            currentFlatPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 targetFlatPos = new Vector3(idealPos.x, 0f, idealPos.z);

            float distToPlayer = Vector3.Distance(currentFlatPos, targetFlatPos);

            // If close enough, break out of this loop to let the standard follow logic resume
            if (distToPlayer < 1.5f) break;

            // 1. Calculate desired XZ step
            Vector3 nextFlatPos = Vector3.MoveTowards(currentFlatPos, targetFlatPos, scoutFlySpeed * Time.deltaTime);

            // 2. Calculate desired Y (Terrain hugging & swooping)
            float desiredY = idealPos.y;
            Vector3 rayStart = new Vector3(nextFlatPos.x, player.position.y + 100f, nextFlatPos.z);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit groundHit, 200f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                // Smoothly interpolate between the high scout altitude and the low shoulder altitude based on distance
                float flightHeight = groundHit.point.y + scoutAscentHeight;
                float swoopBlend = Mathf.Clamp01(distToPlayer / 10f);
                desiredY = Mathf.Lerp(idealPos.y, flightHeight, swoopBlend);
            }

            float smoothedY = Mathf.Lerp(transform.position.y, desiredY, Time.deltaTime * 5f);
            Vector3 target3DPos = new Vector3(nextFlatPos.x, smoothedY, nextFlatPos.z);

            // 3. Horizontal Collision Avoidance
            Vector3 moveDir = target3DPos - transform.position;
            float moveDist = moveDir.magnitude;

            // Cast a sphere ahead of the drone for this frame's movement to slide around trees/rocks
            if (Physics.SphereCast(transform.position, droneRadius, moveDir.normalized, out RaycastHit wallHit, moveDist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                // If it hits a tree or rock, pull it back to the hit surface
                target3DPos = wallHit.point + (wallHit.normal * droneRadius);
            }

            // Apply final safe position
            transform.position = target3DPos;

            // Look at player while returning
            Vector3 lookDir = (idealPos - transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }

            yield return null;
        }

        // Finally hand back to standard follow logic
        isScanning = false;
    }

    // -----------------------------------------------------------------------------
    // Searches for valid targets underneath the drone, creates UI markers for them, 
    // and draws lasers connecting the drone to the targets.
    // -----------------------------------------------------------------------------
    void PerformConeScan(HashSet<Transform> alreadyPinged)
    {
        // 1. Get absolutely everything in a large sphere reaching max depth
        // Use an enemyMask here so we don't waste performance checking rocks and trees
        Collider[] hits = Physics.OverlapSphere(transform.position, scanMaxDepth, enemyMask);
        List<Transform> currentlyVisible = new List<Transform>();

        foreach (var hit in hits)
        {
            // 2. Filter by angle to create the downward cone effect
            // Find the direction from the drone to the enemy
            Vector3 dirToTarget = hit.transform.position - transform.position;

            // Check if the angle between straight down and the target is within the designated cone
            if (Vector3.Angle(Vector3.down, dirToTarget) <= scanConeAngle)
            {
                // Only target the Minotaur Chasers
                if (hit.GetComponentInParent<ChaserEnemyAI>() != null)
                {
                    currentlyVisible.Add(hit.transform); // add to the list of currently visible targets for laser drawing

                    // 3. Drop the ping prefab if it has not pinged them yet this run
                    if (!alreadyPinged.Contains(hit.transform)) // If not pinged yet...
                    {
                        alreadyPinged.Add(hit.transform); // ...then add to HashSet so we never ping them again this scout trip

                        if (pingPrefab && uiCanvas)
                        {
                            // Spawn it inside the UI Canvas
                            GameObject pingObj = Instantiate(pingPrefab, uiCanvas);

                            // Send the transform data to the marker so it knows what to follow
                            PingMarker marker = pingObj.GetComponent<PingMarker>();
                            if (marker != null)
                            {
                                marker.Initialize(hit.transform);
                            }

                            // Auto-destroy the marker after pingDuration to clean up memory
                            Destroy(pingObj, pingDuration);
                        }
                    }
                }
            }
        }

        // 4. Draw Lasers dynamically to targets currently inside the cone
        lineRenderer.positionCount = currentlyVisible.Count * 2; // start and end points for each target

        for (int i = 0; i < currentlyVisible.Count; i++)
        {
            // Point A (Even index): The Drone
            lineRenderer.SetPosition(i * 2, transform.position);
            // Point B (Odd index): The Enemy (slightly above their feet so it hits their chest)
            lineRenderer.SetPosition(i * 2 + 1, currentlyVisible[i].position + (Vector3.up * 1.5f));
        }
    }
}