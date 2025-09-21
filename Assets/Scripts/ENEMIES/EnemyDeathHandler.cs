using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class EnemyDeathHandler : MonoBehaviour
{
    public Animator anim;
    public string dieTrigger = "die";
    public float sinkAfter = 1.0f;
    public float sinkSpeed = 1.5f;
    public float destroyAfter = 5f;

    NavMeshAgent agent;
    Collider[] cols;
    Rigidbody rb;

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
        // stop AI entirely (extra safety on top of AI’s own handler)
        var ai = GetComponent<ChaserEnemyAI>();
        if (ai) ai.enabled = false;

        if (agent) agent.enabled = false;
        if (rb) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        foreach (var c in cols) c.enabled = false;

        if (anim && !string.IsNullOrEmpty(dieTrigger)) anim.SetTrigger(dieTrigger);
        StartCoroutine(DespawnRoutine());
    }

    IEnumerator DespawnRoutine()
    {
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
