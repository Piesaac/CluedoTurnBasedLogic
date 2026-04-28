using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Netcode = Unity.Netcode.NetworkManager;

public class MenuController : NetworkBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject loginPanel;  
    [SerializeField] private GameObject lobbyPanel; 

    [Header("Lobby UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI playersText;
    [SerializeField] private Button startGameBtn;
    [SerializeField] private Button escape;
    [SerializeField] private Button aiBtn;
    [SerializeField] private TextMeshProUGUI aiText;

    [Header("Player/AI Count")]
    public NetworkVariable<int> aiCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int numAI;
    public int numHuman;
    public int totalPlayers;
    public static int numBotsToSpawn;

    //Sets initial login panel on as default
    private void Start()
    {
        numAI = 0;
        loginPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        startGameBtn.gameObject.SetActive(false);
        escape.gameObject.SetActive(false);
    }

    private void Update()
    {
        
        if (Netcode.Singleton != null && Netcode.Singleton.IsListening)
        {
            if (Netcode.Singleton.ConnectedClients != null)
            {
                playersText.text = $"Human Suspects: {Netcode.Singleton.ConnectedClients.Count}\n AI Suspects: {numAI}";
            }
        }
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        { 
            goBack();
        }
    }

    // Refreshes AI text when player joins and only shows add AI button for the host.
    public override void OnNetworkSpawn()
    {
        aiCount.OnValueChanged += (oldVal, newVal) => {
            refreshAIText(newVal);
        };

        if (IsServer)
        {
            aiBtn.gameObject.SetActive(true);
        }
        else
        {
            aiBtn.gameObject.SetActive(false);
        }

        refreshAIText(aiCount.Value);
    }


    public void addAI()
    {
        if (!NetworkManager.Singleton.IsServer) 
        {
            Debug.LogWarning("Only the Host can add AI!");
            return;
        }
        Debug.Log("Add AI button clicked");
        numHuman = Netcode.Singleton.ConnectedClients.Count;
        numAI= aiCount.Value;
        totalPlayers = numHuman + numAI;

        if (totalPlayers >= 6)
        {
            aiCount.Value = 0;
        }
        else
        {
            aiCount.Value++;
        }
        refreshAIText(aiCount.Value);
    }

    private void refreshAIText(int count)
    {
        aiText.text = $"There are currently: {count} AI players";
    }

    // --- Connection Logic ---

    public void OnHostClicked()
    {
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
        }
        Debug.Log("Host button clicked");
        if (Netcode.Singleton.StartHost())
        {
            displayLobby();
            startGameBtn.gameObject.SetActive(true);
            escape.gameObject.SetActive(true);
            statusText.text = "Host Mode: Waiting...";
        }
    }

    public void OnJoinClicked()
    {
        Debug.Log("Join button clicked");
        if (Netcode.Singleton.StartClient())
        {
            displayLobby();
            escape.gameObject.SetActive(true);
            statusText.text = "Client Mode: Joining...";
        }
    }

    private void displayLobby()
    {
        loginPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    private void goBack()
    {   
        if (Netcode.Singleton.IsClient || Netcode.Singleton.IsServer)
        {
            Netcode.Singleton.Shutdown();
        }

        loginPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        startGameBtn.gameObject.SetActive(false);
        escape.gameObject.SetActive(false);
        aiBtn.gameObject.SetActive(false);
    }

    public void OnStartGameClicked()
    {
        numHuman = Netcode.Singleton.ConnectedClients.Count;
        totalPlayers = numHuman + aiCount.Value;
        if (totalPlayers < 2)
        {
            Debug.Log($"Total Human Players: {numHuman}");
            Debug.Log($"Total AI Players: {aiCount.Value}");
            Debug.LogError("Not enough players in lobby to start game");
            return;
        }
        if (NetworkManager.Singleton.IsServer)
        {   
            numBotsToSpawn = aiCount.Value;
            NetworkManager.Singleton.SceneManager.LoadScene("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public void GetPlayerCount()
    {
        if (Netcode.Singleton.ConnectedClients != null)
        {
            int playerCount = Netcode.Singleton.ConnectedClients.Count;
            playersText.text = $"Players in Lobby: {playerCount}";
        }
    }
}