using UnityEngine;

[RequireComponent(typeof(Health))]
public class MinotaurAudio : MonoBehaviour
{
    [Header("Clips")]
    public AudioClip[] attackGrunts;   // size 2, random pick
    public AudioClip hurtClip;         // single
    public AudioClip[] deathClips;     // size 2, random pick

    [Header("Playback")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public Vector2 pitchJitter = new Vector2(0.97f, 1.03f);
    public float attackRateLimit = 0.15f;   // don’t spam if multiple triggers fire

    AudioSource src;
    Health hp;
    float lastAttackTime;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 5f; src.maxDistance = 30f;
        src.playOnAwake = false;

        hp = GetComponent<Health>();
        hp.onHurt.AddListener(PlayHurt);
        hp.onDeath.AddListener(PlayDeath);
    }

    void OnDestroy()
    {
        if (hp != null)
        {
            hp.onHurt.RemoveListener(PlayHurt);
            hp.onDeath.RemoveListener(PlayDeath);
        }
    }

    public void PlayAttack()
    {
        if (Time.time < lastAttackTime + attackRateLimit) return;
        lastAttackTime = Time.time;
        PlayRandom(attackGrunts);
    }

    public void PlayHurt() { PlayOne(hurtClip); }
    public void PlayDeath() { PlayRandom(deathClips); }

    // --- helpers ---
    void PlayRandom(AudioClip[] set)
    {
        if (set == null || set.Length == 0) return;
        var clip = set[Random.Range(0, set.Length)];
        PlayOne(clip);
    }

    void PlayOne(AudioClip clip)
    {
        if (!clip) return;
        src.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
        src.PlayOneShot(clip, sfxVolume);
    }
}
