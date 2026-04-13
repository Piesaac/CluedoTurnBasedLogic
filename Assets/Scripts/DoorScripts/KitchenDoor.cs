using UnityEngine;

public class KitchenDoor : Door
{
    public KitchSpawn[] kitchSpawns;
    public override string roomName => "Kitchen";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Finds the corresponding spawn point for the players ID
        KitchSpawn match = System.Array.Find(kitchSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
