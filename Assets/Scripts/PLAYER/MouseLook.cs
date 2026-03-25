using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public Transform playerBody;  // assign Player root
    public float mouseSensitivity = 120f;
    public float minPitch = -70f;
    public float maxPitch = 80f;

    float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        // Apply the player's saved sensitivity multiplier
        float sensMult = PlayerPrefs.GetFloat("MouseSensitivitySetting", 1f);
        mouseSensitivity *= sensMult;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f); // tilt camera up/down

        if (playerBody) playerBody.Rotate(Vector3.up * mouseX);   // rotate player left/right
    }
}
