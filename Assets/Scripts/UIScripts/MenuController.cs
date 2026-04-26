using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Netcode = Unity.Netcode.NetworkManager;

public class MenuController : NetworkBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;  
    [SerializeField] private GameObject lobbyPanel; 

    [Header("Lobby UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI playersText;
    [SerializeField] private Button startGameBtn;
    [SerializeField] private Button escape;
    [SerializeField] private Button aiBtn;
    [SerializeField] private TextMeshProUGUI aiText;

    public NetworkVariable<int> aiCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public static int numBotsToSpawn;

    private void Start()
    {
        loginPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        startGameBtn.gameObject.SetActive(false);
        escape.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        aiCount.OnValueChanged += (oldVal, newVal) => {
            refreshAIText(newVal);
        };

        if (IsServer)
        {
            aiBtn.gameObject.SetActive(true);
        }

        refreshAIText(aiCount.Value);
    }

    private void Update()
    {
        if (Netcode.Singleton != null && Netcode.Singleton.IsListening)
        {
            if (Netcode.Singleton.ConnectedClients != null)
            {
                playersText.text = $"Players in Lobby: {Netcode.Singleton.ConnectedClients.Count}";
            }
        }
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        { 
            goBack();
        }
    }

    public void addAI()
    {
        if (!NetworkManager.Singleton.IsServer) 
        {
            Debug.LogWarning("Only the Host can add AI!");
            return;
        }
        Debug.Log("Add AI button clicked");
        int numHuman = Netcode.Singleton.ConnectedClients.Count;
        int numAI= aiCount.Value;
        int totalPlayers = numHuman + numAI;

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
        Debug.Log("Start Button Clicked!");

        // CRITICAL CHECK: In a Host/Client setup, IsServer must be true for the Host
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Server validation passed. Loading Scene...");
            
            // Set the static variable so PlayerSpawner can find it
            numBotsToSpawn = aiCount.Value;

            // Use the NETWORK Scene Manager (Required for syncing scene loads)
            NetworkManager.Singleton.SceneManager.LoadScene("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("Start failed: You are not the Server/Host or NetworkManager is not initialized.");
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