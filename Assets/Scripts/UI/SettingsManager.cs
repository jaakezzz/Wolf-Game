using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for modern TextMeshPro UI elements

// -----------------------------------------------------------------------------
// Handles the Main Menu settings options. Links UI elements (Dropdowns/Toggles) 
// directly to Unity's internal rendering engine to adjust performance on the fly.
// -----------------------------------------------------------------------------
public class SettingsManager : MonoBehaviour
{
    // ----------------------------------------
    // UI References
    // ----------------------------------------
    [Header("UI Components")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider lodBiasSlider;
    public Toggle fpsToggle;
    public Toggle VSyncToggle;
    public TMP_Dropdown fpsTargetDropdown;
    public GameObject fpsCounterObject; // Drag the GameObject holding the FPSDisplay script here

    // Stores the list of resolutions specific to the player's physical monitor
    private Resolution[] resolutions;

    // -----------------------------------------------------------------------------
    // Initialization
    // -----------------------------------------------------------------------------
    void Start()
    {
        // Setup the Resolution Dropdown
        SetupResolutionDropdown();

        // Now that the UI exists, pull the saved data from the hard drive
        LoadSettings();
    }

    // -----------------------------------------------------------------------------
    // Dynamically builds a list of resolutions based on the player's monitor
    // -----------------------------------------------------------------------------
    void SetupResolutionDropdown()
    {
        // Grab every resolution the player's monitor is physically capable of displaying
        resolutions = Screen.resolutions;

        // Clear out the default "Option A, Option B" placeholder text
        resolutionDropdown.ClearOptions();

        // Create a blank list of strings to hold our newly formatted text
        List<string> options = new List<string>();

        int currentResolutionIndex = 0;

        // Loop through the monitor's array of resolutions
        for (int i = 0; i < resolutions.Length; i++)
        {
            // Format the text to look nice (e.g., "1920 x 1080")
            string option = resolutions[i].width + " x " + resolutions[i].height;
            options.Add(option);

            // Check if this specific resolution in the loop matches what the game is currently running at
            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                // If it matches, save this index number so we can highlight it in the dropdown
                currentResolutionIndex = i;
            }
        }

        // Push the finished list of text into the actual UI dropdown
        resolutionDropdown.AddOptions(options);

        // Tell the dropdown to display the resolution we are currently using
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    // -----------------------------------------------------------------------------
    // PlayerPrefs: Loading Data
    // -----------------------------------------------------------------------------
    void LoadSettings()
    {
        // 1. Read all saved values from the hard drive first
        int savedQuality = PlayerPrefs.GetInt("QualitySetting", QualitySettings.GetQualityLevel());
        int savedFullscreen = PlayerPrefs.GetInt("FullscreenSetting", Screen.fullScreen ? 1 : 0);
        float savedLOD = PlayerPrefs.GetFloat("LODBiasSetting", QualitySettings.lodBias);
        int savedFPSToggle = PlayerPrefs.GetInt("FPSToggleSetting", 0);
        int savedVSync = PlayerPrefs.GetInt("VSyncSetting", 1); // Default to 1 (On)
        int savedFPSTargetIndex = PlayerPrefs.GetInt("FPSTargetSetting", 1);

        // 2. Update the UI visuals WITHOUT triggering their code events!
        qualityDropdown.SetValueWithoutNotify(savedQuality);
        fullscreenToggle.SetIsOnWithoutNotify(savedFullscreen == 1);
        lodBiasSlider.SetValueWithoutNotify(savedLOD);
        fpsToggle.SetIsOnWithoutNotify(savedFPSToggle == 1);
        VSyncToggle.SetIsOnWithoutNotify(savedVSync == 1);
        fpsTargetDropdown.SetValueWithoutNotify(savedFPSTargetIndex);

        // 3. Now safely apply the actual logic to the engine
        SetQuality(savedQuality);
        SetFullscreen(savedFullscreen == 1);
        SetLODBias(savedLOD);
        SetFPSDisplay(savedFPSToggle == 1);
        SetVSync(savedVSync == 1);

        // Only enforce the manual FPS limit if VSync is turned off
        if (savedVSync == 0)
        {
            SetFPSTarget(savedFPSTargetIndex);
        }
    }

    // -----------------------------------------------------------------------------
    // Public Methods (These get triggered by the UI elements)
    // -----------------------------------------------------------------------------

    /// <summary>
    /// Triggered by the Quality Dropdown.
    /// Changes global settings (shadows, anti-aliasing, textures) all at once.
    /// </summary>
    public void SetQuality(int qualityIndex)
    {
        // Unity automatically passes the Dropdown's index number (0 = Low, 1 = Medium, etc.)
        QualitySettings.SetQualityLevel(qualityIndex);

        // Changing quality presets often overwrites the LOD bias, so we force it back to what the slider says
        QualitySettings.lodBias = lodBiasSlider.value;

        // Force Unity to respect the player's VSync choice after changing presets
        SetVSync(VSyncToggle.isOn);

        // Save the new choice
        PlayerPrefs.SetInt("QualitySetting", qualityIndex);
        PlayerPrefs.Save(); // Forces Unity to write to the hard drive immediately
    }

    /// <summary>
    /// Triggered by the Resolution Dropdown.
    /// </summary>
    public void SetResolution(int resolutionIndex)
    {
        // Look up the exact width and height from the array we built in Start()
        Resolution resolution = resolutions[resolutionIndex];

        // Apply it. The third parameter checks our toggle to decide if it should be windowed or fullscreen.
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);

        PlayerPrefs.SetInt("ResolutionSetting", resolutionIndex);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Triggered by the Fullscreen Toggle checkbox.
    /// </summary>
    public void SetFullscreen(bool isFullscreen)
    {
        // Flips the game between Windowed and Fullscreen mode
        Screen.fullScreen = isFullscreen;

        // Convert the boolean to an int before saving
        PlayerPrefs.SetInt("FullscreenSetting", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Triggered by the LOD Bias Slider.
    /// </summary>
    public void SetLODBias(float biasValue)
    {
        // 1.0 is standard. 2.0 pushes transitions far away (laggy). 0.5 pulls them in close (fast).
        QualitySettings.lodBias = biasValue;

        PlayerPrefs.SetFloat("LODBiasSetting", biasValue);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Turns the visual FPS text on or off.
    /// </summary>
    public void SetFPSDisplay(bool isShowing)
    {
        if (fpsCounterObject != null)
        {
            fpsCounterObject.SetActive(isShowing);
        }

        PlayerPrefs.SetInt("FPSToggleSetting", isShowing ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Caps the engine's maximum framerate.
    /// IMPORTANT: If VSync is turned on in Quality Settings, Unity will ignore this!
    /// </summary>
    public void SetFPSTarget(int targetIndex)
    {
        switch (targetIndex)
        {
            case 0: Application.targetFrameRate = 30; break;
            case 1: Application.targetFrameRate = 60; break;
            case 2: Application.targetFrameRate = 120; break;
            case 3: Application.targetFrameRate = -1; break; // -1 means Unlimited
        }

        // Optional: If you want to guarantee the framerate cap works, you must force VSync off in code
        QualitySettings.vSyncCount = 0;

        PlayerPrefs.SetInt("FPSTargetSetting", targetIndex);
        PlayerPrefs.Save();
    }

    public void SetVSync(bool isVSyncOn)
    {
        if (isVSyncOn)
        {
            // Turn VSync on. Unity will now lock to the monitor's refresh rate.
            QualitySettings.vSyncCount = 1;

            // Set the target to unlimited so it doesn't fight VSync
            Application.targetFrameRate = -1;

            // Optional UX Polish: Disable your FPS dropdown UI here so the player 
            // visually knows they can't use it while VSync is on!
            fpsTargetDropdown.interactable = false;
        }
        else
        {
            // Turn VSync off
            QualitySettings.vSyncCount = 0;
            fpsTargetDropdown.interactable = true;

            // Re-apply whatever manual limit the dropdown is currently set to
            SetFPSTarget(fpsTargetDropdown.value);
        }

        PlayerPrefs.SetInt("VSyncSetting", isVSyncOn ? 1 : 0);
        PlayerPrefs.Save();
    }
}