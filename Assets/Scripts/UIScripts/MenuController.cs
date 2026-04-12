using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using Netcode = Unity.Netcode.NetworkManager;

public class MenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;  
    [SerializeField] private GameObject lobbyPanel; 

    [Header("Lobby UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI playersText;
    [SerializeField] private Button startGameBtn;

    private void Start()
    {
        // Ensures the "Host" and "Join" buttons are the only panel shown first
        loginPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        
        // Hides the start button from all players until host is validated
        startGameBtn.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Constantly updates to account for incoming players
        GetPlayerCount();
    }


    // Once "Host" button is clicked, switches to lobby panel and "Start" button for the host.
    public void OnHostClicked()
    {
        if (Netcode.Singleton.StartHost())
        {
            displayLobby();
            startGameBtn.gameObject.SetActive(true); // Only the Host sees the Start button
            statusText.text = "Host Mode: Waiting for players...";
        }
        else
        {
            statusText.text = "Failed to start Host.";
        }
    }

    // Once "Join" button is clicked, swicthes to lobby panel.
    public void OnJoinClicked()
    {
        if (Netcode.Singleton.StartClient())
        {
            displayLobby();
            statusText.text = "Client Mode: Joining Host...";
        }
        else
        {
            statusText.text = "Failed to start Client.";
        }
    }

    // Method for switching to lobby panelS
    private void displayLobby()
    {
        loginPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    // Once "Start" button is clicked, validates host has actioned before loading next scene
    public void OnStartGameClicked()
    {
        if (Netcode.Singleton.IsServer)
        {
            Netcode.Singleton.SceneManager.LoadScene("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    // This method is called every tick during update method to keep real time tracking of player count
    public void GetPlayerCount()
    {
        int playerCount = Netcode.Singleton.ConnectedClients.Count;
        playersText.text = $"Players in Lobby: {playerCount}";
    }
}