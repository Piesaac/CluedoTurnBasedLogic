using UnityEngine;
using Unity.Netcode;

public abstract class Character : NetworkBehaviour
{
    public abstract string charName { get; }

    // Synced variables for AI identity
    public NetworkVariable<bool> isRobot = new NetworkVariable<bool>(false);
    public NetworkVariable<int> botID = new NetworkVariable<int>(-1);

    public void roboHunt()
    {
        if (IsServer)
        {
            isRobot.Value = true;
        }
    }
}