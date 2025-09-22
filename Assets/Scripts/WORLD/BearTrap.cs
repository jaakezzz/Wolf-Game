using System.Collections;
using UnityEngine;

public class BearTrap : MonoBehaviour
{
    [Header("Models")]
    public GameObject openModel;     // trap1 (open)
    public GameObject closedModel;   // trap1 active (closed)

    [Header("Trigger")]
    public Collider triggerArea;     // leave null to use the root collider
    public LayerMask triggerLayers;  // set to Player layer
    public float damage = 35f;

    [Header("Lifecycle")]
    public bool destroyAfterTrigger = false;
    public bool autoReset = false;
    public float resetDelay = 6f;    // re-open after this many seconds

    [Header("SFX")]
    public AudioSource sfx;
    public AudioClip openClip;       // plays when the trap opens
    public AudioClip closeClip;      // plays when the trap closes
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Play the open sound the very first time (on scene start).")]
    public bool playOpenOnStart = false;

    bool armed = true;
    bool busy;

    void Awake()
    {
        // Ensure we have an AudioSource
        if (!sfx)
        {
            sfx = GetComponent<AudioSource>();
            if (!sfx) sfx = gameObject.AddComponent<AudioSource>();
        }
        sfx.playOnAwake = false;
        sfx.spatialBlend = 1f; // 3D
        sfx.volume = sfxVolume;
    }

    void Reset()
    {
        triggerArea = GetComponent<Collider>();
        if (triggerArea) triggerArea.isTrigger = true;
        SetVisual(open: true);
    }

    void OnValidate()
    {
        if (!triggerArea) triggerArea = GetComponent<Collider>();
        SetVisual(armed);
        if (sfx) sfx.volume = sfxVolume;
    }

    void Start()
    {
        if (!triggerArea) triggerArea = GetComponent<Collider>();
        Arm(true, fromResetOrExplicit: playOpenOnStart);
    }

    void Arm(bool state, bool fromResetOrExplicit = false)
    {
        armed = state;
        busy = false;

        if (triggerArea) triggerArea.enabled = state;
        SetVisual(open: state);

        if (fromResetOrExplicit)
        {
            if (state) PlayOneShotSafe(openClip);
            else PlayOneShotSafe(closeClip);
        }
    }

    void SetVisual(bool open)
    {
        if (openModel) openModel.SetActive(open);
        if (closedModel) closedModel.SetActive(!open);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!armed || busy) return;
        if ((triggerLayers.value & (1 << other.gameObject.layer)) == 0) return;

        StartCoroutine(Snap(other));
    }

    IEnumerator Snap(Collider victim)
    {
        busy = true;
        armed = false;

        SetVisual(open: false);
        PlayOneShotSafe(closeClip);

        var hp = victim.GetComponentInParent<Health>();
        if (hp) hp.Damage(damage);

        var motor = victim.GetComponentInParent<PlayerMotor>();
        if (motor) motor.Stun(1f);

        if (destroyAfterTrigger)
        {
            yield return null;
            Destroy(gameObject);
            yield break;
        }

        if (autoReset)
        {
            if (triggerArea) triggerArea.enabled = false;
            yield return new WaitForSeconds(resetDelay);
            Arm(true, fromResetOrExplicit: true);
        }
        else
        {
            if (triggerArea) triggerArea.enabled = false;
        }
    }

    void PlayOneShotSafe(AudioClip clip)
    {
        if (clip && sfx)
        {
            sfx.volume = sfxVolume;
            sfx.PlayOneShot(clip, sfxVolume);
        }
    }
}
