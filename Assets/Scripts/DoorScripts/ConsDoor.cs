using UnityEngine;

public class ConsDoor : Door
{
    public ConsSpawn[] consSpawns;
    public override string roomName => "Conservatory";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Finds the corresponding spawn point for the players ID
        ConsSpawn match = System.Array.Find(consSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }

}
