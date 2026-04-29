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

        Debug.Log($"<color=yellow>[AI BRAIN] {characterScript.charName} (ID {characterScript.botID.Value}) is starting its turn.</color>");

        if (tm.whatPhase.Value == TurnStage.ROLLING)
        {
            dice = FindFirstObjectByType<Rolling>();
            yield return new WaitForSeconds(2.0f);
            dice.callRoll();
            /*
            int roll = Random.Range(2, 13);

            moveScript.setMovesServerRpc(roll);
            yield return new WaitForSeconds(1.0f);

            tm.pushNextPhase();
            */


        }


        while (tm.whatPhase.Value == TurnStage.MOVING && moveScript.move_tokens.Value > 0)
        {
            yield return new WaitForSeconds(0.8f);

            if (moveScript.stage == null)
            {
                Debug.Log("[AI] Stage is null. Searching for nearest tile...");
                FindStartingTile();
                yield return new WaitForSeconds(0.2f);
                if (moveScript.stage == null) break; 
            }


            if (moveScript.IsOnDoor())
            {
                moveScript.submitEntryServerRpc(moveScript.stage.GetComponent<NetworkObject>().NetworkObjectId);
                break;
            }

            Tile currentTile = moveScript.stage.GetComponent<Tile>();
            if (currentTile != null && currentTile.neighbours.Count > 0)
            {
                GameObject target = currentTile.neighbours[Random.Range(0, currentTile.neighbours.Count)];
                moveScript.AIMove(target);
            }
        }

        if (tm.whatPhase.Value == TurnStage.SUGGESTING)
        {
            yield return new WaitForSeconds(2.0f);


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