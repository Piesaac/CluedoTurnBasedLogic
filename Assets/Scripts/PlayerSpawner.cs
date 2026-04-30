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

        while (availableIndexes.Count > 0)
        {
            ulong uniqueId = (ulong)(500 + availableIndexes.Count); 
            SpawnPlayer(uniqueId, false, availableIndexes);
        }
    }


    private void SpawnPlayer(ulong ownerId, bool isHuman, List<int> availableIndexes)
    {
        if (availableIndexes.Count == 0)
        {
            Debug.LogError("PlayerSpawner: SpawnPlayer() | No prefabs remaining");
            return;
        }
        int prefabIndex = availableIndexes[0];
        availableIndexes.RemoveAt(0);

        GameObject prefabToSpawn = playerPrefabs[prefabIndex];

        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        SpawnPoint chosenPoint = System.Array.Find(points, p => p.index == prefabIndex);

        if (chosenPoint == null)
        {
            Debug.LogWarning($"PlayerSpawner: SpawnPlayer() | No specific spawn point for prefab {prefabIndex}, using default index 0.");
            chosenPoint = (points.Length > 0) ? points[0] : null;
        }

        if (chosenPoint == null)
        {
            Debug.LogError("PlayerSpawner: SpawnPlayer() | No SpawnPoints found in the scene");
            return;
        }

        GameObject playerInstance = Instantiate(prefabToSpawn, chosenPoint.transform.position, chosenPoint.transform.rotation);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        if (playerInstance.TryGetComponent<Character>(out var character))
        {
            character.isRobot.Value = !isHuman;
            character.botID.Value = (int)ownerId;

        }

        if (isHuman)
        {
            netObj.SpawnAsPlayerObject(ownerId);
        }
        else
        {
            netObj.Spawn();
        }
        
    }

}