using UnityEngine;
using Unity.Netcode;

public abstract class Door : MonoBehaviour
{
    // Inherited by all other door forms but overriden within.
    public abstract string roomName { get; }
    public string exitName;
    // Method for finding room spawn point, overriden by all subclasses.
    public abstract Vector3 GetRoomPosition(int clientId);
}