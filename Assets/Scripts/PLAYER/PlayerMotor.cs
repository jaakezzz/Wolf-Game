using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    // --- External control (Sit/Trap/etc) ---
    bool externalLock;                 // hard lock: ignore movement & jump
    bool jumpEnabled = true;           // allow/disallow jump while otherwise movable
    [SerializeField] float groundStick = 3f; // small downward push while locked

    public void SetExternalLock(bool v) { externalLock = v; }
    public void SetJumpEnabled(bool v) { jumpEnabled = v; }
    public bool IsExternallyLocked() { return externalLock; }

    // --- Movement ---
    public float speed = 6f;
    public float gravity = -20f;       // negative
    public float jumpForce = 6f;
    public Transform cam;
    public Vector3 externalImpulse;

    [Header("Jump Reliability")]
    public float coyoteTime = 0.12f;   // still jump shortly after leaving ground
    public float jumpBuffer = 0.12f;   // accept jump slightly before landing

    // --- Attacks ---
    [Header("Attacks")]
    public Animator animator;                 // drag the wolf Animator here (or auto-found)
    public string attack1Trigger = "Attack1";
    public string attack2Trigger = "Attack2";
    public KeyCode attack1Key = KeyCode.Mouse0;
    public KeyCode attack2Key = KeyCode.Mouse1;

    [Tooltip("Temporarily lock movement during attack (optional).")]
    public bool lockMoveDuringAttack = false;
    public float attack1LockTime = 0.35f;
    public float attack2LockTime = 0.45f;

    CharacterController cc;
    float vertVel;
    bool stunned;
    bool attackLocked;                 // internal lock while swinging
    float coyoteTimer;
    float bufferTimer;
    int ignoreGroundedFrames;          // prevents slope from canceling jump
    PlayerMelee melee;   // cache reference

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        melee = GetComponent<PlayerMelee>();   // find the PlayerMelee script
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // --- Handle attack input even when moving ---
        HandleAttackInput();

        // --- Hard locks (stun, external lock, or attack lock) ---
        if (stunned || externalLock || attackLocked)
        {
            // keep controller grounded so we don’t hover on slopes
            if (cc && !cc.isGrounded)
                cc.Move(Vector3.down * groundStick * dt);
            return;
        }

        // ---- Input (WASD relative to camera)
        Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : transform.forward;
        Vector3 right = cam ? cam.right : transform.right;
        Vector2 in2 = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 moveXZ = (right * in2.x + fwd * in2.y).normalized * speed;

        // ---- Grounded check with short grace period
        bool grounded = cc.isGrounded;
        if (ignoreGroundedFrames > 0) { grounded = false; ignoreGroundedFrames--; }

        if (grounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= dt;

        // Jump buffer (only when jump is enabled)
        if (jumpEnabled && Input.GetButtonDown("Jump")) bufferTimer = jumpBuffer;
        else bufferTimer -= dt;

        // Apply buffered / coyote jump
        if (jumpEnabled && bufferTimer > 0f && coyoteTimer > 0f)
        {
            vertVel = jumpForce;
            bufferTimer = 0f;
            coyoteTimer = 0f;
            ignoreGroundedFrames = 2;  // let us fully leave the slope
        }

        // Gravity & stick-to-ground
        if (grounded && vertVel < 0f)
            vertVel = -2f;                 // small downward bias
        vertVel += gravity * dt;

        Vector3 velocity = moveXZ;
        velocity.y = vertVel;

        // add impulses (decay over time)
        if (externalImpulse.sqrMagnitude > 0.01f)
        {
            velocity += externalImpulse;
            externalImpulse = Vector3.Lerp(externalImpulse, Vector3.zero, 5f * dt); // smooth decay
        }

        cc.Move(velocity * dt);
    }

    // --- Attacks ---
    void HandleAttackInput()
    {
        if (Input.GetKeyDown(attack1Key))
            DoAttack(attack1Trigger, attack1LockTime);

        if (Input.GetKeyDown(attack2Key))
            DoAttack(attack2Trigger, attack2LockTime);
    }

    void DoAttack(string triggerName, float lockTime)
    {
        if (stunned || externalLock || attackLocked) return;

        if (animator && !string.IsNullOrEmpty(triggerName))
            animator.SetTrigger(triggerName);

        // Hook into melee
        if (melee)
        {
            if (triggerName == attack1Trigger)
                melee.TriggerAttack1();
            else if (triggerName == attack2Trigger)
                melee.TriggerAttack2();
        }

        if (lockMoveDuringAttack && lockTime > 0f)
            StartCoroutine(AttackLock(lockTime));
    }


    IEnumerator AttackLock(float t)
    {
        attackLocked = true;
        // Also cancel any jump buffer during the lock to avoid instant jump after
        bufferTimer = 0f;
        yield return new WaitForSeconds(t);
        attackLocked = false;
    }

    public void Stun(float duration)
    {
        if (!stunned) StartCoroutine(StunRoutine(duration));
    }

    IEnumerator StunRoutine(float duration)
    {
        stunned = true;
        yield return new WaitForSeconds(duration);
        stunned = false;
    }

    public void ApplyExternalImpulse(Vector3 impulse)
    {
        externalImpulse += impulse;
    }
}
