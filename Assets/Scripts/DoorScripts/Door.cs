using UnityEngine;
using Unity.Netcode;

public abstract class Door : MonoBehaviour
{
    public abstract string roomName { get; }
    public string exitName;
    public abstract Vector3 GetRoomPosition(int clientId);
}