using UnityEngine;
using Unity.Netcode;

public class BallDoor : Door
{
    public BallSpawn[] ballSpawns;
    public override string roomName => "Ballroom";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Find the spawn point where the ID matches the clientId
        BallSpawn match = System.Array.Find(ballSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
