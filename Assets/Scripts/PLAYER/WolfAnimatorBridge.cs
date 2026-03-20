using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class WolfAnimatorBridge : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Animator anim;              // assign PlayerRoot's Animator
    CharacterController cc;

    [Header("Animator Params")]
    [SerializeField] string speedParam = "Speed";
    [SerializeField] float dampTime = 0.05f;     // smoothing for SetFloat damping

    [Header("Speed Feed (1.0 = walk)")]
    [Tooltip("When ON, Speed param ~= (m/s) / walkSpeedRef. So 1.0 ~ walk, 1.5 ~ run if run is ~1.5x walk.")]
    [SerializeField] bool normalizeWithWalk = true;
    [SerializeField] float walkSpeedRef = 4f;    // your actual walk speed in m/s

    [Header("Triggers")]
    [SerializeField] string attack1Trigger = "Attack1";
    [SerializeField] string attack2Trigger = "Attack2";
    [SerializeField] string dieTrigger = "Die";

    int speedHash;

    void Reset()
    {
        anim = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        if (!anim) anim = GetComponentInChildren<Animator>();
        cc = GetComponent<CharacterController>();
        speedHash = Animator.StringToHash(speedParam);
    }

    void Update()
    {
        // planar m/s from CharacterController
        Vector3 v = cc.velocity; v.y = 0f;
        float mps = v.magnitude;

        float value = normalizeWithWalk
            ? (mps / Mathf.Max(0.0001f, walkSpeedRef))   // 1.0 = walk
            : mps;                                       // raw m/s

        anim.SetFloat(speedHash, value, dampTime, Time.deltaTime);
    }

    // ------ External helpers used by other scripts ------
    public void TriggerAttack1()
    {
        if (anim && !string.IsNullOrEmpty(attack1Trigger))
        {
            anim.ResetTrigger(attack1Trigger); // Wipes out ghost triggers
            anim.SetTrigger(attack1Trigger);   // Sets the fresh trigger
        }
    }

    public void TriggerAttack2()
    {
        if (anim && !string.IsNullOrEmpty(attack2Trigger))
        {
            anim.ResetTrigger(attack2Trigger);
            anim.SetTrigger(attack2Trigger);
        }
    }

    //public void TriggerAttack() => TriggerAttack1(); // convenience / legacy
    public void TriggerDie() { if (anim && !string.IsNullOrEmpty(dieTrigger)) anim.SetTrigger(dieTrigger); }

    // Optional tuning at runtime
    public void SetWalkSpeedReference(float metersPerSecond) =>
        walkSpeedRef = Mathf.Max(0.01f, metersPerSecond);

    public void UseRawMetersPerSecond(bool useRaw) => normalizeWithWalk = !useRaw;
}
