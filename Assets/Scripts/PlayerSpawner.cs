using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerSpawner : NetworkBehaviour
{
    public GameObject[] playerPrefabs; 

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnAllPlayers();
        }
    }

    private void SpawnAllPlayers()
    {
        List<int> availableIndexes = new List<int>();
        for (int i = 0; i < playerPrefabs.Length; i++)
        {
            availableIndexes.Add(i);
        }
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayer(clientId, true, availableIndexes);
        }
        int aiPlayers = MenuController.numBotsToSpawn; 
        for (int i = 0; i < aiPlayers; i++)
        {
            SpawnPlayer((ulong)(100 + i), false, availableIndexes);
        }

    }

    private void SpawnPlayer(ulong ownerId, bool isHuman, List<int> availableIndexes)
    {
        if (availableIndexes.Count == 0)
        {
            Debug.LogError("[Server] No more unique prefabs left in the list!");
            return;
        }

        int prefabIndex = availableIndexes[0];
        availableIndexes.RemoveAt(0);

        GameObject prefabToSpawn = playerPrefabs[prefabIndex];

        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        SpawnPoint chosenPoint = System.Array.Find(points, p => p.index == prefabIndex);

        if (chosenPoint == null)
        {
            Debug.LogWarning($"[Server] No specific spawn point for prefab {prefabIndex}, using default.");
            chosenPoint = points[0];
        }

        GameObject playerInstance = Instantiate(prefabToSpawn, chosenPoint.transform.position, chosenPoint.transform.rotation);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        if (isHuman)
        {
            netObj.SpawnAsPlayerObject(ownerId);
        }
        else
        {
            netObj.Spawn();
        }
    }

    // Spawns a player for the client inputted.
    private void SpawnPlayerForClient(ulong clientId)
    {   
        // Finds player spawn points specified.
        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
    
        // Chooses specific spawn point depending on client ID.
        SpawnPoint chosenPoint = System.Array.Find(points, p => p.index == (int)clientId);

        GameObject chosenPrefab = playerPrefabs[clientId];
                
        // If none are found, uses default spawn.
        if (chosenPoint == null && points.Length > 0)
        {
            Debug.LogWarning($"[Server] No spawn point found for ID {clientId}, using index 0.");
            chosenPoint = points[0];
        }
        else if (chosenPoint == null)
        {
            Debug.LogError($"[Server] FATAL: No spawn points found in scene!");
            return;
        }

        // Finds position and rotation for spawn point.
        Vector3 pos = chosenPoint.transform.position;
        Quaternion rot = chosenPoint.transform.rotation;

        // Created player instance and spawns them.
        GameObject playerInstance = Instantiate(chosenPrefab, pos, rot);

        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    
        Debug.Log($"[Server] Manually spawned client {clientId} at {pos}");
    }
}