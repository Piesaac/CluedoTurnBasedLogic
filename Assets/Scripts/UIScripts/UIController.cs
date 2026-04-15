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

    // Button to toggle clueSheet
    public Button clueSheetBtn;
    public bool cluesheetToggled;
    // Clue sheet object
    public GameObject clueSheet;

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

    // Local instance of player's movement script, allocated through the script itself.
    public Movement localPlayerScript;




    // UI panels for each different phase
    [Header("Panels")]
    [SerializeField] private GameObject rollPanel;
    [SerializeField] private GameObject movePanel;
    [SerializeField] private GameObject guessPanel;

    // Fields for disproving UI
    [SerializeField] private GameObject disprovePanel;
    [SerializeField] private TMP_Dropdown disproveList;
    public CardDistributor cardDist;

    // Fields for the suggesting dropdowns
    public Who selectedSuspect;
    public What selectedWeapon;
    public Where selectedRoom;

    // Fields for accusation dropdowns
    public Who accuseWho;
    public What accuseWhat;
    public Where accuseWhere;


    // Simple methods for hiding and showing UI panels
    public void ShowSuggestionUI() => guessPanel.SetActive(true);
    public void HideSuggestionUI() => guessPanel.SetActive(false);
    public void ShowRollingUI() => rollPanel.SetActive(true);
    public void HideRollingUI() => rollPanel.SetActive(true);
    public void HideDisproveUI() => disprovePanel.SetActive(false);

    // Sets the global reference to this instance upon the files being loaded
    private void Awake()
    {
        Instance = this;
    }

    // Subscribes to changes in move_tokens from player movement, updating the number of move tokens on the event of a change.
    void Start()
    {
        Debug.Log($"UIController Start: localPlayerScript is {(localPlayerScript != null ? localPlayerScript.name : "NULL")}");
        if (localPlayerScript != null)
        {
            localPlayerScript.move_tokens.OnValueChanged += (oldVal, newVal) => updateMoveText();
        }
        cluesheetToggled = false;
        // Fills the dropdowns for the guess/clue system.
        fillGuessDropdowns();
        turnMan.whatPhase.OnValueChanged += (oldVal, newVal) => UpdateUIVisibility();
        turnMan.whosPlaying.OnValueChanged += (oldVal, newVal) => UpdateUIVisibility();
        UpdateUIVisibility();
    }

    void Update()
    {
        if (localPlayerScript != null)
        {
            // Force update the text every frame to test if data is actually arriving
            moves.text = $"You have {localPlayerScript.move_tokens.Value} moves left!";
        }
    }

    public void SetLocalPlayer(Movement player)
    {
        localPlayerScript = player;
        // 1. Force the first update immediately
        updateMoveText();

        // 2. Subscribe to future updates
        localPlayerScript.move_tokens.OnValueChanged += (oldVal, newVal) => updateMoveText();
    }


<<<<<<< HEAD
    /// -------V-------- Player Hand Scripts -------V--------//
    // Updates players hand dropdown list in UI with cards inputted.
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

    // Returns the string name value of the card inputted.
    private string whatCard(Card card)
    {
        return card.type switch
        {
            Card.CardType.Suspect => ((Who)card.value).ToString(),
            Card.CardType.Weapon => ((What)card.value).ToString(),
            Card.CardType.Room => ((Where)card.value).ToString(),
            _ => "Unknown"
        };
    }

=======
>>>>>>> parent of 0e29a2d (Revert "Updates for guessing logic")

    // --------V-------- UI update/refresh methods --------V--------
    // Updates UI depending on whether it meets the criteria for showing
    public void UpdateUIVisibility()
    {
        // Bools for determining whether a UI should be visible to the player actioning.
        bool isMyTurn = (turnMan.whosPlaying.Value == (int)NetworkManager.Singleton.LocalClientId);
        bool isOnDoor = localPlayerScript != null && localPlayerScript.IsOnDoor();
        bool isInRoom = localPlayerScript != null && localPlayerScript.IsInRoom();
        bool isMovingPhase = turnMan.whatPhase.Value == TurnStage.MOVING;
<<<<<<< HEAD

=======
        HideDisproveUI();
        
>>>>>>> parent of 0e29a2d (Revert "Updates for guessing logic")
        Debug.Log($"UI Debug: MyTurn={isMyTurn}, MovingPhase={isMovingPhase}, OnDoor={isOnDoor}");

        // Shows/Hides the overarching UI panels of the different phases.
        rollPanel.SetActive(isMyTurn && turnMan.whatPhase.Value == TurnStage.ROLLING);
        movePanel.SetActive(isMyTurn && isMovingPhase);
        guessPanel.SetActive(isMyTurn && turnMan.whatPhase.Value == TurnStage.SUGGESTING);

        // Sets the specific UI elements within the panels to hide or show.
        moves.gameObject.SetActive(isMovingPhase);
        entryButton.gameObject.SetActive(isMyTurn && isMovingPhase && isOnDoor);
        exitList.gameObject.SetActive(isMyTurn && isMovingPhase && isInRoom);
        exitText.gameObject.SetActive(isMyTurn && isMovingPhase && isInRoom);
        exitButton.gameObject.SetActive(isMyTurn && isMovingPhase && isInRoom);
    }

    // Delays UI change by 1 second.
    public void delayedUI()
    {
        Invoke("changeUI", 1f);
    }

    // Changes UI panels showing depending on TurnStage.
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

    // Button to confirm room entry.
    public void submitEnterRoom()
    {
        if (localPlayerScript != null)
        {
            localPlayerScript.whereWeAt();
            NetworkObject stageNet = localPlayerScript.stage.GetComponent<NetworkObject>();
            if (stageNet != null )
            {
                localPlayerScript.submitEntryServerRpc(stageNet.NetworkObjectId);
            }
            else
            {
                Debug.LogError("Player does not have a stage component.");
            }
        }
        entryButton.gameObject.SetActive(false);
        exitDropdown();
        exitList.gameObject.SetActive(true);
        exitText.gameObject.SetActive(true);
        exitButton.gameObject.SetActive(true);
    }

    // Fills room exit dropdown with valid doors.
    public void exitDropdown()
    {
        if (localPlayerScript == null) return;

        Door[] allDoors = GameObject.FindObjectsByType<Door>(FindObjectsSortMode.None);

        exitNames.Clear();
        currentDoors.Clear();

        foreach (var door in allDoors)
        {
<<<<<<< HEAD
            if (door.roomName == localPlayerScript.currentRoomName)
=======
            // Print the comparison values clearly
            string doorRoom = door.roomName ?? "NULL";
            string myRoom = localPlayerScript.currentRoomName ?? "NULL";
    
            bool isMatch = (doorRoom == myRoom);
    
            Debug.Log($"Matching? {isMatch} | Door: '{door.name}' room is '{doorRoom}' | My Room is '{myRoom}'");

            if (isMatch) 
>>>>>>> parent of 0e29a2d (Revert "Updates for guessing logic")
            {
                currentDoors.Add(door);
                exitNames.Add(door.exitName);
            }
        }

        exitList.ClearOptions();
        exitList.AddOptions(exitNames);
    }

    // Clears all UI elements related to room exit.
    public void clearExit()
    {
        if (localPlayerScript == null) return;
        exitList.gameObject.SetActive(false);
        exitText.gameObject.SetActive(false);
        exitButton.gameObject.SetActive(false);

    }

    // Links to button to confirm room exit.
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

    /// -------V-------- Player Hand Scripts -------V--------//
    // Updates players hand dropdown list in UI with cards inputted.
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

    // Returns the string name value of the card inputted.
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

    // -----V----- Used for suggestion phase dropdowns -----V-----

    // Fills dropdown for guesses with valid elements.
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

    // clears dropdowns for guesses after the TurnStage has moved on.
    public void clearGuessDropdowns()
    {
        suspectList.value = 0;
        weaponList.value = 0;
        locationList.value = 0;

        suspectList.RefreshShownValue();
        weaponList.RefreshShownValue();
        locationList.RefreshShownValue();
    }

    // Links to button for confirming guess.
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
        selectedRoom = (Where)roomIndex;
    }

<<<<<<< HEAD
    public void cluePopUp()
    {
        if (cluesheetToggled)
        {
            clueSheet.gameObject.SetActive(false);
            cluesheetToggled = false;
            return;
        }
        clueSheet.gameObject.SetActive(true);
        cluesheetToggled = true;
=======

    // --------V-------- Accusation Methods --------V--------

    public void ShowDisprovePanel(Card[] cards)
    {
        List<string> cardNames = new List<string>();
        foreach (Card card in cards)
        {
            cardNames.Add(cardDist.whatCard(card));
        }
        disproveList.AddOptions(cardNames);
        disprovePanel.gameObject.SetActive(true);
    }

    public void confirmDisprove()
    {
        Card clueToShow = disproveList.value;
>>>>>>> parent of 0e29a2d (Revert "Updates for guessing logic")
    }

}
