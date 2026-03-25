using UnityEngine;

// -----------------------------------------------------------------------------
// Grants this GameObject "diplomatic immunity" so it survives scene changes.
// Uses a Singleton pattern to prevent clones from spawning when returning to the menu.
// -----------------------------------------------------------------------------
public class PersistentUI : MonoBehaviour
{
    // A global, static memory slot that holds the "official" version of this UI
    public static PersistentUI instance;

    void Awake()
    {
        // 1. Check if the official slot is empty
        if (instance == null)
        {
            // 2. If it's empty, claim the throne!
            instance = this;

            // 3. Grant diplomatic immunity to this specific GameObject
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 4. If the slot is ALREADY full, it means we are a clone freshly spawned 
            // by reloading the Main Menu. Destroy ourselves immediately before anyone sees!
            Destroy(gameObject);
        }
    }
}