using UnityEngine;
using Unity.Netcode;
using turnyWurny;
using System.Collections;
using CardList;

public class SimpleAIController : NetworkBehaviour
{
    private Movement moveScript;
    private Character characterScript;
    private bool isThinking = false;
    public GameObject stage; 

    void Start()
    {
        // Get references to the movement and identity components on this prefab
        moveScript = GetComponent<Movement>();
        characterScript = GetComponent<Character>();
    }

    void Update()
    {
        if (!IsServer) return; // AI only runs on the Host

        // 1. Check if component exists
        if (characterScript == null)
        {
            Debug.Log("<color=red>[AI DEBUG] Component missing on " + gameObject.name + "</color>");
            return;
        }

        // 2. Check if it knows it's a Robot
        if (!characterScript.isRobot.Value) return;

        // 3. Check the ID assignment
        if (characterScript.botID.Value == -1)
        {
            // If you see this, the PlayerSpawner failed to give the bot an ID
            Debug.Log("<color=orange>[AI DEBUG] I am a robot, but my botID is still -1!</color>");
            return;
        }

        TurnManager tm = FindFirstObjectByType<TurnManager>();
        if (tm == null) return;

        // 4. Check the Turn Match
        bool isMyTurn = (characterScript.botID.Value == tm.whosPlaying.Value);

        if (isMyTurn && !isThinking)
        {
            StartCoroutine(AIRoutine(tm));
        }
    }

    IEnumerator AIRoutine(TurnManager tm)
    {
        isThinking = true;

        // HEARTBEAT LOG: This confirms the bot has recognized its turn
        Debug.Log($"<color=yellow>[AI BRAIN] {characterScript.charName} (ID {characterScript.botID.Value}) is starting its turn.</color>");

        // --- PHASE: ROLLING ---
        if (tm.whatPhase.Value == TurnStage.ROLLING)
        {
            yield return new WaitForSeconds(2.0f);
            int roll = Random.Range(2, 13);

            // Set the move tokens on the server
            moveScript.setMovesServerRpc(roll);
            yield return new WaitForSeconds(1.0f);

            // Tell TurnManager to move to the MOVING phase
            tm.pushNextPhase();
        }

        // --- PHASE: MOVING ---
        while (tm.whatPhase.Value == TurnStage.MOVING && moveScript.move_tokens.Value > 0)
        {
            yield return new WaitForSeconds(0.8f);

            // FIX: If the bot doesn't know what tile it's on, find the nearest one
            if (moveScript.stage == null)
            {
                Debug.Log("[AI] Stage is null. Searching for nearest tile...");
                FindStartingTile();
                yield return new WaitForSeconds(0.2f);
                if (moveScript.stage == null) break; // Still null? Stop to avoid crash
            }

            // 1. Check for doors
            if (moveScript.IsOnDoor())
            {
                moveScript.submitEntryServerRpc(moveScript.stage.GetComponent<NetworkObject>().NetworkObjectId);
                break;
            }

            // 2. Movement logic
            Tile currentTile = moveScript.stage.GetComponent<Tile>();
            if (currentTile != null && currentTile.neighbours.Count > 0)
            {
                GameObject target = currentTile.neighbours[Random.Range(0, currentTile.neighbours.Count)];
                moveScript.AIMove(target);
            }
        }

        // --- PHASE: SUGGESTING ---
        if (tm.whatPhase.Value == TurnStage.SUGGESTING)
        {
            yield return new WaitForSeconds(2.0f);

            // AI picks random card indices
            Who who = (Who)Random.Range(0, 6);
            What what = (What)Random.Range(0, 6);
            Where where = (Where)Random.Range(0, 9);

            Debug.Log($"[AI] Suggesting: {who} with the {what} in the {where}");
            GuessManager.Instance.submitGuessServerRpc(who, what, where);
        }

        isThinking = false;
    }

    private void FindStartingTile()
    {
        // Find every tile in the scene
        Tile[] allTiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
        float closestDist = float.MaxValue;
        GameObject closestTile = null;

        foreach (Tile t in allTiles)
        {
            float dist = Vector3.Distance(transform.position, t.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestTile = t.gameObject;
            }
        }

        if (closestTile != null)
        {
            moveScript.stage = closestTile; // Manually assign the missing reference
            Debug.Log($"[AI] Assigned starting stage to: {closestTile.name}");
        }
    }
}