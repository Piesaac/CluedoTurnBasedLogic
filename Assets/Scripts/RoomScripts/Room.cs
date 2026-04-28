using CardList;
using UnityEngine;

public abstract class Room : MonoBehaviour
{
    public abstract string myName { get; }
    public Where roomIdentity;

}
