using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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
    [SerializeField] private Button escape;

    private void Start()
    {
        // Ensures the "Host" and "Join" buttons are the only panel shown first
        loginPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        
        // Hides the start button from all players until host is validated
        startGameBtn.gameObject.SetActive(false);
        escape.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Constantly updates to account for incoming players
        GetPlayerCount();

        // if esc is pressed then go back to lobby
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        { 
            goBack();

        }

    }


    // Once "Host" button is clicked, switches to lobby panel and "Start" button for the host.
    public void OnHostClicked()
    {
        if (Netcode.Singleton.StartHost())
        {
            displayLobby();
            startGameBtn.gameObject.SetActive(true); // Only the Host sees the Start button
            escape.gameObject.SetActive(true);
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
            escape.gameObject.SetActive(true);
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

    // makes you go back to lobby screen (esc)
    private void goBack()
    {   
        loginPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        if (startGameBtn == true )
        {
            startGameBtn.gameObject.SetActive(false);
        }

        if (Netcode.Singleton.IsClient || Netcode.Singleton.IsHost)
        {
            Netcode.Singleton.Shutdown();
            Debug.Log("You got disconnected");
        }

        escape.gameObject.SetActive(false);
    }

    public void esc()
    {
        goBack();
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