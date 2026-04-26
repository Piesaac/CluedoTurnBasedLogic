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
    [SerializeField] private Button aiBtn; // This was the missing line!
    [SerializeField] private TextMeshProUGUI aiText;

    // Synced across the network so clients can see the bot count in the lobby
    public NetworkVariable<int> aiCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Static variable that the PlayerSpawner will read in the Game scene
    public static int numBotsToSpawn;

    private void Start()
    {
        loginPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        startGameBtn.gameObject.SetActive(false);
        escape.gameObject.SetActive(false);
        aiBtn.gameObject.SetActive(false); // Hidden until we are the Host

        // Reset the static count when returning to menu
        numBotsToSpawn = 0;
    }

    public override void OnNetworkSpawn()
    {
        // UI Update listener
        aiCount.OnValueChanged += (oldVal, newVal) => {
            refreshAIText(newVal);
        };

        // Only the Host/Server can see the "Add AI" button
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
        if (!IsSpawned || !IsServer)
        {
            Debug.LogWarning("Only the Host can add AI!");
            return;
        }

        int numHuman = Netcode.Singleton.ConnectedClients.Count;
        int numAI = aiCount.Value;
        int totalPlayers = numHuman + numAI;

        // Clue usually has 6 characters max
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
        if (aiText != null)
        {
            aiText.text = $"There are currently: {count} AI players";
        }
    }

    public void OnHostClicked()
    {
        if (Netcode.Singleton.StartHost())
        {
            displayLobby();
            startGameBtn.gameObject.SetActive(true);
            escape.gameObject.SetActive(true);
            aiBtn.gameObject.SetActive(true); // Reveal immediately on host success
            statusText.text = "Host Mode: Waiting...";
        }
    }

    public void OnJoinClicked()
    {
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
        if (IsServer)
        {
            numBotsToSpawn = aiCount.Value; // Save to static for next scene
            NetworkManager.Singleton.SceneManager.LoadScene("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}