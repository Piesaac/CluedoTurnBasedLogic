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

    [Header("Panels")]
    [SerializeField] private GameObject rollPanel;  
    [SerializeField] private GameObject movePanel;  
    [SerializeField] private GameObject guessPanel; 

    [Header("Disproving UI")]
    [SerializeField] private GameObject disprovePanel;
    [SerializeField] private TMP_Dropdown disproveList;
    [SerializeField] public TextMeshProUGUI disproveText;

    [SerializeField] public Button guessButton;
    [SerializeField] public Button accuseButton;
    [SerializeField] public Button confirmAccuseButton;


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
    public void HideSuggestionBtn() => guessButton.gameObject.SetActive(true);
    public void ShowAccuseBtn() => accuseButton.gameObject.SetActive(true);
    public void HideSuggestionUI() => guessPanel.SetActive(false);
    public void ShowRollingUI() => rollPanel.SetActive(true);
    public void HideRollingUI() => rollPanel.SetActive(false);
    public void HideDisproveUI() => disprovePanel.SetActive(false);

    public bool isAccuse;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        isAccuse = false;
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

    public void SetLocalPlayer(Movement player)
    {
        localPlayerScript = player;
        updateMoveText(); 
        localPlayerScript.move_tokens.OnValueChanged += (oldVal, newVal) => updateMoveText();
        UpdateUIVisibility();
    }

    public void UpdateUIVisibility()
    {   
        bool iRobot = turnMan.turingTest();
    
        
        isMyTurn = (NetworkManager.Singleton.LocalClientId == (ulong)turnMan.whosPlaying.Value);
    
        TurnStage currentPhase = turnMan.whatPhase.Value;

        rollPanel.SetActive(false);
        movePanel.SetActive(false);
        guessPanel.SetActive(false);
        HideDisproveUI();

        if (iRobot)
        {
            entryButton.gameObject.SetActive(false);
            exitButton.gameObject.SetActive(false);
            exitList.gameObject.SetActive(false);
            exitText.gameObject.SetActive(false);
            moves.gameObject.SetActive(false);
            return;
        }

        if (isMyTurn)
        {
            rollPanel.SetActive(currentPhase == TurnStage.ROLLING);
            movePanel.SetActive(currentPhase == TurnStage.MOVING);
            guessPanel.SetActive(currentPhase == TurnStage.SUGGESTING);
            accuseButton.gameObject.SetActive(true);

            if (currentPhase != TurnStage.SUGGESTING)
            {
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
                
                if (isInRoom) 
                {
                    exitDropdown();
                    if (localPlayerScript.currentRoomName.Value == "Study" || localPlayerScript.currentRoomName.Value == "Kitchen" || localPlayerScript.currentRoomName.Value == "Conservatory" || localPlayerScript.currentRoomName.Value == "Lounge")
                    {
                        secPasBtn.gameObject.SetActive(true);
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
            }
            else
            {
                guessPanel.SetActive(false);
                disproveText.gameObject.SetActive(false);
            }
        }
        else
        {
            entryButton.gameObject.SetActive(false);
            exitButton.gameObject.SetActive(false);
            moves.gameObject.SetActive(false);
        }
    }


    public void delayedUI()
    {
        Invoke("UpdateUIVisibility", 1f);
    }

    public void updateMoveText()
    {
        if (localPlayerScript != null)
        {
            moves.text = $"Moves: {localPlayerScript.move_tokens.Value}";
            Debug.Log($"UI: Move text updated to {localPlayerScript.move_tokens.Value}");
        }
        else 
        {
            Debug.LogWarning("UI: Cannot update move text because localPlayerScript is NULL");
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
            if (door.roomName == localPlayerScript.currentRoomName.Value) 
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
                localPlayerScript.submitExitServerRpc(doorNetObj.NetworkObjectId);
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
        if (turnMan.whatPhase.Value == TurnStage.SUGGESTING)
        {
            List<string> currentRoom = new List<string> {$"{localPlayerScript.currentRoomName.Value}"};
            locationList.AddOptions(currentRoom);
            Debug.Log($"Current room {localPlayerScript.currentRoomName.Value}");
        }
        else
        {
            locationList.AddOptions(new List<string>(Enum.GetNames(typeof(Where))));
        }
    }

    public void fullyfillGuesses()
    {
        suspectList.ClearOptions();
        weaponList.ClearOptions();
        locationList.ClearOptions();

        suspectList.AddOptions(new List<string>(Enum.GetNames(typeof(Who))));
        weaponList.AddOptions(new List<string>(Enum.GetNames(typeof(What))));
        locationList.AddOptions(new List<string>(Enum.GetNames(typeof(Where))));
    }

    public void startAccuse()
    {
        clearGuessDropdowns();
        guessButton.gameObject.SetActive(false);
        accuseButton.gameObject.SetActive(false);
        confirmAccuseButton.gameObject.SetActive(true);
        ShowSuggestionUI();
    }

    public void confirmAccuse()
    {
        accuseWho = (Who)suspectList.value;
        accuseWhat = (What)weaponList.value;
        accuseWhere = (Where)locationList.value;
        HideSuggestionUI();
        confirmAccuseButton.gameObject.SetActive(false);
        UpdateUIVisibility();
        
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
    }

    public void cluePopUp()
    {
        cluesheetToggled = !cluesheetToggled;
        clueSheet.SetActive(cluesheetToggled);
    }

    public void secretPassageBtn()
    {
        if (localPlayerScript != null)
        {
            localPlayerScript.activateSecPassServerRpc();
        }
    }

    public void showAccuse()
    {
        accuseButton.gameObject.SetActive(true);
    }

    public void skipBtn()
    {
        confirmAccuseButton.gameObject.SetActive(false);
        guessPanel.gameObject.SetActive(false);
        hideDisproveText();
        turnMan.reqNextPhase();
        UpdateUIVisibility();
    }
}