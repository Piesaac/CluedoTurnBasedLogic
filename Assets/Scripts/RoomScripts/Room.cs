using CardList;
using UnityEngine;

public abstract class Room : MonoBehaviour
{
    public abstract string myName { get; }
    //added for use within movement.cs
    public Where roomIdentity;

}
