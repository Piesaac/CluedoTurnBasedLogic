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

    public void anyoneHome()
    {
        // Start slightly below the tile center to ensure the ray starts inside the collider
        Vector3 rayStart = transform.position + Vector3.down * 0.1f;
        float rayDistance = 2.0f; // Adjust based on how high the player is

        // Fire the ray upwards
        if (Physics.Raycast(rayStart, Vector3.up, out RaycastHit hit, rayDistance, playerLayer))
        {
            // If we hit something on the player layer, it's occupied
            occupied = true;
            Debug.Log($"Tile {gameObject.name} is occupied by {hit.collider.name}");
        }
        else
        {
            occupied = false;
        }
        Debug.DrawRay(rayStart, Vector3.up * rayDistance, occupied ? Color.red : Color.green, 2.0f);
    }
    
    // Gets the top position when clicked, for the players movement.
    public Vector3 getTopPosition()
    {
        return new Vector3(transform.position.x, 0.5f, transform.position.z);
    }
}