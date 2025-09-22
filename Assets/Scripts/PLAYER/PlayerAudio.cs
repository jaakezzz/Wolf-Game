using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Clips")]
    public AudioClip idlePantLoop;
    public AudioClip attack1Clip;
    public AudioClip attack2Clip;
    public AudioClip hurtClip;
    public AudioClip deathClip;

    [Header("Levels")]
    [Range(0f, 1f)] public float pantVolume = 0.55f;
    [Range(0f, 1f)] public float duckVolume = 0.18f;
    public float duckFade = 0.05f;

    AudioSource loopSrc; // panting loop
    AudioSource sfxSrc;  // one-shots
    Coroutine duckCo;
    bool dead;

    void Awake()
    {
        loopSrc = gameObject.AddComponent<AudioSource>();
        loopSrc.spatialBlend = 1f;
        loopSrc.rolloffMode = AudioRolloffMode.Linear;
        loopSrc.minDistance = 5f; loopSrc.maxDistance = 25f;
        loopSrc.loop = true; loopSrc.playOnAwake = false;

        sfxSrc = gameObject.AddComponent<AudioSource>();
        sfxSrc.spatialBlend = 1f;
        sfxSrc.rolloffMode = AudioRolloffMode.Linear;
        sfxSrc.minDistance = 5f; sfxSrc.maxDistance = 25f;
        sfxSrc.playOnAwake = false;

        var hp = GetComponent<Health>();
        if (hp)
        {
            hp.onDeath.AddListener(OnDeath);
            hp.onHurt.AddListener(PlayHurt);
        }
    }

    void Start()
    {
        StartPantLoop();
    }

    void StartPantLoop()
    {
        if (!idlePantLoop) return;
        loopSrc.clip = idlePantLoop;
        loopSrc.volume = pantVolume;
        if (!loopSrc.isPlaying) loopSrc.Play();
    }

    void StopDuckIfAny()
    {
        if (duckCo != null) StopCoroutine(duckCo);
        duckCo = null;
    }

    // --- Public hooks ---

    public void PlayAttack1() => PlayOneShot(attack1Clip);
    public void PlayAttack2() => PlayOneShot(attack2Clip);
    public void PlayHurt() => PlayOneShot(hurtClip);

    // Call this from GameManager after respawn
    public void OnRespawned()
    {
        dead = false;
        StopDuckIfAny();
        loopSrc.volume = pantVolume;
        StartPantLoop();
    }

    // --- Internals ---

    void PlayOneShot(AudioClip clip)
    {
        if (!clip || dead) return;
        sfxSrc.pitch = Random.Range(0.97f, 1.03f);
        sfxSrc.PlayOneShot(clip);

        float hold = Mathf.Max(clip.length * 0.9f, 0.1f);
        if (duckCo != null) StopCoroutine(duckCo);
        duckCo = StartCoroutine(DuckLoop(hold));
    }

    IEnumerator DuckLoop(float hold)
    {
        float start = loopSrc.volume, t = 0f;
        while (t < duckFade) { t += Time.unscaledDeltaTime; loopSrc.volume = Mathf.Lerp(start, duckVolume, t / duckFade); yield return null; }
        loopSrc.volume = duckVolume;

        yield return new WaitForSeconds(hold);

        t = 0f;
        while (t < duckFade) { t += Time.unscaledDeltaTime; loopSrc.volume = Mathf.Lerp(duckVolume, pantVolume, t / duckFade); yield return null; }
        loopSrc.volume = pantVolume;
        duckCo = null;
    }

    void OnDeath()
    {
        if (dead) return;
        dead = true;
        StopDuckIfAny();
        if (loopSrc.isPlaying) loopSrc.Stop();
        if (deathClip) sfxSrc.PlayOneShot(deathClip);
    }
}
