using UnityEngine;
using Unity.Netcode;

public abstract class Character : NetworkBehaviour
{
    public abstract string charName { get; }
    public NetworkVariable<bool> isRobot;

    public void roboHunt()
    {
        if (IsServer)
        {
            isRobot.Value = true;
        }
    }
}
