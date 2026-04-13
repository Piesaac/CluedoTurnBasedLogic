using UnityEngine;
using Unity.Netcode;

public class BallDoor : Door
{
    public BallSpawn[] ballSpawns;
    public override string roomName => "Ballroom";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Finds the corresponding spawn point for the players ID
        BallSpawn match = System.Array.Find(ballSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
