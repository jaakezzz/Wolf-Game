using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CactusHazard : MonoBehaviour
{
    [Header("Damage")]
    public float damage = 6f;
    public float cooldown = 0.5f;

    [Header("Knockback")]
    public float knockbackForce = 5f;
    public float upwardBoost = 0.5f;

    [Header("SFX")]
    public AudioSource sfx;
    public AudioClip hitClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Skip this many seconds into the clip when playing.")]
    public float hitOffset = 0f;

    float lastHitTime;

    void Awake()
    {
        // Auto-add an AudioSource if missing
        if (!sfx)
        {
            sfx = GetComponent<AudioSource>();
            if (!sfx) sfx = gameObject.AddComponent<AudioSource>();
        }
        sfx.playOnAwake = false;
        sfx.spatialBlend = 1f; // 3D sound
        sfx.volume = sfxVolume;
    }

    // --- TRIGGERS ---
    void OnTriggerEnter(Collider other) { TryHit(other); }
    void OnTriggerStay(Collider other) { TryHit(other); }

    // --- COLLISIONS ---
    void OnCollisionEnter(Collision c) { TryHit(c.collider); }
    void OnCollisionStay(Collision c) { TryHit(c.collider); }

    void TryHit(Collider other)
    {
        if (Time.time < lastHitTime + cooldown) return;

        var hp = other.GetComponentInParent<Health>();
        if (!hp) return;

        lastHitTime = Time.time;
        hp.Damage(damage);

        PlayWithOffset(hitClip, hitOffset);

        // Knockback via PlayerMotor
        var motor = other.GetComponentInParent<PlayerMotor>();
        if (motor)
        {
            Vector3 dir = other.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward; // fallback
            dir.Normalize();
            dir.y += upwardBoost;
            motor.ApplyExternalImpulse(dir * knockbackForce);
        }
    }

    void PlayWithOffset(AudioClip clip, float offset)
    {
        if (!clip || !sfx) return;

        sfx.clip = clip;
        sfx.volume = sfxVolume;
        sfx.time = Mathf.Clamp(offset, 0f, clip.length - 0.01f); // avoid going past end
        sfx.Play();
    }
}
