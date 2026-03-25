using UnityEngine;

// -----------------------------------------------------------------------------
// Lives in the Game Scene. Wakes up on load and forces the engine to remember 
// the framerate limits that were set in the Main Menu.
// -----------------------------------------------------------------------------
public class GameSettingsEnforcer : MonoBehaviour
{
    void Start()
    {
        // Check if VSync was supposed to be on
        int savedVSync = PlayerPrefs.GetInt("VSyncSetting", 1);
        QualitySettings.vSyncCount = savedVSync;

        if (savedVSync == 0) // If VSync is OFF, enforce the manual frame limit
        {
            int targetIndex = PlayerPrefs.GetInt("FPSTargetSetting", 1);
            switch (targetIndex)
            {
                case 0: Application.targetFrameRate = 30; break;
                case 1: Application.targetFrameRate = 60; break;
                case 2: Application.targetFrameRate = 120; break;
                case 3: Application.targetFrameRate = -1; break;
            }
        }
        else // If VSync is ON, remove the manual cap so they don't fight
        {
            Application.targetFrameRate = -1;
        }
    }
}