using UnityEngine;

public enum CollectibleType { Heal, Speed, Points, Win }

public class Collectible : MonoBehaviour
{
    public CollectibleType type;

    [Header("Values")]
    public int points = 50;
    public float healAmount = 30f;

    [Header("Effects")]
    public ParticleSystem pickupFX;

    [Header("FX Tint (optional)")]
    public bool tintFXByType = true;
    public Color healColor = new Color(1f, 0.9f, 0f);  // yellow
    public Color speedColor = new Color(0f, 1f, 0f);    // green
    public Color pointsColor = new Color(1f, 0f, 1f);    // magenta
    public Color winColor = new Color(1f, 1f, 1f);    // white

    [Header("SFX")]
    public AudioSource sfx;           // optional template (for spatial settings)
    public AudioClip healClip;
    public AudioClip speedClip;
    public AudioClip pointsClip;
    public AudioClip winClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Unlock Pause (Speed only)")]
    public float pauseDelay = 0.75f;           // realtime seconds before pausing
    public bool pauseOnUnlock = true;          // toggle if you want this behavior

    // --- runtime refs ---
    PauseMenu pauseMenu;                        // found at runtime; works from prefab

    void Awake()
    {
        if (sfx)
        {
            sfx.playOnAwake = false;
            sfx.spatialBlend = 1f; // 3D
            sfx.volume = sfxVolume;
        }

        // Find even if disabled in hierarchy
        if (!pauseMenu) pauseMenu = FindObjectOfType<PauseMenu>(true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var motor = other.GetComponent<PlayerMotor>();
        var hp = other.GetComponent<Health>();

        AudioClip clipToPlay = null;

        switch (type)
        {
            case CollectibleType.Heal:
                if (hp) hp.Heal(healAmount);
                clipToPlay = healClip;
                break;

            case CollectibleType.Speed:
                if (motor) motor.UnlockRun();
                clipToPlay = speedClip;

                // Use PauseMenu directly (no GameManager indirection)
                if (pauseOnUnlock && pauseMenu && pauseMenu.unlockedSpeedCanvas)
                    StartDetachedPauseRoutine(pauseMenu, pauseMenu.unlockedSpeedCanvas, pauseMenu.pauseMenu, pauseDelay);
                break;

            case CollectibleType.Points:
                if (GameManager.I) GameManager.I.AddScore(points);
                clipToPlay = pointsClip;
                break;

            case CollectibleType.Win:
                if (GameManager.I) GameManager.I.OnWin();
                clipToPlay = winClip;
                break;
        }

        // Spawn FX and tint that INSTANCE only
        if (pickupFX)
        {
            var fx = Instantiate(pickupFX, transform.position, Quaternion.identity);
            if (tintFXByType)
                TintFX(fx, GetTypeColor(type));
        }

        // Play sound that survives after this object is destroyed
        PlayDetached(clipToPlay, transform.position, sfx, sfxVolume);

        // Remove the pickup immediately; pause routine runs on a tiny helper object
        Destroy(gameObject);
    }

    // ---- Helper: run pause/show panel even after this pickup is destroyed ----
    void StartDetachedPauseRoutine(PauseMenu pm, GameObject customPanel, GameObject defaultPausePanel, float delay)
    {
        var runnerGO = new GameObject("UnlockPauseRunner");
        var runner = runnerGO.AddComponent<CoroutineRunner>();
        runner.StartCoroutine(PauseAndShow(pm, customPanel, defaultPausePanel, delay));
        // cleanup a bit later
        Destroy(runnerGO, delay + 5f);
    }

    static System.Collections.IEnumerator PauseAndShow(PauseMenu pm, GameObject customPanel, GameObject defaultPausePanel, float delay)
    {
        // Use realtime so it ignores Time.timeScale changes
        float t = 0f;
        while (t < delay)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Pause the game
        pm.SetPaused(true);

        // Hide normal pause menu panel (so only the unlock panel shows)
        if (defaultPausePanel) defaultPausePanel.SetActive(false);

        // Show the custom unlock panel
        if (customPanel) customPanel.SetActive(true);
    }

    // Small component just to host coroutines
    private class CoroutineRunner : MonoBehaviour { }

    // ----- Color helpers -----
    Color GetTypeColor(CollectibleType t)
    {
        switch (t)
        {
            case CollectibleType.Heal: return healColor;
            case CollectibleType.Speed: return speedColor;
            case CollectibleType.Points: return pointsColor;
            case CollectibleType.Win: return winColor;
        }
        return Color.white;
    }

    // Tint the spawned FX instance (all child ParticleSystems & Renderers)
    void TintFX(ParticleSystem root, Color c)
    {
        if (!root) return;

        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var main = ps.main;
            // Preserve alpha from existing startColor if set
            float a = 1f;
            if (main.startColor.mode == ParticleSystemGradientMode.Color)
                a = main.startColor.color.a;
            else if (main.startColor.mode == ParticleSystemGradientMode.TwoColors)
                a = main.startColor.colorMax.a;

            var tinted = new Color(c.r, c.g, c.b, a);
            main.startColor = tinted;
        }

        var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
        foreach (var r in renderers)
        {
            var mat = r.material;
            if (!mat) continue;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }
    }

    // Spawns a tiny audio object so the clip finishes even after the pickup is destroyed.
    static void PlayDetached(AudioClip clip, Vector3 pos, AudioSource template, float volume)
    {
        if (!clip) return;

        if (template)
        {
            var go = new GameObject("PickupSFX");
            go.transform.position = pos;
            var au = go.AddComponent<AudioSource>();

            // Copy relevant spatial settings
            au.spatialBlend = template.spatialBlend;
            au.rolloffMode = template.rolloffMode;
            au.minDistance = template.minDistance;
            au.maxDistance = template.maxDistance;
            au.outputAudioMixerGroup = template.outputAudioMixerGroup;
            au.dopplerLevel = template.dopplerLevel;
            au.spread = template.spread;

            au.clip = clip;
            au.volume = volume;
            au.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, pos, volume);
        }
    }
}
