using UnityEngine;
using TMPro;

// -----------------------------------------------------------------------------
// Calculates the frames per second and updates a text UI element.
// Uses a polling timer so the numbers don't flicker unreadably fast.
// -----------------------------------------------------------------------------
public class FPSDisplay : MonoBehaviour
{
    public TextMeshProUGUI fpsText;

    // How often the text updates
    private float pollingTime = 0.2f;
    private float time;
    private int frameCount;

    void Update()
    {
        // Unscaled time ensures the FPS counter stays accurate even if the game is paused or in slow-motion
        time += Time.unscaledDeltaTime;
        frameCount++;

        if (time >= pollingTime)
        {
            // Calculate the average framerate over the last half-second
            int frameRate = Mathf.RoundToInt(frameCount / time);

            if (fpsText != null)
                fpsText.text = frameRate.ToString() + " FPS";

            // Reset the timers
            time -= pollingTime;
            frameCount = 0;
        }
    }
}