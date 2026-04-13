using UnityEngine;

public class DineDoor : Door
{
    public DineSpawn[] dineSpawns;
    public override string roomName => "Dining";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Finds the corresponding spawn point for the players ID
        DineSpawn match = System.Array.Find(dineSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
