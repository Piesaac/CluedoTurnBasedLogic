using UnityEngine;
using Unity.Netcode;
using Netcode = Unity.Netcode.NetworkManager;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Assign 6 transforms here")]
    public Transform[] spawnPoints; 

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            takePosition();

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        takePosition();
    }

    private void takePosition()
    {
        int index = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && index < spawnPoints.Length)
            {
                Transform spawnArea = spawnPoints[index];
                client.PlayerObject.transform.position = spawnArea.position;
                client.PlayerObject.transform.rotation = spawnArea.rotation;

                // NEW: Tell the movement script to find the floor at this new spot
                if (client.PlayerObject.TryGetComponent<Movement>(out var moveScript))
                {
                    moveScript.whereWeAt();
                }

                index++;
            }
        }
    }
}