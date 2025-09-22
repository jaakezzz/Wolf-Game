using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;             // only needed if you wire a TMP lives label
using UnityEngine.UI;    // only needed if you wire a legacy Text lives label
using System.Reflection;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    [Header("Player / Camera")]
    public Transform playerCamera;   // (optional) Main Camera
    public Transform cameraAnchor;   // (optional) Player/CameraAnchor
    public Transform playerSpawn;
    public GameObject player;

    [Header("Respawn")]
    public float respawnDelay = 3f;      // realtime seconds

    [Header("Lives")]
    public int startingLives = 3;
    [SerializeField] int currentLives;
    public TMP_Text livesTMP;            // optional: assign if using TMP
    public Text livesText;               // optional: assign if using legacy Text
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

    // Keep simple lives API (used by PauseMenu save/load helpers)
    public int GetLives() => currentLives;
    public void SetLives(int v) { currentLives = Mathf.Max(0, v); UpdateLivesUI(); }

    void Awake()
    {
        if (I == null) I = this;
        else { Destroy(gameObject); return; }

        currentLives = Mathf.Max(0, startingLives);
        UpdateLivesUI();
        Time.timeScale = 1f; // ensure not paused from previous scene
    }

    public void AddScore(int v) { score += v; }

    // Called by PlayerDeathHandler after the death animation delay
    public void OnPlayerDied()
    {
        if (isGameOver || isRespawning) return;

        currentLives = Mathf.Max(0, currentLives - 1);
        UpdateLivesUI();

        if (currentLives > 0)
            StartCoroutine(RespawnPlayer());
        else
            ShowGameOver();
    }

    IEnumerator RespawnPlayer()
    {
        isRespawning = true;

        // small realtime delay before respawn
        yield return new WaitForSecondsRealtime(respawnDelay);

        if (!player) { isRespawning = false; yield break; }

        // disable CC to safely warp
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        player.transform.SetPositionAndRotation(playerSpawn.position, playerSpawn.rotation);

        if (cc) cc.enabled = true;

        // restore HP (support both Revive() and manual reset)
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

        // clear death-disabled components & colliders
        var death = player.GetComponent<PlayerDeathHandler>();
        if (death) death.SetDeadState(false);

        player.SetActive(true);

        player.GetComponent<PlayerAudio>()?.OnRespawned();

        // Safety: explicitly re-enable core controls (if you’re using these)
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

        if (music && loseJingle)
            music.PlayOneShot(loseJingle);

        // Freeze everything
        Time.timeScale = 0f;

        // Hard-disable player control as a safeguard
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
        SceneManager.LoadScene("MainMenu"); // make sure it's in Build Settings
    }

    public void Quit() { Application.Quit(); }

    void UpdateLivesUI()
    {
        if (livesTMP) livesTMP.text = string.Format(livesFormat, currentLives);
        if (livesText) livesText.text = string.Format(livesFormat, currentLives);
    }

    // Optional public API if you add pickups, checkpoints, etc.
    public void GrantExtraLife(int amount = 1)
    {
        currentLives += Mathf.Max(0, amount);
        UpdateLivesUI();
    }
}
