using System.Collections;
using UnityEngine;

public class GhostDissolve : MonoBehaviour
{
    [Tooltip("SkinnedMeshRenderers or MeshRenderers that use the dissolve shader")]
    public Renderer[] renderers;

    [Tooltip("Seconds to fade in/out")]
    public float fadeTime = 0.35f;

    [Tooltip("Shader property name for dissolve cutoff")]
    public string dissolveProperty = "_Dissolve";

    [Tooltip("Value when fully visible (sample pack used 1)")]
    public float visibleValue = 1f;

    [Tooltip("Value when fully hidden (sample pack used 0)")]
    public float hiddenValue = 0f;

    Coroutine fadeCo;
    float current;

    void Awake()
    {
        // If not assigned, grab all child renderers
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        // Ensure each renderer has its own material instance
        foreach (var r in renderers)
            if (r) _Set(r, visibleValue);
        current = visibleValue;
    }

    public void SetVisible(bool visible)
    {
        float target = visible ? visibleValue : hiddenValue;
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeTo(target));
    }

    // Smoothly fades the material's dissolve cutoff without blocking the main thread
    IEnumerator FadeTo(float target)
    {
        float start = current;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            current = Mathf.Lerp(start, target, t / fadeTime);

            // Apply the updated value to the custom shader property
            foreach (var r in renderers) if (r) _Set(r, current);
            yield return null;
        }
        current = target;
        foreach (var r in renderers) if (r) _Set(r, current);
        fadeCo = null;
    }

    void _Set(Renderer r, float v)
    {
        // Using .material to get a unique instance per renderer (ok for enemies)
        var m = r.material;
        if (m && m.HasProperty(dissolveProperty))
            m.SetFloat(dissolveProperty, v);
    }
}
