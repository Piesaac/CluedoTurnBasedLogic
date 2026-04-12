using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Unity.Netcode;
using turnyWurny;

public class Movement : NetworkBehaviour
{
    [SerializeField] private TurnManager whomst;
    public Camera BoardCam;
    public float moveSpeed = 5f;

    [SerializeField] public GameObject stage;
    public List<GameObject> nearby = new List<GameObject>();
    private bool onWhite = false;

    private Vector3 targetPosition;
    private bool isMoving = false;



    // Allows the variable to be viewable by all players but only changable by the host
    public NetworkVariable<int> move_tokens = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        searchOnce();
        StartCoroutine(floorSearch());
    }

    // Looks for stage below the player repeatedly in case spawns are delayed.
    private System.Collections.IEnumerator floorSearch()
    {
        int attempts = 0;
        while (stage == null && attempts < 20)
        {
            whereWeAt();
            if (stage != null) break;
        
            attempts++;
            yield return new WaitForSeconds(0.2f);
        }

        if (stage == null)
        Debug.LogError("Could not find stage below player");
    }

    // Looks once for references needed so does not need to be called in Update method
    private void searchOnce()
    {   
        // Finds the turn manager
        if (whomst == null)
        {
            whomst = GameObject.FindFirstObjectByType<TurnManager>();
            Debug.Log(whomst != null ? "Found TurnManager!" : "CRITICAL: Could not find TurnManager");
        }

        // Finds the Board camera
        if (BoardCam == null)
        {
            Camera[] allCameras = Resources.FindObjectsOfTypeAll<Camera>();
            Debug.Log($"Found {allCameras.Length} cameras in total.");
            foreach (Camera cam in allCameras)
            {
                if (cam.name == "BoardCam")
                {
                    BoardCam = cam;
                    break;
                }
            }

            if (BoardCam == null)
            {
                BoardCam = Camera.main;
            }
        }

        // Indicates if any references are missing
        if (whomst == null) Debug.LogWarning("Movement: Still looking for TurnManager...");
        if (BoardCam == null) Debug.LogError("Movement: BoardCam not found! Input will fail.");
    }

    // This links to the rolling script to set the move tokens.
    [ServerRpc]
    public void setMovesServerRpc(int value)
    {
        move_tokens.Value = value;
    }

    // Runs 60 times in a second
    void Update()
    {
        if (!IsOwner) return;

        // If references are null, searches until they are found and then stops.
        if (whomst == null || BoardCam == null)
        {
            searchOnce();
        }

        // Checks if mouse was clicked for input
        if (whomst != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log($"Click registered! Turn: {whomst.whosPlaying.Value}, Phase: {whomst.whatPhase.Value}, Moving: {isMoving}");
            bool isMyTurn = (whomst.whosPlaying.Value == (int)NetworkManager.Singleton.LocalClientId);
            bool isMovingPhase = (whomst.whatPhase.Value == TurnStage.MOVING);

            if (isMyTurn && isMovingPhase && !isMoving)
            {
                checkInput();
            }
        }

        if (isMoving) movePlayer();

    }   

    // Cheks clicked tile against the conditions for movement
    void checkInput()
    {
        if (BoardCam == null) searchOnce();
        if (BoardCam == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = BoardCam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // If there is no tile component on the clicked object, returns
            Tile clickedTile = hit.collider.GetComponent<Tile>();
            if (clickedTile == null || stage == null) return;

            if (clickedTile.occupied)
            {
                Debug.Log("Clicked tile is marked as occupied");
                return;
            }

            // Sets the current stage and checks neighbours
            Tile currentStand = stage.GetComponent<Tile>();
            nearby = currentStand.neighbours;

            // If the clicked tile is adjacent and move tokens are not null, validates movement
            if (nearby.Contains(hit.collider.gameObject) && move_tokens.Value > 0)
            {
                bool isWhite = hit.collider.GetComponent<White>() != null;
                bool isBlack = hit.collider.GetComponent<Black>() != null;

                // Checks clicked tile is opposite colour, to enforce only vertical/horizontal movement
                if ((isWhite && !onWhite) || (isBlack && onWhite))
                {
                    requestMoveServerRpc(clickedTile.getTopPosition(), isWhite);
                }
            }
        }
    }

    [ServerRpc]
    void requestMoveServerRpc(Vector3 destination, bool landingOnWhite)
    {
        Tile targetTile = GetTileAtPosition(destination);
        // Validates movements and updates onto the server
        if (targetTile == null) Debug.LogError($"SERVER: Failed to find tile at {destination}");
        if (move_tokens.Value > 0 && targetTile != null && !targetTile.occupied)
        {
            if (stage != null) stage.GetComponent<Tile>().updateOccupied(false);
            move_tokens.Value--;
            targetTile.updateOccupied(true);
            
            movePositionClientRpc(destination, landingOnWhite);

            // If player is out of moves, triggers turn phase change
            if (move_tokens.Value == 0)
            {
                Invoke("delayNextPhase", 0.5f);
            }
        }
    }

    // Sets the required variables for the client
    [ClientRpc]
    void movePositionClientRpc(Vector3 destination, bool landingOnWhite)
    {
        targetPosition = destination;
        isMoving = true;
        onWhite = landingOnWhite;
    }

    // Actually moves the player to the tile selected and updates stage
    void movePlayer()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
        {
            transform.position = targetPosition;
            isMoving = false;
            whereWeAt();
        }
    }

    // Updates stage by raycasting downwards and scanning for valid object
    public void whereWeAt()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        float rayDistance = 2.0f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance))
        {
            Tile tileComponent = hit.collider.GetComponent<Tile>();
            if (tileComponent != null)
            {
                stage = hit.collider.gameObject;
                onWhite = hit.collider.GetComponent<White>() != null;
                Debug.Log($"Found tile: {stage.name}");
            }
        }
        else
        {
            Debug.LogWarning("No stage tile found!");
        }
    }

    private Tile GetTileAtPosition(Vector3 pos)
    {
        // Increase the radius slightly to 0.5f to ensure we "catch" the tile
        // even if the coordinate is slightly off-center.
        Collider[] colls = Physics.OverlapSphere(pos, 0.5f);
    
        foreach (var c in colls)
        {
            Tile t = c.GetComponent<Tile>();
            if (t != null) 
            {
                return t; // Found it!
            }
        }
    
        Debug.LogWarning($"GetTileAtPosition: No Tile found near {pos}");
        return null;
    }

    void delayNextPhase()
    {
        // Ensures this is the host and turn manager has been found
        if (IsServer) 
        {
            if (whomst != null)
            {
                whomst.pushNextPhase();
            }
            else
            {
                // If turn manager is not found, find it again and then pushes to next phase
                whomst = GameObject.FindFirstObjectByType<TurnManager>();
                whomst?.pushNextPhase();
            }
        }
    }
}