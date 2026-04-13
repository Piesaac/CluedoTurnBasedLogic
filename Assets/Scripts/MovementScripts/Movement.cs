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

    [Header("Room Fields")]
    public GameObject ballroom;
    public GameObject billiard;
    public GameObject conserve;
    public GameObject dining;
    public GameObject hall;
    public GameObject kitchen;
    public GameObject library;
    public GameObject lounge;
    public GameObject study;

    public string currentRoomName;


    // Allows the variable to be viewable by all players but only changable by the host
    public NetworkVariable<int> move_tokens = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        if (UIController.Instance != null)
        {
            UIController.Instance.localPlayerScript = this;
        }
        searchOnce();
        StartCoroutine(stageSearch());
    }

    // Looks for stage below the player repeatedly in case spawns are delayed
    private System.Collections.IEnumerator stageSearch()
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
        /*
        if (doorui == null)
        {
            doorui = GameObject.FindFirstObjectByType<DoorUI>();
            Debug.Log(doorui != null ? "Found DoorUI!" : "CRITICAL: Could not find DoorUI");
        }
        */

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
                Invoke("delayNextTurn", 0.5f);
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
        if (UIController.Instance != null)
        {
            UIController.Instance.UpdateUIVisibility();
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
            Room roomComponent = hit.collider.GetComponent<Room>();
            if (roomComponent != null)
            {
                stage = hit.collider.gameObject;
                currentRoomName = roomComponent.myName;
            }
        }
        else
        {
            Debug.LogWarning("No stage tile found!");
        }
    }

    // --------------- Room entry logic ----------------

    public bool IsOnDoor()
    {
        // Check if the current stage has a Door component
        if (stage != null)
        {
            return stage.GetComponent<Door>() != null;
        }
        return false;
    }

    public bool IsInRoom()
    {
        // Check if the current stage has a Room component
        if (stage != null)
        {
            return stage.GetComponent<Room>() != null;
        }
        return false;
    }

    [ClientRpc]
    private void moveToRoomClientRpc(Vector3 roomPos)
    {
        transform.position = roomPos;
        targetPosition = roomPos;
        isMoving = false; 
        onWhite = false;
        move_tokens.Value--;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void submitEntryServerRpc()
    {
        if (whomst.whosPlaying.Value != (int)OwnerClientId) return;

        // Use polymorphism to call the correct method regardless of door type
        Door door = stage.GetComponent<Door>();
        if (door == null) return;

        Vector3 targetPos = door.GetRoomPosition((int)OwnerClientId);

        transform.position = targetPos;
        whereWeAt();
        moveToRoomClientRpc(targetPos);
        if (whomst.whatPhase.Value == TurnStage.MOVING)
        {
            whomst.pushNextPhase();
        }
    }
    
    [ClientRpc]
    private void exitRoomClientRPC(Vector3 exitPos)
    {
        targetPosition = exitPos;
        isMoving = true;
        move_tokens.Value--;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void submitExitServerRPC(ulong doorId)
    {
        if (whomst.whosPlaying.Value != (int)OwnerClientId) return;

        // Find the NetworkObject by its ID on the server
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(doorId, out NetworkObject doorNetworkObject))
        {
            Door exit = doorNetworkObject.GetComponent<Door>();
            if (exit != null)
            {
                exitRoomClientRPC(exit.transform.position);
                Debug.Log("The exit button hath been pressed");
            }
        }
    }

    // ------------ End of room entry logic ------------

    private Tile GetTileAtPosition(Vector3 pos)
    {
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

    void delayNextTurn()
    {
        // Ensures this is the host and turn manager has been found
        if (IsServer) 
        {
            if (whomst != null)
            {
                whomst.nextTurn();
            }
            else
            {
                // If turn manager is not found, find it again and then pushes to next phase
                whomst = GameObject.FindFirstObjectByType<TurnManager>();
                whomst?.nextTurn();
            }
        }
    }
}