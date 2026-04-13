using UnityEngine;

public class KitchenDoor : Door
{
    public KitchSpawn[] kitchSpawns;
    public override string roomName => "Kitchen";

    public override Vector3 GetRoomPosition(int clientId)
    {
        // Find the spawn point where the ID matches the clientId
        KitchSpawn match = System.Array.Find(kitchSpawns, p => p.id == clientId);
        return (match != null) ? match.spawnPoint.position : Vector3.zero;
    }
}
