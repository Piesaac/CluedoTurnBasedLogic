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
    [Header("Links to UI controller")]
    public Button clueSheetBtn;
    public bool cluesheetToggled;
    public GameObject clueSheet;

    // Text for number of moves
    public TextMeshProUGUI diceResult;
    public TextMeshProUGUI moves;

    // Button to action rolling "dice"
    public Button rollBtn;
    
    // TurnManager object
    public TurnManager turnMan;
    public bool isMyTurn;

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

    // Local instance of player's movement script
    public Movement localPlayerScript;

    [Header("Panels")]
    [SerializeField] private GameObject rollPanel;  
    [SerializeField] private GameObject movePanel;  
    [SerializeField] private GameObject guessPanel; 

    [Header("Disproving UI")]
    [SerializeField] private GameObject disprovePanel;
    [SerializeField] private TMP_Dropdown disproveList;
    [SerializeField] public TextMeshProUGUI disproveText;
    public CardDistributor cardDist;
    List<string> cardNames;

    [Header("Dropdown Fields")]
    public Who selectedSuspect;
    public What selectedWeapon;
    public Where selectedRoom;

    public Who accuseWho;
    public What accuseWhat;
    public Where accuseWhere;

    public void ShowSuggestionUI() => guessPanel.SetActive(true);
    public void HideSuggestionUI() => guessPanel.SetActive(false);
    public void ShowRollingUI() => rollPanel.SetActive(true);
    public void HideRollingUI() => rollPanel.SetActive(false);
    public void HideDisproveUI() => disprovePanel.SetActive(false);

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        cluesheetToggled = false;
        clueSheetBtn.gameObject.SetActive(true);
        fillGuessDropdowns();

        // Subscribe to turn changes to refresh UI automatically
        if (turnMan != null)
        {
            turnMan.whatPhase.OnValueChanged += (oldVal, newVal) => UpdateUIVisibility();
            turnMan.whosPlaying.OnValueChanged += (oldVal, newVal) => UpdateUIVisibility();
        }
        
        UpdateUIVisibility();
    }

    void Update()
    {
        // Continuous check for room entry buttons while in moving phase
        if (localPlayerScript != null && isMyTurn && turnMan.whatPhase.Value == TurnStage.MOVING)
        {
            bool isOnDoor = localPlayerScript.IsOnDoor();
        
            // DEBUG: This will tell us exactly why it's not showing
            if (isOnDoor && !entryButton.gameObject.activeSelf) {
                Debug.Log("<color=green>UI: Standing on door, showing button!</color>");
            }

            entryButton.gameObject.SetActive(isOnDoor);
        }
    }

    public void SetLocalPlayer(Movement player)
    {
        localPlayerScript = player;
        updateMoveText(); 
        localPlayerScript.move_tokens.OnValueChanged += (oldVal, newVal) => updateMoveText();
        UpdateUIVisibility();
    }

    public void UpdateUIVisibility()
    {
        // 1. Determine Identity and Phase
        isMyTurn = (turnMan.whosPlaying.Value == (int)NetworkManager.Singleton.LocalClientId);
        TurnStage currentPhase = turnMan.whatPhase.Value;
        
        bool isMovingPhase = currentPhase == TurnStage.MOVING;
        bool isInRoom = localPlayerScript != null && localPlayerScript.IsInRoom();
        bool isOnDoor = localPlayerScript != null && localPlayerScript.IsOnDoor();

        int myID = (int)NetworkManager.Singleton.LocalClientId;
        int activeID = turnMan.whosPlaying.Value;

        isMyTurn = (myID == activeID);

        // Force the Move Panel to stay active if it's the moving phase
        movePanel.SetActive(isMyTurn && turnMan.whatPhase.Value == TurnStage.MOVING);

        // 2. Clear panels initially
        HideDisproveUI();

        // 3. Main Panel Logic
        rollPanel.SetActive(isMyTurn && currentPhase == TurnStage.ROLLING);
        movePanel.SetActive(isMyTurn && isMovingPhase);
        guessPanel.SetActive(isMyTurn && currentPhase == TurnStage.SUGGESTING);

        // 4. Detailed Element Logic (Room Entry/Exit)
        moves.gameObject.SetActive(isMyTurn && isMovingPhase);
        
        // Entry button logic: Only if it's my turn, moving phase, and standing on a door
        entryButton.gameObject.SetActive(isMyTurn && isMovingPhase && isOnDoor);

        // Exit UI logic: Only if it's my turn, moving phase, and already inside a room
        bool showExitUI = isMyTurn && isMovingPhase && isInRoom;
        exitList.gameObject.SetActive(showExitUI);
        exitText.gameObject.SetActive(showExitUI);
        exitButton.gameObject.SetActive(showExitUI);

        if (showExitUI) exitDropdown();
    }

    public void delayedUI()
    {
        Invoke("UpdateUIVisibility", 1f);
    }

    public void updateMoveText()
    {
        if (localPlayerScript != null)
        {
            moves.text = $"You have {localPlayerScript.move_tokens.Value} moves left!";
        }
    }

    public void submitEnterRoom()
    {
        if (localPlayerScript != null)
        {
            localPlayerScript.whereWeAt();
            NetworkObject stageNet = localPlayerScript.stage.GetComponent<NetworkObject>();
            if (stageNet != null)
            {
                localPlayerScript.submitEntryServerRpc(stageNet.NetworkObjectId);
            }
        }
        
        // Immediately update visibility to swap Entry button for Exit/Suggestion UI
        UpdateUIVisibility();
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

    public void confirmExit()
    {
        int selectedIndex = exitList.value;
        if (localPlayerScript != null && selectedIndex >= 0 && selectedIndex < currentDoors.Count)
        {
            Door exitDoor = currentDoors[selectedIndex];
            NetworkObject doorNetObj = exitDoor.GetComponent<NetworkObject>();
            
            if (doorNetObj != null)
            {
                localPlayerScript.submitExitServerRPC(doorNetObj.NetworkObjectId);
            }
        }
        UpdateUIVisibility();
    }

    public void updateHand(Card[] cards)
    {
        if (handList == null) return;
        
        handList.ClearOptions();
        List<string> names = new List<string>();
        foreach (Card card in cards)
        {
            names.Add(whatCard(card));
        }
        handList.AddOptions(names);
        
        if (handText != null) handText.text = "<b>YOUR HAND:</b>";
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

    void fillGuessDropdowns()
    {
        suspectList.ClearOptions();
        weaponList.ClearOptions();
        locationList.ClearOptions();

        suspectList.AddOptions(new List<string>(Enum.GetNames(typeof(Who))));
        weaponList.AddOptions(new List<string>(Enum.GetNames(typeof(What))));
        locationList.AddOptions(new List<string>(Enum.GetNames(typeof(Where))));
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
        selectedSuspect = (Who)suspectList.value;
        selectedWeapon = (What)weaponList.value;
        selectedRoom = (Where)locationList.value;
        HideSuggestionUI();
    }

    public void ShowDisprovePanel(Card[] cards)
    {
        cardNames = new List<string>();
        disproveList.ClearOptions();
        foreach (Card card in cards)
        {
            cardNames.Add(cardDist.whatCard(card));
        }
        disproveList.AddOptions(cardNames);
        disprovePanel.SetActive(true);
    }

    public void confirmDisprove()
    {
        if (cardNames != null && cardNames.Count > 0)
        {
            string clueToShow = cardNames[disproveList.value];
            GuessManager.Instance.disproveResult(clueToShow);
        }
        HideDisproveUI();
    }

    public void hideDisproveText()
    {
        disproveText.gameObject.SetActive(false);
        disproveText.text = "";
        GuessManager.Instance.endDisprove();
    }

    public void cluePopUp()
    {
        cluesheetToggled = !cluesheetToggled;
        clueSheet.SetActive(cluesheetToggled);
    }
}