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

        StartCoroutine(DespawnRoutine());
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
