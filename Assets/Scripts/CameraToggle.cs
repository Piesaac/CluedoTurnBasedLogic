using UnityEngine;
using turnyWurny;

public class CameraToggle : MonoBehaviour
{
    public Camera UICam;
    public Camera BoardCam;
    public TurnManager turns;

    private TurnStage lastCheckedPhase;

    void Start()
    {
        
        UICam.gameObject.SetActive(true);
        BoardCam.gameObject.SetActive(true);
        BoardCam.enabled = false;
        UICam.enabled = true;
        lastCheckedPhase = turns.whatPhase.Value;
    }

    void Update()
    {
        if (turns.whatPhase.Value != lastCheckedPhase)
        {
            Debug.Log("Phase changed to: " + turns.whatPhase.Value);
            switchCam();
            lastCheckedPhase = turns.whatPhase.Value;
        }
    }

    void switchCam()
    {
        if (turns.whatPhase.Value == TurnStage.MOVING)
        {
        Debug.Log("Switching to Board Cam");
        BoardCam.enabled = true;
        UICam.enabled = false;
        }
        else if (turns.whatPhase.Value == TurnStage.ROLLING || turns.whatPhase.Value == TurnStage.SUGGESTING)
        {
        Debug.Log("Switching to UI Cam");
        BoardCam.enabled = false;
        UICam.enabled = true;
        }
    }
}