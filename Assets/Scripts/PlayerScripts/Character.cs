using UnityEngine;
using Unity.Netcode;

public abstract class Character : MonoBehaviour
{
    public abstract string charName { get; }
    public bool isHuman;
}
