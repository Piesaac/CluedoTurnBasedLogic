using UnityEngine;
using Unity.Netcode;
using turnyWurny;
using System.Collections;
using CardList;

public class AIPLayer : NetworkBehaviour
{
    private Movement moveScript;
    private Character characterScript;
    private bool isThinking = false;
    public GameObject stage; 
    private Rolling dice;
    private float accusationChance = 0.1f;

    void Start()
    {
        moveScript = GetComponent<Movement>();
        characterScript = GetComponent<Character>();
    }

    void Update()
    {
        if (!IsServer) return;

        if (characterScript == null)
        {
            Debug.Log("<color=red>[AI DEBUG] Component missing on " + gameObject.name + "</color>");
            return;
        }

        if (!characterScript.isRobot.Value) return;

        if (characterScript.botID.Value == -1)
        {
            Debug.Log("<color=orange>[AI DEBUG] I am a robot, but my botID is still -1!</color>");
            return;
        }

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

        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));

        if (tm.whatPhase.Value == TurnStage.ROLLING)
        {
            dice = FindFirstObjectByType<Rolling>();
            yield return new WaitForSeconds(2.0f);
            dice.callRoll();

            isThinking = false;
            yield break;
        }

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
            tm.pushNextPhase();
            yield return new WaitForSeconds(1.0f);
            //still moves to suggestion phase even though it may be able to

        }

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
            Debug.Log($"[AI] Assigned starting stage to: {closestTile.name}");
        }
    }
}