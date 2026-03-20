using UnityEngine;

public class ModelGroundAlignAndFace : MonoBehaviour
{
    [Header("References")]
    public Transform model;                // Wolf visual (child of PlayerRoot)
    public CharacterController cc;         // optional
    public Transform moveReference;        // camera or PlayerRoot; used to read input direction

    [Tooltip("Optional: used to lock facing while externally locked (e.g., Sit).")]
    public PlayerMotor motor;              // <- NEW (optional)
    [Tooltip("Optional: if assigned, we lock facing only when sit locks movement.")]
    public SitController sit;              // <- NEW (optional)

    [Header("Raycast")]
    public float rayStartOffset = 0.6f;
    public float rayLength = 2.0f;
    public LayerMask groundMask = ~0;

    [Header("Facing & Tilt")]
    public float minMoveSpeed = 0.05f;     // ignore tiny jitter (m/s)
    public float rotateSpeed = 540f;       // yaw speed (deg/sec)
    public float tiltLerpSpeed = 10f;      // pitch/roll smoothing
    public float maxTiltAngle = 60f;       // clamp extreme slopes
    public float yawOffset = 0f;           // if model's forward isn't +Z, set 90/180/etc

    Vector3 lastDir = Vector3.forward;
    Vector3 smoothedUp = Vector3.up;
    Vector3 prevPos;

    void Awake()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        if (!moveReference) moveReference = Camera.main ? Camera.main.transform : transform;
        if (!motor) motor = GetComponent<PlayerMotor>();
        prevPos = transform.position;
    }

    void LateUpdate()
    {
        if (!model) return;

        // Should we freeze facing updates?
        bool facingLocked =
            (motor && motor.IsExternallyLocked()) ||                 // e.g., SitController with lock ON, traps, stun
            (sit && sitLocksFacingFromSit());                        // explicit sit check (below)

        // --- A) Preferred: use input relative to camera when pressed ---
        if (!facingLocked)
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool hasInput = input.sqrMagnitude > 0.01f;
            if (hasInput)
            {
                Vector3 refFwd = Vector3.ProjectOnPlane(moveReference.forward, Vector3.up).normalized;
                Vector3 refRight = Vector3.Cross(Vector3.up, refFwd);
                Vector3 desired = (refFwd * input.y + refRight * input.x).normalized;
                if (desired.sqrMagnitude > 1e-6f) lastDir = desired;
            }
            else
            {
                // --- B) Fallback: derive from actual motion (slides/knockbacks)
                Vector3 planar;
                if (cc != null) planar = new Vector3(cc.velocity.x, 0f, cc.velocity.z);
                else
                {
                    Vector3 delta = transform.position - prevPos;
                    planar = new Vector3(delta.x, 0f, delta.z) / Mathf.Max(Time.deltaTime, 1e-5f);
                }
                if (planar.sqrMagnitude > minMoveSpeed * minMoveSpeed)
                    lastDir = planar.normalized;
            }
        }
        // if facingLocked: keep lastDir as-is (no turning while seated/locked)

        prevPos = transform.position;

        // --- Ground normal via raycast, smoothed ---
        // Read the terrain's surface normal directly beneath the player
        Vector3 origin = model.position + Vector3.up * rayStartOffset;
        Vector3 groundUp = Vector3.up;
        if (Physics.Raycast(origin, Vector3.down, out var hit, rayLength, groundMask))
        {
            groundUp = hit.normal;
            // Clamp extreme slopes so the model doesn't flip entirely upside down
            float ang = Vector3.Angle(Vector3.up, groundUp);
            if (ang > maxTiltAngle)
                groundUp = Vector3.Slerp(Vector3.up, groundUp, maxTiltAngle / Mathf.Max(ang, 0.001f));
        }
        // Smoothly interpolate the model's Up vector to match the terrain slope
        smoothedUp = Vector3.Slerp(smoothedUp, groundUp, Time.deltaTime * tiltLerpSpeed);

        // Desired forward on the slope plane
        Vector3 wantFwd = Vector3.ProjectOnPlane(lastDir, smoothedUp);
        if (wantFwd.sqrMagnitude < 1e-6f)
            wantFwd = Vector3.ProjectOnPlane(model.forward, smoothedUp);
        wantFwd.Normalize();

        if (Mathf.Abs(yawOffset) > 0.01f)
            wantFwd = Quaternion.AngleAxis(yawOffset, smoothedUp) * wantFwd;

        // Turn toward target with constant angular speed
        Quaternion target = Quaternion.LookRotation(wantFwd, smoothedUp);
        model.rotation = Quaternion.RotateTowards(model.rotation, target, rotateSpeed * Time.deltaTime);
    }

    bool sitLocksFacingFromSit()
    {
        // lock only when sit exists, is sitting, and sit-locks movement
        return sit != null && sit.IsSitting() && sit.sitLocksMovement;
    }
}
