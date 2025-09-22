using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Reflection;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    [Header("Player / Camera")]
    public Transform playerCamera;
    public Transform cameraAnchor;
    public Transform playerSpawn;
    public GameObject player;

    [Header("Respawn")]
    public float respawnDelay = 3f;

    [Header("Lives")]
    public int startingLives = 3;
    [SerializeField] int currentLives;
    public TMP_Text livesTMP;
    public Text livesText;
    public string livesFormat = "Lives: {0}";

    [Header("UI")]
    public GameObject hud;
    public GameObject gameOverUI;
    public GameObject winUI;

    [Header("Audio")]
    public AudioSource music;
    public AudioClip winJingle;
    public AudioClip loseJingle;

    public int score;

    bool isGameOver;
    bool isRespawning;

    [Header("Pause/Unlock UI")]
    public MonoBehaviour pauseController;   // UIController
    public GameObject defaultPausePanel;
    public GameObject unlockedSpeedCanvas;

    void Awake()
    {
        if (I == null) I = this;
        else { Destroy(gameObject); return; }

        currentLives = Mathf.Max(0, startingLives);
        UpdateLivesUI();
        Time.timeScale = 1f;
    }

    public void AddScore(int v) { score += v; }

    public void OnPlayerDied()
    {
        if (isGameOver || isRespawning) return;

        currentLives = Mathf.Max(0, currentLives - 1);
        UpdateLivesUI();

        if (currentLives > 0) StartCoroutine(RespawnPlayer());
        else ShowGameOver();
    }

    IEnumerator RespawnPlayer()
    {
        isRespawning = true;
        yield return new WaitForSecondsRealtime(respawnDelay);

        if (!player) { isRespawning = false; yield break; }

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        player.transform.SetPositionAndRotation(playerSpawn.position, playerSpawn.rotation);
        if (cc) cc.enabled = true;

        var hp = player.GetComponent<Health>();
        if (hp)
        {
            var revive = hp.GetType().GetMethod("Revive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (revive != null) revive.Invoke(hp, null);
            else
            {
                hp.currentHP = hp.maxHP;
                hp.onHealthChanged?.Invoke(hp.currentHP, hp.maxHP);
            }
        }

        var death = player.GetComponent<PlayerDeathHandler>();
        if (death) death.SetDeadState(false);

        player.SetActive(true);
        player.GetComponent<PlayerAudio>()?.OnRespawned();

        var controls = player.GetComponents<MonoBehaviour>();
        foreach (var c in controls)
        {
            if (c is PlayerMotor || c is PlayerCombat) c.enabled = true;
        }

        isRespawning = false;
    }

    void ShowGameOver()
    {
        isGameOver = true;

        if (hud) hud.SetActive(false);
        if (gameOverUI) gameOverUI.SetActive(true);

        if (music && loseJingle) music.PlayOneShot(loseJingle);

        Time.timeScale = 0f;

        if (player)
        {
            var death = player.GetComponent<PlayerDeathHandler>();
            if (death) death.SetDeadState(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OnWin()
    {
        if (isGameOver) return;

        if (winUI) winUI.SetActive(true);
        if (music && winJingle) music.PlayOneShot(winJingle);

        Time.timeScale = 0f;

        if (player)
        {
            var death = player.GetComponent<PlayerDeathHandler>();
            if (death) death.SetDeadState(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void Quit() { Application.Quit(); }

    void UpdateLivesUI()
    {
        if (livesTMP) livesTMP.text = string.Format(livesFormat, currentLives);
        if (livesText) livesText.text = string.Format(livesFormat, currentLives);
    }

    public void GrantExtraLife(int amount = 1)
    {
        currentLives += Mathf.Max(0, amount);
        UpdateLivesUI();
    }

    // === Speed unlock popup ===
    public void ShowSpeedUnlockPopup(float delay = 0.75f)
    {
        StartCoroutine(ShowUnlockPopupCo(delay));
    }

    IEnumerator ShowUnlockPopupCo(float delay)
    {
        float t = 0f;
        while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }

        SetPaused(true);

        if (defaultPausePanel) defaultPausePanel.SetActive(false);
        if (unlockedSpeedCanvas) unlockedSpeedCanvas.SetActive(true);
        else Debug.LogWarning("[GameManager] Unlocked Speed canvas reference is missing.");
    }

    public void SetPaused(bool paused)
    {
        if (pauseController)
        {
            var mi = pauseController.GetType().GetMethod("SetPaused", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null) { mi.Invoke(pauseController, new object[] { paused }); return; }
        }

        Time.timeScale = paused ? 0f : 1f;
        Cursor.visible = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;

        if (hud) hud.SetActive(!paused);
        if (defaultPausePanel) defaultPausePanel.SetActive(paused);
    }

    public void ResumeFromUnlockPanel()
    {
        if (unlockedSpeedCanvas) unlockedSpeedCanvas.SetActive(false);
        if (defaultPausePanel) defaultPausePanel.SetActive(false);
        SetPaused(false);
    }
}
