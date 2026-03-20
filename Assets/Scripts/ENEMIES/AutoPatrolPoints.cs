using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(ChaserEnemyAI))]
public class AutoPatrolPoints : MonoBehaviour
{
    [Header("Generation")]
    [Min(1)] public int pointCount = 3;
    [Min(0f)] public float minSeparation = 10f;
    [Tooltip("Bias toward the outer ring of the interestRadius (0=center, 1=edge).")]
    [Range(0f, 1f)] public float edgeBias = 0.75f;
    [Tooltip("Try this many random samples per point before giving up.")]
    public int maxAttemptsPerPoint = 120;

    [Header("Placement")]
    public bool snapToNavMesh = true;
    [Tooltip("How far we’re willing to search for a nearby NavMesh position.")]
    public float navMeshSampleRange = 4f;
    [Tooltip("Layers considered \"ground\" when we raycast to set the Y height.")]
    public LayerMask groundMask = ~0;

    [Header("When to run")]
    public bool autoGenerateAtRuntime = false;

    ChaserEnemyAI ai;
    Transform container;

    void Awake()
    {
        ai = GetComponent<ChaserEnemyAI>();
        if (autoGenerateAtRuntime) GenerateNow();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!ai) ai = GetComponent<ChaserEnemyAI>();
        pointCount = Mathf.Max(1, pointCount);
        minSeparation = Mathf.Max(0f, minSeparation);
        navMeshSampleRange = Mathf.Max(0.1f, navMeshSampleRange);
        maxAttemptsPerPoint = Mathf.Max(10, maxAttemptsPerPoint);
    }
#endif

    [ContextMenu("Generate Patrol Points")]
    public void GenerateNow()
    {
        if (!ai) ai = GetComponent<ChaserEnemyAI>();
        if (!ai)
        {
            Debug.LogWarning("[AutoPatrolPoints] Missing ChaserEnemyAI.");
            return;
        }

        // Prepare container
        var name = "PatrolPoints (auto)";
        var t = transform.Find(name);
        if (!t)
        {
            container = new GameObject(name).transform;
            container.SetParent(transform);
            container.localPosition = Vector3.zero;
            container.localRotation = Quaternion.identity;
        }
        else
        {
            container = t;
            // Clear old children
            var toDestroy = new List<Transform>();
            foreach (Transform c in container) toDestroy.Add(c);
#if UNITY_EDITOR
            // DestroyImmediate in editor, Destroy in playmode
            if (!Application.isPlaying)
                toDestroy.ForEach(d => DestroyImmediate(d.gameObject));
            else
                toDestroy.ForEach(d => Destroy(d.gameObject));
#else
            toDestroy.ForEach(d => Destroy(d.gameObject));
#endif
        }

        // Generate positions
        var pts = new List<Vector3>(pointCount);
        var validTransforms = new List<Transform>();
        Vector3 center = transform.position;
        float R = Mathf.Max(0.1f, ai.interestRadius);

        int created = 0;
        int globalGuard = 0;

        while (created < pointCount && globalGuard < 10000)
        {
            globalGuard++;

            // Angle around circle
            float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;

            // Radius biased toward edge: r = lerp(inner, outer, bias^2) to push even more to edge
            float inner = Mathf.Lerp(0f, R, Mathf.Clamp01(edgeBias * 0.5f));  // small inner ring
            float outer = Mathf.Lerp(R * 0.7f, R * 0.98f, edgeBias);
            float rbias = Mathf.Lerp(inner, outer, Random.value * Random.value);

            Vector3 candidate = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rbias;

            // Snap to NavMesh if desired
            if (snapToNavMesh && NavMesh.SamplePosition(candidate, out var hit, navMeshSampleRange, NavMesh.AllAreas))
                candidate = hit.position;

            // Snap to ground height (optional but looks better)
            if (Physics.Raycast(candidate + Vector3.up * 50f, Vector3.down, out var gHit, 200f, groundMask, QueryTriggerInteraction.Ignore))
                candidate.y = gHit.point.y;

            // Check separation
            bool ok = true;
            foreach (var p in pts)
            {
                if ((p - candidate).sqrMagnitude < minSeparation * minSeparation)
                {
                    ok = false;
                    break;
                }
            }
            if (!ok) continue;

            // Also keep inside interest radius (after snaps)
            if ((candidate - center).sqrMagnitude > R * R) continue;

            // Passed all checks—create point
            var pGo = new GameObject($"P{created + 1}");
            pGo.transform.SetParent(container);
            pGo.transform.position = candidate;
            pGo.transform.rotation = Quaternion.identity;

            pts.Add(candidate);
            validTransforms.Add(pGo.transform);
            created++;
        }

        if (created < pointCount)
            Debug.LogWarning($"[AutoPatrolPoints] Only generated {created}/{pointCount} points. Try lowering minSeparation or edgeBias or raising attempts.");

        // Assign to AI
        validTransforms.Sort((a, b) =>
        {
            Vector2 ca = new Vector2(a.position.x - center.x, a.position.z - center.z);
            Vector2 cb = new Vector2(b.position.x - center.x, b.position.z - center.z);
            float aa = Mathf.Atan2(ca.y, ca.x);
            float ab = Mathf.Atan2(cb.y, cb.x);
            return aa.CompareTo(ab);
        });
        ai.patrolPoints = validTransforms.ToArray();
        if (Application.isPlaying && container != null)
        {
            container.SetParent(null);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!ai) ai = GetComponent<ChaserEnemyAI>();
        if (!ai) return;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, ai.interestRadius);

        if (container)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            foreach (Transform c in container)
            {
                Gizmos.DrawSphere(c.position + Vector3.up * 0.25f, 0.35f);
            }
        }
    }
}
