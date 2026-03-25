using System.Collections.Generic;
using UnityEngine;

// -----------------------------------------------------------------------------
// The centralized memory bank for the game world. Survives scene loads so it 
// can pass the list of dead enemies to the new scene before they render.
// -----------------------------------------------------------------------------
public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager I;

    [Header("World Memory")]
    public int worldSeed;
    public List<string> destroyedEntities = new List<string>();

    [HideInInspector]
    public SaveData pendingLoad;

    void Awake()
    {
        // Standard Singleton Pattern to survive scene loads
        if (I == null)
        {
            I = this;
            DontDestroyOnLoad(gameObject);

            // If this is a brand new game, pick a random seed for the Spawner to use!
            worldSeed = Random.Range(1, 999999);

            // Explicitly destroy Unity's auto-generated ghost file
            pendingLoad = null;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called by enemies and items exactly when they are destroyed.
    /// </summary>
    public void RegisterDeath(string entityID)
    {
        if (!destroyedEntities.Contains(entityID))
        {
            destroyedEntities.Add(entityID);
        }
    }
}