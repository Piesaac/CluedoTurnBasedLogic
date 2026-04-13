using UnityEngine;

public class LoungeDoor : Door
{
    public LoungeSpawn[] loungeSpawns;
    public override string roomName => "Lounge";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Finds the corresponding spawn point for the players ID
        LoungeSpawn match = System.Array.Find(loungeSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }


}
