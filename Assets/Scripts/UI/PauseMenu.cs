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

        // As soon as the new scene wakes up, ask the World Memory if we are supposed to be loading a save
        if (WorldStateManager.I != null && WorldStateManager.I.pendingLoad != null)
        {
            // Apply all the positions, health, and stats
            ApplySaveData(WorldStateManager.I.pendingLoad);

            // Clear the backpack so we don't accidentally load it again later!
            WorldStateManager.I.pendingLoad = null;
        }
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
        var gm = GameManager.I;
        if (!gm || !gm.player) return;

        var t = gm.player.transform;
        var hp = gm.player.GetComponent<Health>();

        // Grab the missing player stats
        var hungerScript = gm.player.GetComponent<PlayerHunger>();
        var motorScript = gm.player.GetComponent<PlayerMotor>();

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
            lives = TryGetLivesFromGM(gm),

            currentHunger = hungerScript ? hungerScript.currentHunger : 100f,
            runUnlocked = motorScript ? motorScript.runUnlocked : true,
            jumpUnlocked = motorScript ? motorScript.jumpUnlocked : true,

            worldSeed = WorldStateManager.I.worldSeed,
            destroyedEntities = new System.Collections.Generic.List<string>(WorldStateManager.I.destroyedEntities)
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

        SetPaused(false);

        // Hand the data to the World Brain so it survives the scene wipe
        if (WorldStateManager.I != null)
        {
            WorldStateManager.I.worldSeed = data.worldSeed;
            WorldStateManager.I.destroyedEntities = new System.Collections.Generic.List<string>(data.destroyedEntities);

            // Put the save file in the backpack
            WorldStateManager.I.pendingLoad = data;
        }

        // Just load the scene directly. No more fragile coroutines!
        Time.timeScale = 1f;
        SceneManager.LoadScene(data.sceneName);
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

        // Player-specific stats (hunger, run/jump unlocks)
        var hungerScript = gm.player.GetComponent<PlayerHunger>();
        if (hungerScript) hungerScript.currentHunger = data.currentHunger;

        var motorScript = gm.player.GetComponent<PlayerMotor>();
        if (motorScript)
        {
            motorScript.runUnlocked = data.runUnlocked;
            motorScript.jumpUnlocked = data.jumpUnlocked;
        }

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
