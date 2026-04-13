using UnityEngine;

public class StudDoor : Door
{
    public StudSpawn[] studSpawns;
    public override string roomName => "Study";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Find the spawn point where the ID matches the clientId
        StudSpawn match = System.Array.Find(studSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
