using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class TintParticlesOnSpawn : MonoBehaviour
{
    [ColorUsage(false, true)] public Color color = Color.white;
    public bool tintStartColor = true;
    public bool tintRendererMaterials = true;
    [Range(0.1f, 4f)] public float materialIntensity = 1f;

    void Awake()
    {
        // Tint start color
        if (tintStartColor)
        {
            var ps = GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }

        // Tint materials (ParticleSystemRenderer + trail)
        if (tintRendererMaterials)
        {
            var rends = GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in rends)
            {
                // duplicate materials so we don't edit shared material
                if (r.material)
                {
                    r.material = new Material(r.material);
                    if (r.material.HasProperty("_BaseColor"))
                        r.material.SetColor("_BaseColor", color * materialIntensity);
                    if (r.material.HasProperty("_Color"))
                        r.material.color = color * materialIntensity;
                }

                if (r.trailMaterial)
                {
                    r.trailMaterial = new Material(r.trailMaterial);
                    if (r.trailMaterial.HasProperty("_BaseColor"))
                        r.trailMaterial.SetColor("_BaseColor", color * materialIntensity);
                    if (r.trailMaterial.HasProperty("_Color"))
                        r.trailMaterial.color = color * materialIntensity;
                }
            }
        }
    }
}
