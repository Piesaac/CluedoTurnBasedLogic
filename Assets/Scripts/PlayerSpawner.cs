using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerSpawner : NetworkBehaviour
{
    public GameObject[] playerPrefabs;

    public override void OnNetworkSpawn()
    {
        // Only the Server/Host handles the spawning logic
        if (IsServer)
        {
            SpawnAllPlayers();
        }
    }

    private void SpawnAllPlayers()
    {
        if (playerPrefabs == null || playerPrefabs.Length == 0)
        {
            Debug.LogError("PlayerSpawner: No prefabs assigned in the Inspector!");
            return;
        }

        List<int> availableIndexes = new List<int>();
        for (int i = 0; i < playerPrefabs.Length; i++)
        {
            availableIndexes.Add(i);
        }

        // 1. Spawn Human Players
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayer(clientId, true, availableIndexes);
        }

        // 2. Spawn AI Players using the static count from MenuController
        int aiPlayers = MenuController.numBotsToSpawn;
        Debug.Log($"[Spawner] Spawning {aiPlayers} AI bots...");

        for (int i = 0; i < aiPlayers; i++)
        {
            // AI IDs start at 100
            SpawnPlayer((ulong)(100 + i), false, availableIndexes);
        }
    }

    private void SpawnPlayer(ulong ownerId, bool isHuman, List<int> availableIndexes)
    {
        if (availableIndexes.Count == 0)
        {
            Debug.LogWarning("PlayerSpawner: Ran out of character prefabs!");
            return;
        }

        int prefabIndex = availableIndexes[0];
        availableIndexes.RemoveAt(0);

        GameObject prefabToSpawn = playerPrefabs[prefabIndex];

        // Find the spawn point matching this character index
        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        SpawnPoint chosenPoint = System.Array.Find(points, p => p.index == prefabIndex);

        if (chosenPoint == null) chosenPoint = (points.Length > 0) ? points[0] : null;
        if (chosenPoint == null) return;

        // 1. Instantiate the object
        GameObject playerInstance = Instantiate(prefabToSpawn, chosenPoint.transform.position, chosenPoint.transform.rotation);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        // 2. Spawn it on the network FIRST
        if (isHuman)
        {
            netObj.SpawnAsPlayerObject(ownerId);
        }
        else
        {
            netObj.Spawn();
        }

        // 3. SET DATA AFTER SPAWNING (This ensures NetworkVariables sync correctly)
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
    }
}