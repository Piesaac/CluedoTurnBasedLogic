using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour
{
    public GameObject playerPrefab; 
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnAllPlayers();
        }
    }

    private void SpawnAllPlayers()
    {
        // Gets all connected clients and spawns one for each
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    // Spawns a player for the client inputted.
    private void SpawnPlayerForClient(ulong clientId)
    {   
        // Finds player spawn points specified.
        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
    
        // Chooses specific spawn point depending on client ID.
        SpawnPoint chosenPoint = System.Array.Find(points, p => p.index == (int)clientId);

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
        GameObject player = Instantiate(playerPrefab, pos, rot);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    
        Debug.Log($"[Server] Manually spawned client {clientId} at {pos}");
    }
}