using UnityEngine;

public class BilliarDoor : Door
{
    public BillSpawn[] billSpawns;
    public override string roomName => "Billiard";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Find the spawn point where the ID matches the clientId
        BillSpawn match = System.Array.Find(billSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
    
}
