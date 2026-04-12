using UnityEngine;
using Unity.Netcode;

public class ConnectionManager : MonoBehaviour
{
    [Tooltip("The maximum number of players allowed, including the host.")]
    [SerializeField] private int maxPlayers = 6;

    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;

        if (currentPlayers >= maxPlayers)
        {
            response.Approved = false;
            response.Reason = "Server is full";
            
            Debug.Log($"Rejected connection from Client {request.ClientNetworkId}. " +
                      $"Players: {currentPlayers}/{maxPlayers}");
        }
        else
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            
            Debug.Log($"Approved connection from Client {request.ClientNetworkId}. " +
                      $"Players: {currentPlayers + 1}/{maxPlayers}");
        }
    }
}