using UnityEngine;

public class SimpleChaseCamera : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;        // PlayerRoot
    public Camera cam;              // Main Camera child

    [Header("Orbit")]
    public float height = 1.6f;     // orbit center above target
    public float distance = 7f;     // default follow distance
    public float minDistance = 2.5f, maxDistance = 12f;
    public float yaw = 0f, pitch = 20f;
    public float minPitch = -5f, maxPitch = 55f;
    public float yawSpeed = 180f, pitchSpeed = 140f;

    [Header("Collision")]
    public float collisionRadius = 0.35f;
    public float collisionPadding = 0.25f;
    public float minGroundClearance = 0.6f;
    public LayerMask collideMask;   // MUST include Terrain/Ground + Environment, exclude Player

    [Header("Smoothing")]
    public float followLerp = 12f;  // smoothing of the *target position*
    public float camLerp = 20f;     // smoothing of the camera itself

    // runtime state
    Vector3 curPos;
    Quaternion curRot;
    Vector3 followPos;              // smoothed target position (virtual anchor)

    void Start()
    {
        if (!cam) cam = GetComponentInChildren<Camera>();
        if (!target || !cam) return;

        followPos = target.position;                  // start anchored at target
        curPos = cam.transform.position;
        curRot = cam.transform.rotation;

        // Apply the player's saved sensitivity multiplier
        float sensMult = PlayerPrefs.GetFloat("MouseSensitivitySetting", 1f);
        yawSpeed *= sensMult;
        pitchSpeed *= sensMult;
    }

    void Update()
    {
        // Mouse orbit (unscaled so it still works if the game pauses)
        yaw += Input.GetAxis("Mouse X") * yawSpeed * Time.unscaledDeltaTime;
        pitch -= Input.GetAxis("Mouse Y") * pitchSpeed * Time.unscaledDeltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Scroll zoom
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
            distance = Mathf.Clamp(distance - scroll, minDistance, maxDistance);
    }

    void LateUpdate()
    {
        if (!target || !cam) return;

        // --- 1) Smooth a *virtual* follow position (we no longer move this transform) ---
        float kFollow = 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        followPos = Vector3.Lerp(followPos, target.position, kFollow);

        // --- 2) Build a world-up basis (stable, no tilt drift) ---
        Vector3 up = Vector3.up;
        Quaternion yawQ = Quaternion.AngleAxis(yaw, up);
        Vector3 fwd = yawQ * Vector3.forward;
        Vector3 right = Vector3.Cross(up, fwd);
        Vector3 camFwd = Quaternion.AngleAxis(pitch, right) * fwd;

        // --- 3) Desired camera from lifted orbit center ---
        Vector3 orbitCenter = followPos + up * height;
        Vector3 desired = orbitCenter - camFwd * distance;
        Quaternion desiredRot = Quaternion.LookRotation(camFwd, up);

        // --- 4) Collision push-in from orbit center to desired ---
        Vector3 ray = desired - orbitCenter;
        float len = ray.magnitude;
        Vector3 dir = (len > 1e-4f) ? ray / len : -camFwd;

        // cast a thick sphere along that path. If it hits a wall/tree, pull the camera inward
        if (Physics.SphereCast(orbitCenter, collisionRadius, dir, out var hit, len, collideMask, QueryTriggerInteraction.Ignore))
            desired = hit.point - dir * collisionPadding;

        // keep a little clearance above ground (prevents ground flicker)
        if (Physics.Raycast(desired + up * 0.5f, -up, out var floorHit, 2f, collideMask))
        {
            float h = Vector3.Dot(desired - floorHit.point, up);
            if (h < minGroundClearance) desired = floorHit.point + up * minGroundClearance;
        }

        // --- 5) Smooth camera towards desired (single smoothing stage) ---
        float kCam = 1f - Mathf.Exp(-camLerp * Time.deltaTime);
        curPos = Vector3.Lerp(curPos, desired, kCam);
        curRot = Quaternion.Slerp(curRot, desiredRot, kCam);

        cam.transform.SetPositionAndRotation(curPos, curRot);
    }
}
