using UnityEngine;
using Unity.Netcode;

public class WeaponSpawner : NetworkBehaviour
{
    [Header("Scene Weapons (already in scene)")]
    [SerializeField] private NetworkObject[] weapons;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        PlaceWeapons();
    }

    private void PlaceWeapons()
    {
        if (weapons.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogWarning("WeaponSpawner: Missing weapons or spawn points.");
            return;
        }

        // Shuffle spawn points
        Transform[] shuffledPoints = (Transform[])spawnPoints.Clone();
        ShuffleArray(shuffledPoints);

        int count = Mathf.Min(weapons.Length, shuffledPoints.Length);

        for (int i = 0; i < count; i++)
        {
            NetworkObject weapon = weapons[i];
            Transform spawnPoint = shuffledPoints[i];

            // Move weapon on server
            weapon.transform.position = spawnPoint.position;
            weapon.transform.rotation = spawnPoint.rotation;

            // Make sure it's spawned on the network
            
        }
    }

    private void ShuffleArray(Transform[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            int randomIndex = Random.Range(i, array.Length);
            Transform temp = array[i];
            array[i] = array[randomIndex];
            array[randomIndex] = temp;
        }
    }
}
