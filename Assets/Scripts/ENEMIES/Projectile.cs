using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Projectile : MonoBehaviour
{
    [Header("Stats")]
    public float speed = 12f;
    public float life = 5f;
    public float damage = 15f;
    public LayerMask hitMask;

    [Header("Audio")]
    public AudioClip shootSound;
    [Range(0f, 5f)] public float shootStartOffset = 0f; // seconds into clip
    public AudioClip hitSound;
    [Range(0f, 5f)] public float hitStartOffset = 0f;

    AudioSource audioSrc;

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 1f;   // 3D sound
    }

    void Start()
    {
        Destroy(gameObject, life);

        if (shootSound)
            PlayClipWithOffset(audioSrc, shootSound, shootStartOffset);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        var hp = other.GetComponentInParent<Health>();
        if (hp)
        {
            hp.Damage(damage);
            PlayClipAtPointWithOffset(hitSound, transform.position, hitStartOffset);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & hitMask) != 0)
        {
            PlayClipAtPointWithOffset(hitSound, transform.position, hitStartOffset);
            Destroy(gameObject);
        }
    }

    // --- Helpers ---
    void PlayClipWithOffset(AudioSource src, AudioClip clip, float offsetSec)
    {
        if (!clip) return;

        src.clip = clip;
        offsetSec = Mathf.Clamp(offsetSec, 0f, clip.length);
        src.time = offsetSec;
        src.Play();
    }

    void PlayClipAtPointWithOffset(AudioClip clip, Vector3 pos, float offsetSec)
    {
        if (!clip) return;

        GameObject temp = new GameObject("TempAudio");
        temp.transform.position = pos;
        var tempSrc = temp.AddComponent<AudioSource>();
        tempSrc.spatialBlend = 1f;
        tempSrc.clip = clip;
        offsetSec = Mathf.Clamp(offsetSec, 0f, clip.length);
        tempSrc.time = offsetSec;
        tempSrc.Play();
        Destroy(temp, clip.length - offsetSec + 0.1f);
    }
}
