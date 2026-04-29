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
            Debug.LogWarning($"[Server] No specific spawn point for prefab {prefabIndex}, using default index 0.");
            chosenPoint = (points.Length > 0) ? points[0] : null;
        }

        if (chosenPoint == null)
        {
            Debug.LogError("[Server] No SpawnPoints found in the scene!");
            return;
        }

        // Instantiate the player
        GameObject playerInstance = Instantiate(prefabToSpawn, chosenPoint.transform.position, chosenPoint.transform.rotation);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        if (playerInstance.TryGetComponent<Character>(out var character))
        {
            character.isRobot.Value = !isHuman;
            character.botID.Value = (int)ownerId;

            Debug.Log($"<color=cyan>[Spawner] Configured {character.charName}: ID={ownerId}, Robot={!isHuman}</color>");
        }
        else
        {
            Debug.LogError($"[Spawner] {prefabToSpawn.name} is missing a Character-derived script!");
        }

        if (isHuman)
        {
            // Spawn as a player object owned by the specific client
            netObj.SpawnAsPlayerObject(ownerId);
        }
        else
        {
            // Spawn as a standard server-owned object (AI)
            netObj.Spawn();
        }
        
        Debug.Log($"[Server] Spawned {(isHuman ? "Human" : "AI")} ID {ownerId} using prefab {prefabIndex}");
    }

    // Manual spawn method for late-joining clients or specific requests
    private void SpawnPlayerForClient(ulong clientId)
    {
        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        SpawnPoint chosenPoint = System.Array.Find(points, p => p.index == (int)clientId);

        if (chosenPoint == null && points.Length > 0)
        {
            chosenPoint = points[0];
        }

        if (chosenPoint == null) return;

        GameObject chosenPrefab = playerPrefabs[(int)clientId % playerPrefabs.Length];
        GameObject playerInstance = Instantiate(chosenPrefab, chosenPoint.transform.position, chosenPoint.transform.rotation);
        
        // Ensure standard human setup
        if (playerInstance.TryGetComponent<Character>(out var character))
        {
            character.isRobot.Value = false;
        }

        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}