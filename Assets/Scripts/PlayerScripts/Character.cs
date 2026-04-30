using UnityEngine;
using Unity.Netcode;

public abstract class Character : NetworkBehaviour
{
    public abstract string charName { get; }
    public NetworkVariable<bool> isRobot;
    public NetworkVariable<int> botID = new NetworkVariable<int>(-1);
    public NetworkVariable<bool> isOut = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isAbsent = new NetworkVariable<bool>(false);

    public void roboHunt()
    {
        if (IsServer)
        {
            isRobot.Value = true;
        }
    }
}
