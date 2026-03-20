using System;
using UnityEngine;
using UnityEngine.UI;       // Image, legacy Text
using TMPro;                // TMP_Text

public class HUD : MonoBehaviour
{
    [Header("=== HEALTH SYSTEM ===")]
    [Space(5)]
    [Header("Health Refs")]
    public Health playerHealth;            // drag PlayerRoot Health (auto-found if empty)
    public TMP_Text healthTmpText;         // optional
    public Text healthUiText;              // optional

    [Header("Health Bar (Filled Image)")]
    public Image healthBarFill;            // set an Image whose Type = Filled (Horizontal)
    public bool healthSmoothBar = true;
    public float healthBarLerpSpeed = 10f;

    [Header("Health Formatting / Colors")]
    public string healthFormat = "Health: {0}/{1}";
    public Color healthNormalColor = Color.white;              // text color when healthy
    public Color healthLowColor = new Color(1f, 0.35f, 0.35f); // text color when low
    [Range(0f, 1f)] public float healthLowThreshold = 0.25f;

    [Header("Optional Health Bar Gradient")]
    public bool healthUseBarGradient = true;
    public Gradient healthBarGradient = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(new Color(0.9f, 0.2f, 0.2f), 0f),  // low = red
            new GradientColorKey(new Color(0.2f, 0.8f, 0.25f), 1f), // high = green
        },
        alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
    };

    float healthTargetFill = 1f;

    [Space(15)]
    [Header("=== HUNGER SYSTEM ===")]
    [Space(5)]
    [Header("Hunger Refs")]
    public PlayerHunger playerHunger;      // drag PlayerRoot PlayerHunger (auto-found if empty)
    public TMP_Text hungerTmpText;         // optional
    public Text hungerUiText;              // optional

    [Header("Hunger Bar (Filled Image)")]
    public Image hungerBarFill;            // set an Image whose Type = Filled (Horizontal)
    public bool hungerSmoothBar = true;
    public float hungerBarLerpSpeed = 10f;

    [Header("Hunger Formatting / Colors")]
    public string hungerFormat = "Hunger: {0}/{1}";
    public Color hungerNormalColor = Color.white;              // text color when healthy
    public Color hungerLowColor = new Color(1f, 0.5f, 0f);     // text color when low (orange)
    [Range(0f, 1f)] public float hungerLowThreshold = 0.25f;

    [Header("Optional Hunger Bar Gradient")]
    public bool hungerUseBarGradient = true;
    public Gradient hungerBarGradient = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(new Color(0f, 0f, 0f), 0f),    // low = black
            new GradientColorKey(new Color(0f, 0.5f, 1f), 1f),    // high = blue
        },
        alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
    };

    float hungerTargetFill = 1f;


    void Awake()
    {
        // Auto-find player references if empty
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p)
        {
            if (!playerHealth) playerHealth = p.GetComponent<Health>();
            if (!playerHunger) playerHunger = p.GetComponent<PlayerHunger>();
        }
    }

    void OnEnable()
    {
        // Health uses an event because it only changes occasionally
        if (playerHealth)
        {
            playerHealth.onHealthChanged.AddListener(OnHealthChanged);
            OnHealthChanged(playerHealth.currentHP, playerHealth.maxHP); // initial
        }
    }

    void OnDisable()
    {
        if (playerHealth)
            playerHealth.onHealthChanged.RemoveListener(OnHealthChanged);
    }

    void Update()
    {
        UpdateHealthBar();
        UpdateHungerSystem();
    }

    // --- HEALTH LOGIC ---
    void OnHealthChanged(float current, float max)
    {
        int c = Mathf.CeilToInt(current);
        int m = Mathf.CeilToInt(max);

        // ---- text ----
        string s = FormatString(healthFormat, c, m);
        if (healthTmpText) healthTmpText.text = s;
        if (healthUiText) healthUiText.text = s;

        float pct = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
        var textColor = (pct <= healthLowThreshold) ? healthLowColor : healthNormalColor;
        if (healthTmpText) healthTmpText.color = textColor;
        if (healthUiText) healthUiText.color = textColor;

        // ---- bar ----
        healthTargetFill = pct;
        if (!healthSmoothBar && healthBarFill) healthBarFill.fillAmount = healthTargetFill;
        if (healthUseBarGradient && healthBarFill) healthBarFill.color = healthBarGradient.Evaluate(healthTargetFill);
    }

    void UpdateHealthBar()
    {
        if (!healthBarFill) return;

        if (healthSmoothBar)
        {
            healthBarFill.fillAmount = Mathf.Lerp(
                healthBarFill.fillAmount, healthTargetFill,
                1f - Mathf.Exp(-healthBarLerpSpeed * Time.unscaledDeltaTime)
            );
        }
        else
        {
            healthBarFill.fillAmount = healthTargetFill;
        }

        if (healthUseBarGradient)
            healthBarFill.color = healthBarGradient.Evaluate(healthBarFill.fillAmount);
    }

    // --- HUNGER LOGIC ---
    void UpdateHungerSystem()
    {
        // Hunger drains every frame, so we process its text and bar target right here in Update
        if (!playerHunger) return;

        // 1. Calculate Target Fill
        hungerTargetFill = (playerHunger.maxHunger > 0f) ? Mathf.Clamp01(playerHunger.currentHunger / playerHunger.maxHunger) : 0f;

        // 2. Format Text
        int c = Mathf.CeilToInt(playerHunger.currentHunger);
        int m = Mathf.CeilToInt(playerHunger.maxHunger);
        string s = FormatString(hungerFormat, c, m);

        if (hungerTmpText) hungerTmpText.text = s;
        if (hungerUiText) hungerUiText.text = s;

        // 3. Color Text based on threshold
        var textColor = (hungerTargetFill <= hungerLowThreshold) ? hungerLowColor : hungerNormalColor;
        if (hungerTmpText) hungerTmpText.color = textColor;
        if (hungerUiText) hungerUiText.color = textColor;

        // 4. Update the actual UI Bar
        if (!hungerBarFill) return;

        if (hungerSmoothBar)
        {
            hungerBarFill.fillAmount = Mathf.Lerp(
                hungerBarFill.fillAmount, hungerTargetFill,
                1f - Mathf.Exp(-hungerBarLerpSpeed * Time.unscaledDeltaTime)
            );
        }
        else
        {
            hungerBarFill.fillAmount = hungerTargetFill;
        }

        if (hungerUseBarGradient)
            hungerBarFill.color = hungerBarGradient.Evaluate(hungerBarFill.fillAmount);
    }

    // --- HELPERS ---
    // Handles both "HP {0}" and "HP {0}/{1}" (and avoids FormatException)
    static string FormatString(string fmt, int current, int max)
    {
        // Fast path: if it references {1}, pass both; otherwise pass one.
        if (!string.IsNullOrEmpty(fmt) && fmt.IndexOf("{1}", StringComparison.Ordinal) >= 0)
            return string.Format(fmt, current, max);

        // Fallback to single-arg; if someone typed weird placeholders, catch it.
        try { return string.Format(fmt, current); }
        catch { return $"{current}/{max}"; }
    }
}