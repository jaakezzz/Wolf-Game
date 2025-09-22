using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class EnemyDeathHandler : MonoBehaviour
{
    [Header("Anim")]
    public Animator anim;
    public string dieTrigger = "die";

    [Header("Despawn")]
    public float sinkAfter = 1.0f;   // wait so the death anim + vfx can read
    public float sinkSpeed = 1.5f;
    public float destroyAfter = 5f;

    [Header("Death VFX")]
    [Tooltip("Prefab of your one-shot death particle system. Leave empty to skip.")]
    public ParticleSystem deathVFX;
    public Vector3 vfxOffset = new Vector3(0f, 0.8f, 0f);
    [Min(0.01f)] public float vfxScale = 1f;

    [Header("Drops")]
    [Tooltip("Speed powerup prefab (e.g., your collectible).")]
    public GameObject speedPickupPrefab;
    [Tooltip("Heal powerup prefab (e.g., your collectible).")]
    public GameObject healPickupPrefab;
    [Tooltip("Where to spawn the drop relative to the enemy.")]
    public Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);

    NavMeshAgent agent;
    Collider[] cols;
    Rigidbody rb;
    bool alreadyDied;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        cols = GetComponentsInChildren<Collider>(true);
        rb = GetComponent<Rigidbody>();
        if (!anim) anim = GetComponentInChildren<Animator>();

        GetComponent<Health>().onDeath.AddListener(OnDeath);
    }

    void OnDeath()
    {
        if (alreadyDied) return;   // guard against duplicate invokes
        alreadyDied = true;

        // stop AI entirely (extra safety on top of AI’s own handler)
        var ai = GetComponent<ChaserEnemyAI>();
        if (ai) ai.enabled = false;

        if (agent) agent.enabled = false;
        if (rb) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        foreach (var c in cols) if (c) c.enabled = false;

        // Animate
        if (anim && !string.IsNullOrEmpty(dieTrigger)) anim.SetTrigger(dieTrigger);

        // Spawn VFX (your prefab should auto-destroy via Stop Action: Destroy)
        if (deathVFX)
        {
            var vfx = Instantiate(deathVFX, transform.position + vfxOffset, Quaternion.identity);
            if (Mathf.Abs(vfxScale - 1f) > 0.001f) vfx.transform.localScale *= vfxScale;
        }

        // NEW: decide and spawn a drop
        MaybeSpawnDrop();

        StartCoroutine(DespawnRoutine());
    }

    void MaybeSpawnDrop()
    {
        // Figure out if the player's run is already unlocked
        bool runUnlocked = false;
        var gm = GameManager.I;
        if (gm && gm.player)
        {
            var motor = gm.player.GetComponent<PlayerMotor>();
            if (motor) runUnlocked = motor.runUnlocked;
        }

        // Choose outcome
        // If run is unlocked -> 50% heal, 50% nothing
        // If not unlocked -> 1/3 nothing, 1/3 speed, 1/3 heal
        Vector3 spawnPos = transform.position + dropOffset;

        if (runUnlocked)
        {
            // 0.5 heal, 0.5 nothing
            if (Random.value < 0.5f)
            {
                if (healPickupPrefab) Instantiate(healPickupPrefab, spawnPos, Quaternion.identity);
            }
            // else: nothing
            return;
        }
        else
        {
            float r = Random.value; // [0,1)
            if (r < 1f / 3f)
            {
                // nothing
                return;
            }
            else if (r < 2f / 3f)
            {
                if (speedPickupPrefab) Instantiate(speedPickupPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                if (healPickupPrefab) Instantiate(healPickupPrefab, spawnPos, Quaternion.identity);
            }
        }
    }

    IEnumerator DespawnRoutine()
    {
        // small hold so death pose + VFX read
        yield return new WaitForSeconds(sinkAfter);

        float t = 0f;
        var start = transform.position;
        while (t < destroyAfter)
        {
            t += Time.deltaTime;
            transform.position = start + Vector3.down * (t * sinkSpeed * 0.2f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
