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
    public Button secPasBtn;

    // Instance of this UI controller
    public static UIController Instance;

    // Text displaying players hand
    public TextMeshProUGUI handText;
    public TMP_Dropdown handList;

    // Local instance of player's movement script
    public Movement localPlayerScript;

    // UI Panels for general game phases
    [Header("Panels")]
    [SerializeField] private GameObject rollPanel;  
    [SerializeField] private GameObject movePanel;  
    [SerializeField] private GameObject guessPanel; 

    // Disproving UI
    [Header("Disproving UI")]
    [SerializeField] private GameObject disprovePanel;
    [SerializeField] private TMP_Dropdown disproveList;
    [SerializeField] public TextMeshProUGUI disproveText;

    // Buttons used for accusation and suggestion
    [SerializeField] public Button guessButton;
    [SerializeField] public Button accuseButton;
    [SerializeField] public Button confirmAccuseButton;

    // Links to Card Distributor
    public CardDistributor cardDist;
    
    // Used to populate the list of disprove options.
    List<string> cardNames;

    // Used to store the selected clues of a suggestion.
    public Who selectedSuspect;
    public What selectedWeapon;
    public Where selectedRoom;

    // Used to store the selected clues of an accusation.
    public Who accuseWho;
    public What accuseWhat;
    public Where accuseWhere;


    // Simple methods used for hiding and showing different UI elements.
    public void ShowSuggestionUI() => guessPanel.SetActive(true);
    public void HideSuggestionBtn() => guessButton.gameObject.SetActive(true);
    public void ShowAccuseBtn() => accuseButton.gameObject.SetActive(true);
    public void ShowRollingUI() => rollPanel.SetActive(true);
    public void HideRollingUI() => rollPanel.SetActive(false);
    public void HideDisproveUI() => disprovePanel.SetActive(false);
    public void HideSuggestionUI() => guessPanel.SetActive(false);

    // Marks if player is currently Accusing
    public bool isAccuse;

    // Marks if player is currently Disproving
    public bool isDisprove;

    private void Awake()
    {
        Instance = this;
    }

    // Initialises the cluesheet as hidden, fills the suggestion dropdowns with all clues and subscribes to changes in the Turn Manager.
    void Start()
    {
        isAccuse = false;
        isDisprove = false;
        cluesheetToggled = false;
        clueSheetBtn.gameObject.SetActive(true);
        fillGuessDropdowns();

        if (turnMan != null)
        {
            turnMan.whatPhase.OnValueChanged += (oldVal, newVal) => UpdateUIVisibility();
            turnMan.whosPlaying.OnValueChanged += (oldVal, newVal) => UpdateUIVisibility();
        }
        
        UpdateUIVisibility();
    }

    // Keeps all required variables from Movement and Turn Manager script up to date.
    // Keeps UI hidden or shown depending if player is currently in a room or on a door.
    void Update()
    {
        if (localPlayerScript != null && isMyTurn)
        {
            TurnStage phase = turnMan.whatPhase.Value;

            if (phase == TurnStage.MOVING)
            {
                bool isInRoom = localPlayerScript.IsInRoom();
                bool isOnDoor = localPlayerScript.IsOnDoor();

                entryButton.gameObject.SetActive(isOnDoor);
            
                exitList.gameObject.SetActive(isInRoom);
                exitButton.gameObject.SetActive(isInRoom);
                exitText.gameObject.SetActive(isInRoom);
            }
            else 
            {
                entryButton.gameObject.SetActive(false);
                exitButton.gameObject.SetActive(false);
            }
        }
    }

    // Finds player script that has spawned this UI controller instance and auto-updates their move counter.
    public void SetLocalPlayer(Movement player)
    {
        localPlayerScript = player;
        updateMoveText(); 
        localPlayerScript.move_tokens.OnValueChanged += (oldVal, newVal) => updateMoveText();
        UpdateUIVisibility();
    }

    // Method to generally update UI across turns and turn phases.
    public void UpdateUIVisibility()
    {   
        bool iRobot = turnMan.turingTest();
    
        
        isMyTurn = (NetworkManager.Singleton.LocalClientId == (ulong)turnMan.whosPlaying.Value);
    
        TurnStage currentPhase = turnMan.whatPhase.Value;

        rollPanel.SetActive(false);
        movePanel.SetActive(false);
        guessPanel.SetActive(false);
        
        if (!isDisprove)
        {
            HideDisproveUI();
        }

        // If character is an AI, hides all UI elements from them.
        if (iRobot)
        {
            entryButton.gameObject.SetActive(false);
            exitButton.gameObject.SetActive(false);
            exitList.gameObject.SetActive(false);
            exitText.gameObject.SetActive(false);
            moves.gameObject.SetActive(false);
            return;
        }

        // Shows UI elements if it is the players turn and the relevent phase.
        if (isMyTurn)
        {
            rollPanel.SetActive(currentPhase == TurnStage.ROLLING);
            movePanel.SetActive(currentPhase == TurnStage.MOVING);
            guessPanel.SetActive(currentPhase == TurnStage.SUGGESTING);
            accuseButton.gameObject.SetActive(true);

            if (currentPhase != TurnStage.SUGGESTING)
            {
                isAccuse = false;
                confirmAccuseButton.gameObject.SetActive(false);
            }

            if (currentPhase == TurnStage.MOVING && localPlayerScript != null)
            {
                moves.gameObject.SetActive(true);
                updateMoveText();

                bool isInRoom = localPlayerScript.IsInRoom();
                bool isOnDoor = localPlayerScript.IsOnDoor();

                exitList.gameObject.SetActive(isInRoom);
                exitText.gameObject.SetActive(isInRoom);
                exitButton.gameObject.SetActive(isInRoom);
                entryButton.gameObject.SetActive(isOnDoor);
                secPasBtn.gameObject.SetActive(false);
                
                // Shows secret passage button if the player is in a relevent room.
                if (isInRoom) 
                {
                    exitDropdown();
                    if (localPlayerScript.currentRoomName.Value == "Study" || localPlayerScript.currentRoomName.Value == "Kitchen" || localPlayerScript.currentRoomName.Value == "Conservatory" || localPlayerScript.currentRoomName.Value == "Lounge")
                    {
                        secPasBtn.gameObject.SetActive(true);
                    }
                    else
                    {
                        secPasBtn.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                entryButton.gameObject.SetActive(false);
                exitButton.gameObject.SetActive(false);
                moves.gameObject.SetActive(false);
                secPasBtn.gameObject.SetActive(false);
            }
            if (currentPhase == TurnStage.SUGGESTING)
            {
                fillGuessDropdowns();
                guessPanel.SetActive(true);
                confirmAccuseButton.gameObject.SetActive(isAccuse);
            }
            else
            {
                guessPanel.SetActive(false);
                disproveText.gameObject.SetActive(false);
            }
        }
        else
        {
            if (isDisprove)
            {
                disproveList.gameObject.SetActive(true);
                disprovePanel.SetActive(true);
            }
            entryButton.gameObject.SetActive(false);
            exitButton.gameObject.SetActive(false);
            moves.gameObject.SetActive(false);
        }
    }

    // Updates move text for player depending on their current move_token value
    public void updateMoveText()
    {
        if (localPlayerScript != null)
        {
            moves.text = $"Moves: {localPlayerScript.move_tokens.Value}";
        }
    }

    // Allows client player object to submit room entry request to server.
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
        UpdateUIVisibility();
    }

    // Populates room exit dropdown with the correct doors of the room.
    public void exitDropdown()
    {
        if (localPlayerScript == null) return;

        Door[] allDoors = GameObject.FindObjectsByType<Door>(FindObjectsSortMode.None);
        exitNames.Clear();
        currentDoors.Clear();

        foreach (var door in allDoors)
        {
            if (door.roomName == localPlayerScript.currentRoomName.Value) 
            {
                currentDoors.Add(door);
                exitNames.Add(door.exitName);   
            }
        }

        exitList.ClearOptions();
        exitList.AddOptions(exitNames);
    }

    // Links to the button for selecting the exit to leave the room.
    public void confirmExit()
    {
        int selectedIndex = exitList.value;
        if (localPlayerScript != null && selectedIndex >= 0 && selectedIndex < currentDoors.Count)
        {
            Door exitDoor = currentDoors[selectedIndex];
            NetworkObject doorNetObj = exitDoor.GetComponent<NetworkObject>();
            
            if (doorNetObj != null)
            {
                localPlayerScript.submitExitServerRpc(doorNetObj.NetworkObjectId);
            }
        }
        UpdateUIVisibility();
    }

    // Adds cards given from Card Distributor to the players UI hand list.
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
    }

    // Returns the string name from the card given.
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

    // Fills the suggestion dropdown with valid clues, only adding the room they are currently in.
    void fillGuessDropdowns()
    {
        suspectList.ClearOptions();
        weaponList.ClearOptions();
        locationList.ClearOptions();

        suspectList.AddOptions(new List<string>(Enum.GetNames(typeof(Who))));
        weaponList.AddOptions(new List<string>(Enum.GetNames(typeof(What))));
        if (turnMan.whatPhase.Value == TurnStage.SUGGESTING)
        {
            string roomToShow = localPlayerScript.localRoomName;
            List<string> currentRoom = new List<string> {$"{roomToShow}"};
            locationList.AddOptions(currentRoom);
            Debug.Log($"Current room {localPlayerScript.currentRoomName.Value}");
        }
        else
        {
            locationList.AddOptions(new List<string>(Enum.GetNames(typeof(Where))));
        }
    }

    // Fully fills the dropdowns for clues when accusation is required when player is still in a room.
    public void fullyfillGuesses()
    {
        suspectList.ClearOptions();
        weaponList.ClearOptions();
        locationList.ClearOptions();

        suspectList.AddOptions(new List<string>(Enum.GetNames(typeof(Who))));
        weaponList.AddOptions(new List<string>(Enum.GetNames(typeof(What))));
        locationList.AddOptions(new List<string>(Enum.GetNames(typeof(Where))));
    }

    // Resets dropdowns to original values and shows button to confirm accusation.
    public void startAccuse()
    {
        isAccuse = true;
        clearGuessDropdowns();
        ToggleSuggestionLists(true);
        guessButton.gameObject.SetActive(false);
        accuseButton.gameObject.SetActive(false);
        confirmAccuseButton.gameObject.SetActive(true);
        ShowSuggestionUI();
    }

    // Links to GuessManager as updates value of accusation from current dropdown values.
    public void confirmAccuse()
    {
        accuseWho = (Who)suspectList.value;
        accuseWhat = (What)weaponList.value;
        accuseWhere = (Where)locationList.value;
        HideSuggestionUI();
        confirmAccuseButton.gameObject.SetActive(false);
        UpdateUIVisibility();
        
    }

    // Resets dropdown list so players cannot see what was last input.
    public void clearGuessDropdowns()
    {
        suspectList.value = 0;
        weaponList.value = 0;
        locationList.value = 0;
        suspectList.RefreshShownValue();
        weaponList.RefreshShownValue();
        locationList.RefreshShownValue();
    }

    // Links to GuessManager and converts the string value of the dropdown to the associated enum of the clue.
    public void suggestionButton()
    {
        int suspectIndex = suspectList.value;
        int weaponIndex = weaponList.value;
        int roomIndex = locationList.value;
        string suspName = suspectList.options[suspectIndex].text; 
        string weapName = weaponList.options[weaponIndex].text;
        string roomName = locationList.options[roomIndex].text;

        if (Enum.TryParse(suspName.Replace(" ", ""), true, out Who hooWho))
            selectedSuspect = hooWho;

        if (Enum.TryParse(weapName.Replace(" ", ""), true, out What wutWhat))
            selectedWeapon = wutWhat;

        if (Enum.TryParse(roomName.Replace(" ", ""), true, out Where werWhere))
            selectedRoom = werWhere;

        guessButton.gameObject.SetActive(false);
        confirmAccuseButton.gameObject.SetActive(true);
    }

    // Shows the disprove panel to the player with all cards they can choose from.
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

    // Links to the confirm disprove button to submit it to the GuessManager.
    public void confirmDisprove()
    {
        if (cardNames != null && cardNames.Count > 0)
        {
            string clueToShow = cardNames[disproveList.value];
            GuessManager.Instance.disproveResult(clueToShow);
        }
        isDisprove = false;
        HideDisproveUI();
        UpdateUIVisibility();
    }

    // Hides disprove Text and clears it for next players.
    public void hideDisproveText()
    {
        disproveText.gameObject.SetActive(false);
        disproveText.text = "";
    }

    // Function used for the pop-up cluesheet.
    public void cluePopUp()
    {
        cluesheetToggled = !cluesheetToggled;
        clueSheet.SetActive(cluesheetToggled);
    }


    // Links to the secret passage button allowing players to move across rooms.
    public void secretPassageBtn()
    {
        if (localPlayerScript != null)
        {
            localPlayerScript.activateSecPassServerRpc();
        }
    }

    // Shows the accusation button.
    public void showAccuse()
    {
        accuseButton.gameObject.SetActive(true);
    }

    // Links to the skip button for users, allowing them to skip a phase of their turn.
    public void skipBtn()
    {
        if (localPlayerScript.IsInRoom() == false)
        {
            confirmAccuseButton.gameObject.SetActive(false);
            guessPanel.gameObject.SetActive(false);
            hideDisproveText();
            localPlayerScript.reqTurnChangeServerRpc();
            UpdateUIVisibility();

        }
        else
        {
            confirmAccuseButton.gameObject.SetActive(false);
            guessPanel.gameObject.SetActive(false);
            hideDisproveText();
            turnMan.reqNextPhase();
            UpdateUIVisibility();

        }
    }

    public void HideAccuseUI()
    {
        disproveList.gameObject.SetActive(false);
        disprovePanel.SetActive(false);
        confirmAccuseButton.gameObject.SetActive(false);
    }

    public void ToggleSuggestionLists(bool trigger)
    {
        suspectList.gameObject.SetActive(trigger);
        weaponList.gameObject.SetActive(trigger);
        locationList.gameObject.SetActive(trigger);
    }
}