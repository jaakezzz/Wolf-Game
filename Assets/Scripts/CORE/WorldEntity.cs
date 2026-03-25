using UnityEngine;

// -----------------------------------------------------------------------------
// Attached to any object that can be permanently killed or collected.
// Generates a unique ID, and deletes itself on load if it was already collected.
// -----------------------------------------------------------------------------
public class WorldEntity : MonoBehaviour
{
    public string id;
    bool isApplicationQuitting = false;

    void Start()
    {
        // 1. Generate a permanent, unique ID based on where this object was placed.
        // Rounding the coordinates prevents micro-decimals from breaking the ID!
        string posHash = $"{Mathf.RoundToInt(transform.position.x)}_{Mathf.RoundToInt(transform.position.y)}_{Mathf.RoundToInt(transform.position.z)}";
        id = $"{gameObject.name}_{posHash}";

        // 2. Ask the World Brain: "Was I killed/eaten in a previous save?"
        if (WorldStateManager.I != null && WorldStateManager.I.destroyedEntities.Contains(id))
        {
            // I am already dead! Delete my geometry immediately before the player sees me.
            Destroy(gameObject);
        }
    }

    // Safety flags to prevent unity from thinking objects are "dying" when you close the game
    void OnApplicationQuit() { isApplicationQuitting = true; }

    void OnDestroy()
    {
        if (isApplicationQuitting) return;

        // Safety Check: We ONLY want to record a death if the scene is actually active.
        // If the scene is unloading (because we are reloading a save), ignore this!
        if (!gameObject.scene.isLoaded) return;

        // 3. Tell the World Brain to remember that I was permanently destroyed
        if (WorldStateManager.I != null)
        {
            WorldStateManager.I.RegisterDeath(id);
        }
    }
}