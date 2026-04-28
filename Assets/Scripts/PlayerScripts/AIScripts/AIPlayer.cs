using CardList;
using System.Collections;
using turnyWurny;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.ShaderData;

public class AIPLayer : NetworkBehaviour
{
    private Movement moveScript;
    private Character characterScript;
    private bool isThinking = false;
    public GameObject stage; 
    private Rolling dice;

    //chance that the AI makes an accusation (0.1f = 10% chance)
    [SerializeField] private float accusationChance = 1f;

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
        bool isMyTurn = ((ulong)characterScript.botID.Value == tm.whosPlaying.Value);

        if (isMyTurn && !isThinking)
        {
            StartCoroutine(AIRoutine(tm));
        }
    }

    IEnumerator AIRoutine(TurnManager tm)
    {
        isThinking = true;

        // Tiny random delay so they don't act instantly
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));

        Debug.Log($"<color=yellow>[AI BRAIN] {characterScript.charName} starting turn.</color>");

        // --- PHASE: ROLLING ---
        if (tm.whatPhase.Value == TurnStage.ROLLING)
        {
            dice = FindFirstObjectByType<Rolling>();
            yield return new WaitForSeconds(2.0f);
            dice.callRoll();
            // We exit here because the phase will change and Update will restart this routine
            isThinking = false;
            yield break;
        }

        // --- PHASE: MOVING ---
        while (tm.whatPhase.Value == TurnStage.MOVING && moveScript.move_tokens.Value > 0)
        {
            yield return new WaitForSeconds(0.8f);

            if (moveScript.stage == null)
            {
                FindStartingTile();
                yield return new WaitForSeconds(0.2f);
                if (moveScript.stage == null) break;
            }

            if (moveScript.IsOnDoor())
            {
                moveScript.submitEntryServerRpc(moveScript.stage.GetComponent<NetworkObject>().NetworkObjectId);
                yield return new WaitForSeconds(1.0f);
                break;
            }

            Tile currentTile = moveScript.stage.GetComponent<Tile>();
            if (currentTile != null && currentTile.neighbours.Count > 0)
            {
                GameObject target = currentTile.neighbours[Random.Range(0, currentTile.neighbours.Count)];
                moveScript.AIMove(target);
            }
        }

        // --- PHASE: SUGGESTING (Only if in a room) ---
        if (tm.whatPhase.Value == TurnStage.SUGGESTING)
        {
            yield return new WaitForSeconds(2.0f);
            Who who = (Who)Random.Range(0, 6);
            What what = (What)Random.Range(0, 6);
            Where where = (Where)Random.Range(0, 9);

            Debug.Log($"[AI] Suggesting: {who} with {what} in {where}");
            GuessManager.Instance.submitGuessServerRpc(who, what, where);

            // Wait for the suggestion to finish before checking for accusation
            yield return new WaitForSeconds(2.0f);
        }

        // --- THE FIX: RECKLESS ACCUSATION (End of Turn Check) ---
        // We only check for accusation if the AI is DONE moving (0 tokens) 
        // OR it is currently in the Suggestion phase.
        if (moveScript.move_tokens.Value == 0 || tm.whatPhase.Value == TurnStage.SUGGESTING)
        {
            if (Random.value < accusationChance)
            {
                Debug.Log($"<color=red>[AI] {characterScript.charName} making final accusation!</color>");

                Who finalWho = (Who)Random.Range(0, 6);
                What finalWhat = (What)Random.Range(0, 6);
                Where finalWhere = (Where)Random.Range(0, 9);

                GuessManager.Instance.submitAccuseServerRpc(finalWho, finalWhat, finalWhere, NetworkObjectId);

                // If we accuse, we stop the routine here (because we are either winning or kicked)
                isThinking = false;
                yield break;
            }
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