using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using turnyWurny;

public class Movement : NetworkBehaviour
{
    // Links to the Turn Manager so the player is kept in line of turns.
    [SerializeField] private TurnManager whomst;
    
    // Links to the Board Camera for raycasting.
    public Camera BoardCam;

    private UIController uiobj;

    // Move speed for player movements.
    public float moveSpeed = 5f;

    // The GameObject currently under the player.
    [SerializeField] public GameObject stage;

    // All tiles adjacent to the one currently under the player.
    public List<GameObject> nearby = new List<GameObject>();

    // Bool showing if the player is currently on a white tile.
    private bool onWhite = false;

    // Field for movement.
    private Vector3 targetPosition;

    // Field for activating movement in update method.
    private bool isMoving = false;

    // Stores room player is currently in for room exit logic.
    public string currentRoomName;


    // Allows the variable to be viewable by all players but only changable by the host
    public NetworkVariable<int> move_tokens = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // Checks the player is the owner of the prefab.   
        if (!IsOwner)
        {
            return;
        }
        else
        {
            uiobj = GameObject.FindFirstObjectByType<UIController>();
            if (uiobj != null)
            {
                uiobj.localPlayerScript = this;
            }
        }

        // Dynamically links the players script to the UI controller instance.
        if (UIController.Instance != null)
        {
            UIController.Instance.localPlayerScript = this;
        }
        // Searches for required references once, searches repeatedly for stage in case of delayed spawn.
        searchOnce();
        StartCoroutine(stageSearch());
        StartCoroutine(linkUI());

    }

    private IEnumerator linkUI()
    {
        // Waits until UI controller has spawned
        while (UIController.Instance == null) yield return null;
    
        UIController.Instance.localPlayerScript = this;
        UIController.Instance.updateMoveText();
        UIController.Instance.UpdateUIVisibility();
        uiobj.SetLocalPlayer(this);
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

            if (clickedTile.occupied.Value)
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

    // Used to request movement on the server.
    [ServerRpc]
    void requestMoveServerRpc(Vector3 destination, bool landingOnWhite)
    {   
        // This is the tile the player has clicked to move to.
        Tile targetTile = GetTileAtPosition(destination);

        // Validates movements and updates onto the server
        if (targetTile == null) Debug.LogError($"SERVER: Failed to find tile at {destination}");

        // Allows movement if the tile is not occupied and the player has enough move tokens.
        if (move_tokens.Value > 0 && targetTile != null && !targetTile.occupied.Value)
        {
            if (stage != null) stage.GetComponent<Tile>().updateOccupied(false);
            move_tokens.Value--;

            // Marks tile as occupied if player moves onto it.
            targetTile.updateOccupied(true);
            movePositionClientRpc(destination, landingOnWhite);

            // If player is out of moves, triggers turn change
            if (move_tokens.Value == 0)
            {
                Invoke("delayNextTurn", 0.5f);
            }
        }
    }

    // Activates movement on clients side.
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
            Debug.Log($"Raycast hit: {hit.collider.name}");
            // Detects if the stage below is a Tile
            Tile tileComponent = hit.collider.GetComponent<Tile>();
            if (tileComponent != null)
            {
                stage = hit.collider.gameObject;
                onWhite = hit.collider.GetComponent<White>() != null;
                Debug.Log($"Found tile: {stage.name}");
            }
            // Detects if the stage below is a Room
            Room roomComponent = hit.collider.GetComponentInParent<Room>();
            if (roomComponent != null)
            {
                stage = hit.collider.gameObject;
                currentRoomName = roomComponent.myName;
                Debug.Log($"Room detected: {currentRoomName}");
            }
            else
            {
                Debug.LogWarning($"Hit {hit.collider.name} but no Room component found!");
            }
        }
        else
        {
            Debug.LogWarning("No stage found!");
        }
        if (IsOwner && UIController.Instance != null)
        {
            UIController.Instance.UpdateUIVisibility();
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
        Debug.Log($"IsInRoom check: stage is {(stage != null ? stage.name : "NULL")}");
        // Check if the current stage has a Room component
        if (stage != null)
        {
            return stage.GetComponent<Room>() != null;
        }
        return false;
    }

    // Moves client side to the room of the door
    [ClientRpc]
    private void moveToRoomClientRpc(Vector3 roomPos)
    {
        transform.position = roomPos;
        targetPosition = roomPos;
        isMoving = false; 
        onWhite = false;
        whereWeAt();
        StartCoroutine(delayedExitList());
    }

    private IEnumerator delayedExitList()
    {
        yield return new WaitForSeconds(0.1f);
        if (UIController.Instance != null)
        {
            UIController.Instance.exitDropdown();
        }
    }

    // Submits request to move player to room
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void submitEntryServerRpc(ulong stageNetworkObjectId)
    {
        // Find the object on the server using the ID passed by the client
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(stageNetworkObjectId, out NetworkObject stageNetObj))
        {
            // Now use this netObj instead of the local 'stage' variable
            Tile tileComp = stageNetObj.GetComponent<Tile>();
            if (tileComp != null)
            {
                tileComp.updateOccupied(false);
            }

            Door door = stageNetObj.GetComponent<Door>();
            if (door == null) return;

            Vector3 targetPos = door.GetRoomPosition((int)OwnerClientId);

            // Update position on server
            transform.position = targetPos;
        
            // Notify clients
            moveToRoomClientRpc(targetPos);
            move_tokens.Value = 0;
        
            if (whomst.whatPhase.Value == TurnStage.MOVING)
            {
                whomst.pushNextPhase();
            }
        }
        else
        {
            Debug.LogError("Server could not find stage with ID: " + stageNetworkObjectId);
        }
    }
    
    // Moves the client to the exit selected.
    [ClientRpc]
    private void exitRoomClientRPC(Vector3 exitPos)
    {
        targetPosition = exitPos;
        isMoving = true;
    }

    // Submits exit request to the server.
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
                move_tokens.Value--;
                exitRoomClientRPC(exit.transform.position);
                Debug.Log("The exit button hath been pressed");
            }
        }
    }

    // ------------ End of room entry logic ------------

    // Returns the tile the player has clicked to move to.
    private Tile GetTileAtPosition(Vector3 pos)
    {
        Collider[] colls = Physics.OverlapSphere(pos, 0.5f);
    
        foreach (var c in colls)
        {
            Tile t = c.GetComponent<Tile>();
            if (t != null) 
            {
                return t;
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