using CardList;
using System.Collections;
using turnyWurny;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;


public class AIPLayer : NetworkBehaviour
{
    private Movement moveScript;
    private Character characterScript;
    private bool isThinking = false;
    public GameObject stage;
    private Rolling dice;
    //accusation chance, 0.1 = 10%
    //can use 1 for testing purposes
    //no longer a SerializeField because we want to be able to change the accusation chance in here, rather than in all the prefabs
    private float accusationChance = 0.1f;

    void Start()
    {
        moveScript = GetComponent<Movement>();
        characterScript = GetComponent<Character>();
    }

    void Update()
    {
        if (!IsServer) return;

        if (characterScript == null || !characterScript.isRobot.Value) return;

        if (characterScript.botID.Value == -1) return;

        TurnManager tm = FindFirstObjectByType<TurnManager>();
        if (tm == null) return;

        bool isMyTurn = ((ulong)characterScript.botID.Value == tm.whosPlaying.Value);

        if (isMyTurn && !isThinking)
        {
            StartCoroutine(AIRoutine(tm));
        }
    }

    IEnumerator AIRoutine(TurnManager tm)
    {
        isThinking = true;

        //give the game a second to breathe
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));

        Debug.Log($"<color=yellow>[AI BRAIN] {characterScript.charName} is taking over.</color>");

        // --- PHASE: ROLLING ---
        if (tm.whatPhase.Value == TurnStage.ROLLING)
        {
            dice = FindFirstObjectByType<Rolling>();
            yield return new WaitForSeconds(2.0f);
            dice.callRoll();

            //break here because the dice roll will trigger a phase change automatically
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

        // If the bot finishes moving in a hallway, it needs to tell the game to move on
        if (tm.whatPhase.Value == TurnStage.MOVING && moveScript.move_tokens.Value == 0)
        {
            Debug.Log("Out of moves in the hallway, skipping to the next phase.");
            tm.pushNextPhase();
            yield return new WaitForSeconds(1.0f);
            //still moves to suggestion phase even though it may be able to

        }

        // --- PHASE: SUGGESTING ---
        if (tm.whatPhase.Value == TurnStage.SUGGESTING)
        {
            //only try to suggest if we actually made it into a room
            if (moveScript.IsInRoom())
            {
                yield return new WaitForSeconds(2.0f);
                Who who = (Who)Random.Range(0, 6);
                What what = (What)Random.Range(0, 6);
                Where where = (Where)Random.Range(0, 9);

                Debug.Log($"[AI] Suggesting: {who} with {what} in {where}");
                GuessManager.Instance.submitGuessServerRpc(who, what, where);

                //give the players a moment to show their cards
                yield return new WaitForSeconds(4.0f);
            }
            else
            {
                //not in room therefore no suggestion
                Debug.Log("Not in a room, so I can't suggest anything. Moving on.");
                tm.pushNextPhase();
                yield return new WaitForSeconds(1.0f);
            }
        }

        // --- RECKLESS ACCUSATION --
        if (Random.value < accusationChance)
        {
            Debug.Log($"<color=red>[AI] {characterScript.charName} is going for the win (or the boot)!</color>");

            Who finalWho = (Who)Random.Range(0, 6);
            What finalWhat = (What)Random.Range(0, 6);
            Where finalWhere = (Where)Random.Range(0, 9);

            GuessManager.Instance.submitAccuseServerRpc(finalWho, finalWhat, finalWhere, NetworkObjectId);

            //accusation means we are complete no matter correct or incorrect
            isThinking = false;
            yield break;
        }
        else
        {
            //if theres no accusation then we end the turn manually
            if (tm.whatPhase.Value == TurnStage.SUGGESTING)
            {
                Debug.Log("Decided not to accuse. Turn's over.");
                tm.pushNextPhase();
            }
        }

        isThinking = false;
    }
    



private void FindStartingTile()
    {
        //find every tile in the scene
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
            moveScript.stage = closestTile; //manually assign missing reference
            Debug.Log($"[AI] Assigned starting stage to: {closestTile.name}");
        }
    }
}
