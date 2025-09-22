using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject hud;                 // gameplay HUD
    public GameObject pauseMenu;           // standard pause panel

    [Header("Special Panels")]
    public GameObject unlockedSpeedCanvas; // optional: “Unlocked Speed” panel

    [Header("Controls")]
    public KeyCode pauseKey = KeyCode.Escape;
    public MonoBehaviour[] disableWhilePaused; // e.g., PlayerMotor, camera look

    bool paused;

    void Start()
    {
        // Start unpaused with HUD on, pause panel off
        SetPaused(false);
        if (pauseMenu) pauseMenu.SetActive(false);
        if (hud) hud.SetActive(true);
        if (unlockedSpeedCanvas) unlockedSpeedCanvas.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
            TogglePause();
    }

    public void TogglePause() => SetPaused(!paused);

    // Primary pause toggler
    public void SetPaused(bool p)
    {
        paused = p;

        // Time & cursor
        Time.timeScale = paused ? 0f : 1f;
        Cursor.visible = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;

        // Panels
        if (hud) hud.SetActive(!paused);
        if (pauseMenu) pauseMenu.SetActive(paused);

        // Enable/disable gameplay scripts
        if (disableWhilePaused != null)
        {
            for (int i = 0; i < disableWhilePaused.Length; i++)
            {
                var m = disableWhilePaused[i];
                if (m) m.enabled = !paused;
            }
        }
    }

    // --- UI Buttons ---
    public void OnResumeButton()
    {
        // Hide the special unlock panel if it’s up
        if (unlockedSpeedCanvas && unlockedSpeedCanvas.activeSelf)
            unlockedSpeedCanvas.SetActive(false);

        SetPaused(false);
    }

    public void OnRestartButton()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnQuitButton()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // ==========================
    //     v SAVE / LOAD v
    // ==========================

    public void OnSaveButton()
    {
        // Gather
        var gm = GameManager.I;
        if (!gm || !gm.player)
        {
            Debug.LogWarning("[Save] No GameManager or Player found.");
            return;
        }

        var t = gm.player.transform;
        var hp = gm.player.GetComponent<Health>();

        var data = new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name,

            px = t.position.x,
            py = t.position.y,
            pz = t.position.z,
            rx = t.rotation.x,
            ry = t.rotation.y,
            rz = t.rotation.z,
            rw = t.rotation.w,

            currentHP = hp ? hp.currentHP : 100f,
            maxHP = hp ? hp.maxHP : 100f,

            score = gm.score,
            lives = TryGetLivesFromGM(gm) // optional helper below
        };

        SaveSystem.Save(data);
    }

    public void OnLoadButton()
    {
        if (!SaveSystem.TryLoad(out var data))
        {
            Debug.LogWarning("[Load] No save file found.");
            return;
        }

        // Always unpause before loading/appplying
        SetPaused(false);

        // Same scene? Apply immediately; otherwise load then apply.
        if (SceneManager.GetActiveScene().name == data.sceneName)
        {
            ApplySaveData(data);
        }
        else
        {
            StartCoroutine(LoadSceneThenApply(data));
        }
    }

    System.Collections.IEnumerator LoadSceneThenApply(SaveData data)
    {
        Time.timeScale = 1f;
        var op = SceneManager.LoadSceneAsync(data.sceneName);
        while (!op.isDone) yield return null;

        // Wait a frame for objects to initialize
        yield return null;
        ApplySaveData(data);
    }

    void ApplySaveData(SaveData data)
    {
        var gm = GameManager.I;
        if (!gm || !gm.player)
        {
            Debug.LogWarning("[Load] No GameManager or Player after scene load.");
            return;
        }

        // Warp player safely (disable CC while teleporting)
        var t = gm.player.transform;
        var cc = gm.player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        t.SetPositionAndRotation(
            new Vector3(data.px, data.py, data.pz),
            new Quaternion(data.rx, data.ry, data.rz, data.rw)
        );

        if (cc) cc.enabled = true;

        // Health
        var hp = gm.player.GetComponent<Health>();
        if (hp)
        {
            hp.maxHP = data.maxHP;
            hp.currentHP = Mathf.Clamp(data.currentHP, 0f, hp.maxHP);
            hp.onHealthChanged?.Invoke(hp.currentHP, hp.maxHP);
        }

        // Score & lives
        gm.score = data.score;
        TrySetLivesOnGM(gm, data.lives);

        // Re-show HUD (in case we loaded from pause)
        if (hud) hud.SetActive(true);
    }

    // ---- Lives helpers (works even if you don't add methods) ----
    int TryGetLivesFromGM(GameManager gm)
    {
        // If you add a method to GameManager:
        // public int GetLives() => currentLives;
        var m = typeof(GameManager).GetMethod("GetLives",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (m != null)
            return (int)m.Invoke(gm, null);

        // Fallback: try to reach a serialized "currentLives" field
        var f = typeof(GameManager).GetField("currentLives",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f != null) return (int)f.GetValue(gm);

        return 0;
    }

    void TrySetLivesOnGM(GameManager gm, int lives)
    {
        // If you add a method to GameManager:
        // public void SetLives(int v) { currentLives = Mathf.Max(0, v); UpdateLivesUI(); }
        var m = typeof(GameManager).GetMethod("SetLives",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (m != null) { m.Invoke(gm, new object[] { lives }); return; }

        // Fallback: write the private field & refresh UI if we can
        var f = typeof(GameManager).GetField("currentLives",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f != null)
        {
            f.SetValue(gm, Mathf.Max(0, lives));
            var ui = typeof(GameManager).GetMethod("UpdateLivesUI",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (ui != null) ui.Invoke(gm, null);
        }
    }
}
