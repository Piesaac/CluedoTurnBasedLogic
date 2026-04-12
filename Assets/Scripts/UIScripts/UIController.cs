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
    public TextMeshProUGUI moves;
    // Button to action rolling "dice"
    public Button rollBtn;
    // TurnManager object
    public TurnManager turnMan;

    // Drop downs for suggestions
    public TMP_Dropdown suspectList;
    public TMP_Dropdown weaponList;
    public TMP_Dropdown locationList;

    // Instance of this UI controller
    public static UIController Instance;

    // Text displaying players hand
    public TextMeshProUGUI handText;

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
        string displayString = "<b>YOUR HAND:</b>\n";
        foreach (Card card in cards)
        {
            displayString += $"- {whatCard(card)}\n";
        }

        handText.text = displayString;
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
        fillDropdowns();
        rollPanel.SetActive(true);
        movePanel.SetActive(false);
        guessPanel.SetActive(false);
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

    public void suggestionButton()
    {
        int suspectIndex = suspectList.value;
        string suspectName = suspectList.options[suspectIndex].text;

        int weaponIndex = weaponList.value;
        string weaponName = weaponList.options[weaponIndex].text;

        int roomIndex = locationList.value;
        string roomName = locationList.options[roomIndex].text;
    
        // You can now cast this back to your Enum!
        selectedSuspect = (Who)suspectIndex;
        selectedWeapon = (What)weaponIndex;
        selectedRoom =  (Where)roomIndex;
    }

    void fillDropdowns()
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

    public void clearDropdowns()
    {
        suspectList.value = 0;
        weaponList.value = 0;
        locationList.value = 0;
    
        suspectList.RefreshShownValue();
        weaponList.RefreshShownValue();
        locationList.RefreshShownValue();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    

}
