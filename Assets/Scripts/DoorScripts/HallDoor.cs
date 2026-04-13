using UnityEngine;

public class HallDoor : Door
{
    public HallSpawn[] hallSpawns;
    public override string roomName => "Hall";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Finds the corresponding spawn point for the players ID
        HallSpawn match = System.Array.Find(hallSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
