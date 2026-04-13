using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.UI;
using turnyWurny;
using System.Collections;
using CardList;
using System.Linq;
using System;
using System.Collections.Generic;

public class UIController : MonoBehaviour
{
    // Text for number of moves
    public TextMeshProUGUI diceResult;
    public TextMeshProUGUI moves;
    // Button to action rolling "dice"
    public Button rollBtn;
    // TurnManager object
    public TurnManager turnMan;

    // Drop downs for suggestions
    public TMP_Dropdown suspectList;
    public TMP_Dropdown weaponList;
    public TMP_Dropdown locationList;

    // Fields for room entry/exit
    public TMP_Dropdown exitList;
    public TextMeshProUGUI exitText;
    public Button exitButton;
    public Button entryButton;
    private List<Door> currentDoors = new List<Door>();
    private List<string> exitNames = new List<string>();

    // Instance of this UI controller
    public static UIController Instance;

    // Text displaying players hand
    public TextMeshProUGUI handText;
    public TMP_Dropdown handList;

    public Movement localPlayerScript;


    // UI panels for each different phase
    [Header("Panels")]
    [SerializeField] private GameObject rollPanel;  
    [SerializeField] private GameObject movePanel;  
    [SerializeField] private GameObject guessPanel; 

    public Who selectedSuspect;
    public What selectedWeapon;
    public Where selectedRoom;


    public void ShowSuggestionUI() => guessPanel.SetActive(true);
    public void HideSuggestionUI() => guessPanel.SetActive(false);
    public void ShowRollingUI() => rollPanel.SetActive(true);
    public void HideRollingUI() => rollPanel.SetActive(true);

    private void Awake()
    {
        Instance = this;
    }

    public void updateHand(Card[] cards)
    {
        handText.text = "<b>YOUR HAND:</b>\n";
        List<string> cardNames = new List<string>();
        foreach (Card card in cards)
        {
            cardNames.Add(whatCard(card));
        }
        handList.AddOptions(cardNames);
    }

    private string whatCard(Card card)
    {
        return card.type switch
        {
            Card.CardType.Suspect => ((Who)card.value).ToString(),
            Card.CardType.Weapon => ((What)card.value).ToString(),
            Card.CardType.Room   => ((Where)card.value).ToString(),
            _=> "Unknown"
        };
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (localPlayerScript != null)
        {
            localPlayerScript.move_tokens.OnValueChanged += (oldVal, newVal) => updateMoveText();
        }
        fillGuessDropdowns();
        turnMan.whatPhase.OnValueChanged += (oldVal, newVal) => UpdateUIVisibility();
        turnMan.whosPlaying.OnValueChanged += (oldVal, newVal) => UpdateUIVisibility();
        UpdateUIVisibility();
    }

    public void UpdateUIVisibility()
    {
        bool isMyTurn = (turnMan.whosPlaying.Value == (int)NetworkManager.Singleton.LocalClientId);
        bool isOnDoor = localPlayerScript != null && localPlayerScript.IsOnDoor();
        bool isInRoom = localPlayerScript != null && localPlayerScript.IsInRoom();
        bool isMovingPhase = turnMan.whatPhase.Value == TurnStage.MOVING;

        // By explicitly setting the Active state based on the boolean result,
        // you guarantee they turn off when the condition is not met.
        rollPanel.SetActive(isMyTurn && turnMan.whatPhase.Value == TurnStage.ROLLING);
        movePanel.SetActive(isMyTurn && isMovingPhase);
        guessPanel.SetActive(isMyTurn && turnMan.whatPhase.Value == TurnStage.SUGGESTING);

        moves.gameObject.SetActive(isMovingPhase);
        // Explicitly hide the entry button if not on a door or not moving phase
        entryButton.gameObject.SetActive(isMyTurn && isMovingPhase && isOnDoor);
        exitList.gameObject.SetActive(isMyTurn && isMovingPhase && isInRoom);
        exitText.gameObject.SetActive(isMyTurn && isMovingPhase && isInRoom);
        exitButton.gameObject.SetActive(isMyTurn && isMovingPhase && isInRoom);
    }

    public void delayedUI()
    {
        Invoke("changeUI", 1f);
    }

    private void changeUI()
    {
        // Hide everything first
        rollPanel.SetActive(false);
        movePanel.SetActive(false);
        guessPanel.SetActive(false);

        // Show only the current phase
        switch (turnMan.whatPhase.Value)
        {
            case TurnStage.ROLLING:
                rollPanel.SetActive(true);
                break;
            case TurnStage.MOVING:
                movePanel.SetActive(true);
                break;
            case TurnStage.SUGGESTING:
                guessPanel.SetActive(true);
                break;
        }
    }

    // -----V----- Used for tracking moves ---------V-------
    public void updateMoveText()
    {
        if (localPlayerScript != null)
        {
            // Use $ for interpolation, and access .Value for the NetworkVariable
            moves.text = $"You have {localPlayerScript.move_tokens.Value} moves left!";
        }
    }

    // -----V----- Used for room entry/exit --------V--------

    public void submitEnterRoom()
    {
        if (localPlayerScript != null)
        {
            localPlayerScript.submitEntryServerRpc();
        }
        entryButton.gameObject.SetActive(false);
        exitDropdown();
        exitList.gameObject.SetActive(true);
        exitText.gameObject.SetActive(true);
        exitButton.gameObject.SetActive(true);
    }

    public void exitDropdown()
    {
        if (localPlayerScript == null) return;

        Door[] allDoors = GameObject.FindObjectsByType<Door>(FindObjectsSortMode.None);
    
        exitNames.Clear();
        currentDoors.Clear();

        foreach (var door in allDoors)
        {
            if (door.roomName == localPlayerScript.currentRoomName) 
            {
                currentDoors.Add(door);
                exitNames.Add(door.exitName);   
            }
        }

        exitList.ClearOptions();
        exitList.AddOptions(exitNames);
    }

    public void clearExit()
    {
        if (localPlayerScript == null) return;
        exitList.gameObject.SetActive(false);
        exitText.gameObject.SetActive(false);
        exitButton.gameObject.SetActive(false);

    }

    public void confirmExit()
    {
        Debug.Log("UIController: confirmExit called!");
        int selectedIndex = exitList.value;
        if (localPlayerScript != null)
        {
            if (selectedIndex >= 0 && selectedIndex < currentDoors.Count)
            {
                Door exitDoor = currentDoors[selectedIndex];
            
                // Get the NetworkObject attached to the door
                NetworkObject doorNetObj = exitDoor.GetComponent<NetworkObject>();
            
                if (doorNetObj != null)
                {
                    Debug.Log("UIController: Sending RPC to server for door: " + exitDoor.name);
                    localPlayerScript.submitExitServerRPC(doorNetObj.NetworkObjectId);
                }
                else
                {
                    Debug.LogError("UIController: Door has no NetworkObject component!");
                }
            }
            clearExit();

        }
    }

    // -----V----- Used for suggestion phase dropdowns -----V-----
    void fillGuessDropdowns()
    {
        
        suspectList.ClearOptions();
        weaponList.ClearOptions();
        locationList.ClearOptions();

        List<string> whoOptions = new List<string>(Enum.GetNames(typeof(Who)));
        List<string> whatOptions = new List<string>(Enum.GetNames(typeof(What)));
        List<string> whereOptions = new List<string>(Enum.GetNames(typeof(Where)));


        suspectList.AddOptions(whoOptions);
        weaponList.AddOptions(whatOptions);
        locationList.AddOptions(whereOptions);

    }

    public void clearGuessDropdowns()
    {
        suspectList.value = 0;
        weaponList.value = 0;
        locationList.value = 0;
    
        suspectList.RefreshShownValue();
        weaponList.RefreshShownValue();
        locationList.RefreshShownValue();
    }

    public void suggestionButton()
    {
        int suspectIndex = suspectList.value;
        string suspectName = suspectList.options[suspectIndex].text;

        int weaponIndex = weaponList.value;
        string weaponName = weaponList.options[weaponIndex].text;

        int roomIndex = locationList.value;
        string roomName = locationList.options[roomIndex].text;
        
        selectedSuspect = (Who)suspectIndex;
        selectedWeapon = (What)weaponIndex;
        selectedRoom =  (Where)roomIndex;
    }

    

}
