using UnityEngine;

public class LibDoor : Door
{
    public LibSpawn[] libSpawns;
    public override string roomName => "Library";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Finds the corresponding spawn point for the players ID
        LibSpawn match = System.Array.Find(libSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
