using UnityEngine;

public class ConsDoor : Door
{
    public ConsSpawn[] consSpawns;
    public override string roomName => "Conservatory";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Find the spawn point where the ID matches the clientId
        ConsSpawn match = System.Array.Find(consSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }

}
