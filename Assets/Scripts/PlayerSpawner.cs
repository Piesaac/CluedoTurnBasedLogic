using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour
{
    public GameObject playerPrefab; // Drag your prefab here
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnAllPlayers();
        }
    }

    private void SpawnAllPlayers()
    {
        // Get all connected clients
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        // 1. Get all spawn points in the scene
        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
    
        // 2. Find the point where the index matches the clientId
        // If no point is found, chosenPoint will be null
        SpawnPoint chosenPoint = System.Array.Find(points, p => p.index == (int)clientId);

        // 3. Safety check: If we can't find a match, default to the first one found
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

        // 4. Extract position and rotation safely
        Vector3 pos = chosenPoint.transform.position;
        Quaternion rot = chosenPoint.transform.rotation;

        // 5. Instantiate and Spawn
        GameObject player = Instantiate(playerPrefab, pos, rot);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    
        Debug.Log($"[Server] Manually spawned client {clientId} at {pos}");
    }
}