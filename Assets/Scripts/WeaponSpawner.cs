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

        // Shuffle spawn points for randomness
        Transform[] shuffledPoints = (Transform[])spawnPoints.Clone();
        ShuffleArray(shuffledPoints);

        int count = Mathf.Min(weaponPrefabs.Length, shuffledPoints.Length);

        for (int i = 0; i < count; i++)
        {
            GameObject weaponPrefab = weaponPrefabs[Random.Range(0, weaponPrefabs.Length)];
            Transform spawnPoint = shuffledPoints[i];

            // Instantiate on server
            GameObject weaponInstance = Instantiate(
                weaponPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            // Spawn across network
            NetworkObject netObj = weaponInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
            else
            {
                Debug.LogError($"Weapon {weaponPrefab.name} is missing a NetworkObject!");
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
