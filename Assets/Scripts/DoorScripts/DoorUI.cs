using UnityEngine;
using TMPro;  
using Unity.Netcode;
using turnyWurny;
public class DoorUI : MonoBehaviour
{
    public TextMeshProUGUI doorNotif;
    public GameObject entryButton;
    public TurnManager turnMan;
    public UIController uiMan;

    public bool isDoorVisible = false; 

    void Start()
    {
        // Subscribe to turn changes so it cleans itself up when the turn ends
        turnMan.whosPlaying.OnValueChanged += (oldVal, newVal) => UpdateVisibility();
    }

    // Call this from your logic when a door is found/lost
    public void SetDoorFound(bool found)
    {
        isDoorVisible = found;
        UpdateVisibility();
    }

    public void UpdateVisibility()
    {
        // 1. Get the current active player ID
        int activePlayerId = turnMan.whosPlaying.Value;
    
        // 2. Get the local player's ID
        int myId = (int)NetworkManager.Singleton.LocalClientId;
    
        // 3. Only show if it IS my turn AND the door condition is met
        // Add your "isDoorFound" condition here
        bool isMyTurn = (activePlayerId == myId);
        bool shouldShow = isMyTurn && isDoorVisible; 

        doorNotif.gameObject.SetActive(shouldShow);
        entryButton.gameObject.SetActive(shouldShow);
    }   
}