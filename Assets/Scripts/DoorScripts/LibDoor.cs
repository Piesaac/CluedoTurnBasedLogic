using UnityEngine;

public class LibDoor : Door
{
    public LibSpawn[] libSpawns;
    public override string roomName => "Library";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Find the spawn point where the ID matches the clientId
        LibSpawn match = System.Array.Find(libSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
