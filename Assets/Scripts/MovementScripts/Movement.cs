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
    public bool onWhite = false;

    // Field for movement.
    private Vector3 targetPosition;

    // Field for activating movement in update method.
    private bool isMoving = false;

    // Stores room player is currently in for room exit logic.
    public NetworkVariable<Unity.Collections.FixedString32Bytes> currentRoomName = 
    new NetworkVariable<Unity.Collections.FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    public NetworkVariable<int> move_tokens = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Links Player to their own UI Controller 
    public override void OnNetworkSpawn()
    {
        if (!IsOwner || turingTest())
        {
            return;
        } 
        else
        {
            /*
            uiobj = GameObject.FindFirstObjectByType<UIController>();
            if (uiobj != null)
            {
                uiobj.localPlayerScript = this;
            }
            */
        }

        searchOnce();
        StartCoroutine(stageSearch());
        StartCoroutine(linkUI());

    }

    // Returns if the local player has been marked as an AI in their Character script.
    private bool turingTest() 
    {
        Debug.Log("Movement: turingTest() called");
        if (TryGetComponent<Character>(out var c)) return c.isRobot.Value;
        return false;
    }


    private IEnumerator linkUI()
    {
        Debug.Log("Movement: linkUI() called");
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
        Debug.Log("Movement: stageSearch() called");
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
        Debug.Log("Movement: searchOnce() called");
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
        Debug.Log("Movement: setMovesServerRpc() called");
        move_tokens.Value = value;
    }

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
            bool isMyTurn = (whomst.whosPlaying.Value == NetworkManager.Singleton.LocalClientId);
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
        Debug.Log("Movement: checkInput() called");
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
        Debug.Log("Movement: requestMoveServerRpc() called");
        // This is the tile the player has clicked to move to.
        Tile currentTile = GetTileAtPosition(transform.position);
        Tile targetTile = GetTileAtPosition(destination);

        // Validates movements and updates onto the server
        if (targetTile == null) Debug.LogError($"SERVER: Failed to find tile at {destination}");

        // Allows movement if the tile is not occupied and the player has enough move tokens.
        if (move_tokens.Value > 0 && targetTile != null && !targetTile.occupied.Value)
        {
            if (currentTile != null) currentTile.updateOccupied(false);

            // Marks tile as occupied if player moves onto it.
            targetTile.updateOccupied(true);
            movePositionClientRpc(destination, landingOnWhite);
            move_tokens.Value--;

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
        Debug.Log("Movement: movePositionClientRpc() called");
        targetPosition = destination;
        isMoving = true;
        onWhite = landingOnWhite;
    }


    // Actually moves the player to the tile selected and updates stage
    void movePlayer()
    {
        Debug.Log("Movement: movePlayer() called");
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
        // Shoot ray from higher up to avoid clipping
        Vector3 rayStart = transform.position + Vector3.up * 1.5f; 
    
        // Use RaycastAll to find the Tile even if a Room Trigger is in the way
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 3.0f);
        bool foundTile = false;

        foreach (var hit in hits)
        {
            Tile tileComponent = hit.collider.GetComponent<Tile>();
            if (tileComponent != null)
            {
                stage = hit.collider.gameObject;
                onWhite = hit.collider.GetComponent<White>() != null;
                foundTile = true;
                Debug.Log($"<color=green>Movement:</color> whereWeAt found {stage.name}, White: {onWhite}");
                break; 
            }
        }

        if (!foundTile) Debug.LogWarning("<color=green>Movement:</color> whereWeAt failed to find a Tile!");

        // Detect Room separately
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit roomHit, 3.0f, Physics.AllLayers, QueryTriggerInteraction.Collide))
        {
            Room roomComponent = roomHit.collider.GetComponentInParent<Room>();
            currentRoomName.Value = roomComponent != null ? roomComponent.myName : "";
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
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 2f))
        {
            return hit.collider.GetComponent<Door>() != null;
        }
        return false;
    }

    public bool IsInRoom()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, Physics.AllLayers, QueryTriggerInteraction.Collide))
        {
            // If we hit a TILE, we are in a hallway, NOT a room
            if (hit.collider.GetComponent<Tile>() != null && hit.collider.GetComponent<Door>() == null)
            {
                return false;
            }

            // If we hit something that has a Room script (and it's not just a parent of a tile)
            Room r = hit.collider.GetComponentInParent<Room>();
            if (r != null)
            {
                // Optional: Ensure the room script isn't on a parent of the board itself
                // Debug.Log($"Movement: Actually inside room: {r.myName}");
                return true;
            }
        }
        return false;
    }

    // Moves client side to the room of the door
    [ClientRpc]
    private void moveToRoomClientRpc(Vector3 roomPos)
    {
        if (!IsOwner) return;
    
        transform.position = roomPos;
        targetPosition = roomPos;
        isMoving = false; 
        onWhite = false;

        stage = null; 
        whereWeAt(); 

        StartCoroutine(delayedExitList());
    }

    private IEnumerator delayedExitList()
    {
        Debug.Log("Movement: delayedExitList() called");
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
        Debug.Log("Movement: submitEntryServerRpc() called");
        CancelInvoke("delayNextTurn");
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
    private void exitRoomClientRpc(Vector3 exitPos)
    {
        Debug.Log("Movement: exitRoomClientRpc");
        targetPosition = exitPos;
        isMoving = true;
        whereWeAt();
    }

    // Submits exit request to the server.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void submitExitServerRpc(ulong doorId)
    {
        if (whomst.whosPlaying.Value != OwnerClientId) return;

        // Find the NetworkObject by its ID on the server
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(doorId, out NetworkObject doorNetworkObject))
        {
            Door exit = doorNetworkObject.GetComponent<Door>();
            if (exit != null)
            {
                move_tokens.Value--;
                exitRoomClientRpc(exit.transform.position);
                Debug.Log("The exit button hath been pressed");
            }
        }
        whereWeAt();
    }

    [ServerRpc]
    public void activateSecPassServerRpc()
    {
        CancelInvoke("delayNextTurn");
        // 1. Map the connections
        Dictionary<string, string> passages = new Dictionary<string, string>
        {
            { "Study", "Kitchen" },
            { "Kitchen", "Study" },
            { "Conservatory", "Lounge" },
            { "Lounge", "Conservatory" }
        };
        string currentNameStr = currentRoomName.Value.ToString();

        if (passages.ContainsKey(currentNameStr))
        {
            
            string targetRoomName = passages[currentNameStr];
        
            // 2. Find any Door that belongs to the destination room
            Door targetDoor = FindDoorForRoom(targetRoomName);

            if (targetDoor != null)
            {

                Vector3 targetPos = targetDoor.GetRoomPosition((int)OwnerClientId);

                // 5. Authoritative Move
                transform.position = targetPos;
                moveToRoomClientRpc(targetPos);
            
                move_tokens.Value = 0;
            
                if (whomst.whatPhase.Value == TurnStage.MOVING)
                {
                    whomst.pushNextPhase();
                }
            }
            else
            {
                Debug.LogError($"SecretPassage: Could not find a Door associated with {targetRoomName}");
            }
        }
    }

    private Door FindDoorForRoom(string targetRoom)
    {
        Door[] allDoors = GameObject.FindObjectsByType<Door>(FindObjectsSortMode.None);
    
        foreach (Door d in allDoors)
        {
            // Option 1: Check the new string variable (Most Reliable)
            if (d.roomName == targetRoom)
            {
                return d;
            }
        }
        return null;
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
        if (!IsServer)
        {
            reqTurnChangeServerRpc();
        }
        else
        {
            forceNextTurn();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void reqTurnChangeServerRpc()
    {
        forceNextTurn();
    }

    private void forceNextTurn()
    {
        if (whomst == null) whomst = GameObject.FindFirstObjectByType<TurnManager>();
    
        // Safety: Only end turn if we aren't already suggesting
        if (whomst != null && whomst.whatPhase.Value != TurnStage.SUGGESTING)
        {
            whomst.nextTurn();
        }
    }

    public void AIclick(GameObject tileToTraverse)
    {
        if (!IsServer) return;

        Tile clickedTile = tileToTraverse.GetComponent<Tile>();
        if (clickedTile == null || stage == null) return;

        bool isWhite = tileToTraverse.GetComponent<White>() != null;
        requestMoveServerRpc(clickedTile.getTopPosition(), isWhite);
    }

     public void AIMove(GameObject targetTileObject)
    {
        if (targetTileObject == null) return;


        Tile clickedTile = targetTileObject.GetComponent<Tile>();
        if (clickedTile == null || stage == null) return;

        if (clickedTile.occupied.Value || move_tokens.Value <= 0) return;

        Tile currentStand = stage.GetComponent<Tile>();
        if (currentStand.neighbours.Contains(targetTileObject))
        {
            bool isWhite = targetTileObject.GetComponent<White>() != null;
            bool isBlack = targetTileObject.GetComponent<Black>() != null;

            if ((isWhite && !onWhite) || (isBlack && onWhite))
            {
                requestMoveServerRpc(clickedTile.getTopPosition(), isWhite);
            }
        }
    }

    [ClientRpc]
    public void SetPlayerVisibilityClientRpc(bool isVisible)
    {
        // 1. Handle Mesh Renderers (including children)
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = isVisible;
        }

        // 2. Handle Colliders (so players can walk through the ghost)
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = isVisible;
        }

        // 3. Handle Canvas/UI (if the player has a name tag floating over them)
        foreach (var canvas in GetComponentsInChildren<Canvas>())
        {
            canvas.enabled = isVisible;
        }
    
        Debug.Log($"<color=orange>Visibility set to {isVisible} for {gameObject.name}</color>");
    }

}