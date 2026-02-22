using UnityEngine;
using UnityEngine.UI;

public class PingMarker : MonoBehaviour
{
    [Header("Tracking")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2.0f, 0f);

    [Header("Animation")]
    public float bobSpeed = 5f;
    public float bobHeight = 15f; // measured in screen pixels
    public float popInSpeed = 15f;

    private Camera mainCam;
    private Image img;
    private float currentScale = 0f;

    void Start()
    {
        mainCam = Camera.main;
        img = GetComponent<Image>();
        transform.localScale = Vector3.zero;
    }

    public void Initialize(Transform newTarget)
    {
        target = newTarget;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!mainCam) return;

        // 1. Convert 3D world position to 2D screen position
        Vector3 screenPos = mainCam.WorldToScreenPoint(target.position + offset);

        // 2. Hide if behind the camera
        if (screenPos.z < 0)
        {
            if (img) img.enabled = false;
            return;
        }
        else
        {
            if (img) img.enabled = true;
        }

        // 3. Apply position directly to the screen with a pixel-based bobbing effect
        float bobY = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(screenPos.x, screenPos.y + bobY, 0f);

        // 4. Pop-in Animation
        currentScale = Mathf.Lerp(currentScale, 1f, Time.deltaTime * popInSpeed);
        transform.localScale = Vector3.one * currentScale;
    }
}