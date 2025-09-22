using UnityEngine;
using System.Collections;
using System.Linq;

public class PlayerDeathHandler : MonoBehaviour
{
    [Header("What to disable on death")]
    public MonoBehaviour[] controlScripts;   // e.g., PlayerMotor, camera look, combat
    public CharacterController cc;

    [Header("Animation")]
    public Animator anim;
    public float deathAnimDuration = 1.0f;

    // NEW: optional – auto-found
    public ModelGroundAlignAndFace faceAlign;

    Collider[] colliders;
    Rigidbody rb;
    Health hp;
    bool isDead;

    void Awake()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        colliders = GetComponentsInChildren<Collider>(true);
        rb = GetComponent<Rigidbody>();

        if (!anim || !anim.runtimeAnimatorController)
        {
            anim = GetComponentsInChildren<Animator>(true)
                   .FirstOrDefault(a => a && a.runtimeAnimatorController);
        }

        // NEW: auto-find if not assigned
        if (!faceAlign) faceAlign = GetComponent<ModelGroundAlignAndFace>();
    }

    void Start()
    {
        hp = GetComponent<Health>();
        hp.onDeath.AddListener(OnDied);
    }

    void OnDied()
    {
        if (isDead) return;
        isDead = true;

        if (cc) cc.enabled = false;
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        foreach (var c in colliders) if (c) c.enabled = false;
        foreach (var s in controlScripts) if (s) s.enabled = false;

        // NEW: stop the facing script so the model can’t spin
        if (faceAlign) faceAlign.enabled = false;

        var bridge = GetComponent<WolfAnimatorBridge>();
        if (bridge != null) bridge.TriggerDie();
        else if (anim && anim.runtimeAnimatorController) anim.SetTrigger("Die");

        StartCoroutine(NotifyGMAfter(deathAnimDuration));
    }

    IEnumerator NotifyGMAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        GameManager.I.OnPlayerDied();
    }

    // Called by GameManager before showing the player again
    public void SetDeadState(bool dead)
    {
        isDead = dead;

        if (rb) rb.isKinematic = dead;
        if (cc) cc.enabled = !dead;
        foreach (var c in colliders) if (c) c.enabled = !dead;
        foreach (var s in controlScripts) if (s) s.enabled = !dead;

        // NEW: re-enable facing on revive
        if (faceAlign) faceAlign.enabled = !dead;

        if (!dead && anim && anim.runtimeAnimatorController)
        {
            anim.Rebind();
            anim.Update(0f);
        }
    }
}
