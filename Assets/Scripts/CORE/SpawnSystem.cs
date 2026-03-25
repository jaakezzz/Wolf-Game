using UnityEngine;
using System.Linq;

public class SpawnSystem : MonoBehaviour
{
    [Header("Scatter Spawns (Food)")]
    public Transform[] spawnPoints;     // assign food spawns here
    public GameObject[] spawnables;     // food prefabs to choose from
    public int toSpawn = 16;

    [Header("Objective Spawn (Chicken)")]
    public GameObject objectivePrefab;  // The win condition item
    public Transform[] objectivePoints; // Potential hiding spots for the chicken

    [Header("Environment")]
    public Terrain terrain;             // snap y to terrain

    void Start()
    {
        // --- Force the Spawner to use our specific Saved Seed ---
        if (WorldStateManager.I != null)
        {
            Random.InitState(WorldStateManager.I.worldSeed);
        }

        SpawnScatterItems();
        SpawnMainObjective();
    }

    void SpawnScatterItems()
    {
        if (spawnPoints == null || spawnPoints.Length < toSpawn)
        {
            Debug.LogWarning("Not enough food spawn points configured.");
            return;
        }

        // Pick distinct random points
        var chosen = spawnPoints.OrderBy(_ => Random.value).Take(toSpawn).ToArray();

        for (int i = 0; i < chosen.Length; i++)
        {
            var prefab = spawnables[Random.Range(0, spawnables.Length)];
            var p = chosen[i].position;

            // snap to terrain height if provided
            if (terrain && terrain.terrainData)
            {
                float h = terrain.SampleHeight(p) + terrain.GetPosition().y;
                p.y = h;
            }

            var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Instantiate(prefab, p, rot);
        }
    }

    void SpawnMainObjective()
    {
        // Safety check to ensure we actually assigned a chicken and at least one spawn point
        if (objectivePrefab == null || objectivePoints == null || objectivePoints.Length == 0)
            return;

        // Pick EXACTLY ONE random point from the objective list
        Transform chosenPoint = objectivePoints[Random.Range(0, objectivePoints.Length)];
        Vector3 spawnPos = chosenPoint.position;

        // Snap the chicken to the terrain just like the food
        if (terrain && terrain.terrainData)
        {
            float h = terrain.SampleHeight(spawnPos) + terrain.GetPosition().y;
            spawnPos.y = h;
        }

        // Spawn it
        Instantiate(objectivePrefab, spawnPos, Quaternion.identity);
    }
}