using UnityEngine;

public class BilliarDoor : Door
{
    public BillSpawn[] billSpawns;
    public override string roomName => "Billiard";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Finds the corresponding spawn point for the players ID
        BillSpawn match = System.Array.Find(billSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
    
}
