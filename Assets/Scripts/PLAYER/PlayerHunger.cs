using UnityEngine;

[RequireComponent(typeof(Health))]
public class PlayerHunger : MonoBehaviour
{
    [Header("Stats")]
    public float maxHunger = 100f;
    public float currentHunger;
    public float decayPerSecond = 0.15f; // How much hunger is lost per second

    [Header("Refs")]
    public SitController sitController; // Drag the SitController here
    Health health;
    bool isDead;

    void Awake()
    {
        health = GetComponent<Health>();
        if (!sitController) sitController = GetComponent<SitController>();

        currentHunger = maxHunger;
        health.onDeath.AddListener(() => isDead = true);
    }

    void Update()
    {
        if (isDead) return;

        // Pause hunger drain if the player is actively sitting
        if (sitController != null && sitController.IsSitting()) return;

        // Drain hunger over time
        currentHunger -= decayPerSecond * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);

        // Kill the player if they starve
        if (currentHunger <= 0)
        {
            health.Damage(health.maxHP); // Instant kill
        }
    }

    // Called by Food and Biting
    public void AddHunger(float amount)
    {
        if (isDead) return;
        currentHunger = Mathf.Clamp(currentHunger + amount, 0, maxHunger);
    }

    // Called by GameManager upon respawn
    public void ApplyRespawnPenalty(int livesLeft)
    {
        isDead = false;

        // 2 lives left (1st death) -> Minimum 75% hunger
        if (livesLeft == 2 && currentHunger < maxHunger * 0.75f)
        {
            currentHunger = maxHunger * 0.75f;
        }
        // 1 life left (2nd death) -> Minimum 50% hunger
        else if (livesLeft == 1 && currentHunger < maxHunger * 0.50f)
        {
            currentHunger = maxHunger * 0.50f;
        }
    }
}