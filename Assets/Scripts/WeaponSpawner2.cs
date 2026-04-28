using UnityEngine;
using Unity.Netcode;

public class WeaponSpawner2 : NetworkBehaviour
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

        // Shuffle spawn points
        Transform[] shuffledPoints = (Transform[])spawnPoints.Clone();
        ShuffleArray(shuffledPoints);

        // Shuffle weapon prefabs (THIS removes duplicates)
        GameObject[] shuffledWeapons = (GameObject[])weaponPrefabs.Clone();
        ShuffleArray(shuffledWeapons);

        int count = Mathf.Min(shuffledWeapons.Length, shuffledPoints.Length);

        for (int i = 0; i < count; i++)
        {
            GameObject weaponPrefab = shuffledWeapons[i];
            Transform spawnPoint = shuffledPoints[i];

            GameObject weaponInstance = Instantiate(
                weaponPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

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

    // Generic shuffle (works for both Transform[] and GameObject[])
    private void ShuffleArray<T>(T[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            int randomIndex = Random.Range(i, array.Length);
            T temp = array[i];
            array[i] = array[randomIndex];
            array[randomIndex] = temp;
        }
    }
}
