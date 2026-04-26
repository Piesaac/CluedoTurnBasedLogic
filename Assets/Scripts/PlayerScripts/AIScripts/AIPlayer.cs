using UnityEngine;
using Unity.Netcode;
using turnyWurny;
using System.Collections;

public class AIPlayer : NetworkBehaviour
{
    private Movement movementScript;
    private Character characterScript;
    private TurnManager turnMan;
    private bool roboToggle = false;
    public bool aiFlag = false;

    void Start()
    {
        movementScript = GetComponent<Movement>();
        characterScript = GetComponent<Character>();
        turnMan = GameObject.FindFirstObjectByType<TurnManager>();
        if (characterScript.isRobot.Value == true)
        {
            aiFlag = true;
        }
    }

    void Update()
    {
        if (!IsServer) return;
        if (characterScript == null) return;
        if (!aiFlag) return;

        if (turnMan.whosPlaying.Value == (int)OwnerClientId && aiFlag == true)
        {
            if (!roboToggle) StartCoroutine(AITurnReq());
        }
    }

    IEnumerator AITurnReq()
    {
        roboToggle = true;
        yield return new WaitForSeconds(1f);

        if (turnMan.whatPhase.Value == TurnStage.ROLLING)
        {
            movementScript.setMovesServerRpc(Random.Range(1, 13));
            yield return new WaitForSeconds(1f);
            turnMan.pushNextPhase(); 
        }

        while (turnMan.whatPhase.Value == TurnStage.MOVING && movementScript.move_tokens.Value > 0)
        {
            yield return new WaitForSeconds(0.5f);
            
            if (movementScript.nearby.Count > 0)
            {
                GameObject randomTile = movementScript.nearby[Random.Range(0, movementScript.nearby.Count)];
                movementScript.AIclick(randomTile);
            }
            else { break; }
        }

        if (turnMan.whatPhase.Value == TurnStage.SUGGESTING)
        {
            yield return new WaitForSeconds(1f);
            turnMan.pushNextPhase();
        }

        roboToggle = false;
    }
}