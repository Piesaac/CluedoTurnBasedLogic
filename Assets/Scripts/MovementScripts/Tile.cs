using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class Tile : MonoBehaviour
{
    public List<GameObject> neighbours = new List<GameObject>();
    public LayerMask tiles;
    public GameObject stage;
    [SerializeField] public bool occupied;
    [SerializeField] private LayerMask playerLayer; 

    void Start()
    {
        helloNeighbour();
    
    }

    // This is not the game hello neighbour sadly, but it does find all the neighbouring tiles to eachother which is pretty nice.
    // It makes a sphere collider slightly larger than the tile itself (10% extra= 1.1f).
    // All objects with a Tile script component (i.e. a "Tile") within this collider get added to a 'neighbours' array.

    public void helloNeighbour()
    {
    neighbours.Clear();
    Collider[] colls = Physics.OverlapSphere(transform.position, 1.1f); 
    foreach (var c in colls)
    {
        if (c.gameObject != this.gameObject && c.GetComponent<Tile>() != null)
        {
            neighbours.Add(c.gameObject);
        }
    }
    }

    public void updateOccupied(bool state) {
        occupied = state;
    }

    
    // Gets the top position when clicked, for the players movement.
    public Vector3 getTopPosition()
    {
        return new Vector3(transform.position.x, 0.5f, transform.position.z);
    }
}