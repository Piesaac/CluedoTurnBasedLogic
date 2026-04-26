using UnityEngine;
using Unity.Netcode;

public class WeaponSpawner : NetworkBehaviour
{
    [Header("Weapon Prefabs")]
    [SerializeField] private GameObject[] weaponPrefabs;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        // Only the server should spawn weapons
        if (!IsServer) return;

        SpawnWeapons();
    }

    private void SpawnWeapons()
    {
        if (weaponPrefabs.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogWarning("WeaponSpawner: Missing prefabs or spawn points.");
            return;
        }

        // Shuffle spawn points so placement is random
        Transform[] shuffledPoints = (Transform[])spawnPoints.Clone();
        ShuffleArray(shuffledPoints);

        for (int i = 0; i < weaponPrefabs.Length; i++)
        {
            GameObject weaponPrefab = weaponPrefabs[Random.Range(0, weaponPrefabs.Length)];
            Transform spawnPoint = shuffledPoints[i];

            GameObject weaponInstance = Instantiate(
                weaponPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            // This makes it networked
            NetworkObject netObj = weaponInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
            else
            {
                Debug.LogError($"Weapon {weaponPrefab.name} is missing a NetworkObject component!");
            }
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
