using UnityEngine;
using Unity.Netcode;
using turnyWurny;
using System.Collections;
using CardList;

public class AIPLayer : NetworkBehaviour
{
    private Movement moveScript;
    private Character characterScript;
    private TurnManager tm;
    private bool isThinking = false;
    public GameObject stage; 
    private Rolling dice;
    private float accusationChance = 0.0001f;

    // Initialises Movement and Character scripts and for the AI Player.
    void Start()
    {
        moveScript = GetComponent<Movement>();
        characterScript = GetComponent<Character>();
        tm = FindFirstObjectByType<TurnManager>();
    }

    // Checks the requirements have been found and updates isMyTurn when its turn has been reached.
    void Update()
    {
        if (!IsServer) return;

        if (characterScript == null)
        {
            return;
        }

        if (!characterScript.isRobot.Value) return;

        if (characterScript.botID.Value == -1)
        {
            return;
        }
        if (tm == null) return;

        bool isMyTurn = ((ulong)characterScript.botID.Value == tm.whosPlaying.Value);

        // If its the AIs turn, starts coroutine of its actions.
        if (isMyTurn && !isThinking)
        {
            isThinking = true;
            StartCoroutine(AIRoutine(tm));
        }
    }

    // The actual behaviour coded into the AI for each specific phase.
    IEnumerator AIRoutine(TurnManager tm)
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));

        // ---- ROLLING PHASE ----
        // Calls Rolling script to generate move tokens.
        if (tm.whatPhase.Value == TurnStage.ROLLING)
        {
            dice = FindFirstObjectByType<Rolling>();
            yield return new WaitForSeconds(2.0f);
            dice.callRoll();

            isThinking = false;
            yield break;
        }

        // ---- MOVING PHASE ----
        while (tm.whatPhase.Value == TurnStage.MOVING && moveScript.move_tokens.Value > 0)
        {
            yield return new WaitForSeconds(0.8f);
            // Finds tile under AI if stage is not found
            if (moveScript.stage == null)
            {
                FindStartingTile();
                yield return new WaitForSeconds(0.2f);
                if (moveScript.stage == null) break;
            }

            // If AI is on a door, allows them to move into the room authorised on server.
            if (moveScript.IsOnDoor())
            {
                moveScript.submitEntryServerRpc(moveScript.stage.GetComponent<NetworkObject>().NetworkObjectId);
                yield return new WaitForSeconds(1.0f);
                break;
            }

            // Tries all nearby tiles until move is made, and while move tokens are over 0.
            Tile currentTile = moveScript.stage.GetComponent<Tile>();
            bool moveMade = false;
            if (currentTile != null && currentTile.neighbours.Count > 0)
            {
                while (!moveMade)
                {
                    GameObject target = currentTile.neighbours[Random.Range(0, currentTile.neighbours.Count)];
                    if (moveScript.onWhite && target.GetComponent<Black>() != null || !moveScript.onWhite && target.GetComponent<White>() != null)
                    {
                        moveScript.AIMove(target);
                    }
                    moveMade = true;
                }
            }
        }

        // If move tokens run out while it is AI's moving phase, pushes to next phase.
        if (tm.whatPhase.Value == TurnStage.MOVING && moveScript.move_tokens.Value == 0)
        {
            tm.pushNextPhase();
            yield return new WaitForSeconds(1.0f);

        }

        if (tm.whatPhase.Value == TurnStage.SUGGESTING)
        {
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
                tm.pushNextPhase();
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

    // Finds the closest tile to the AI player and marks it as the stage.
    private void FindStartingTile()
    {
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
            moveScript.stage = closestTile;
        }
    }
}