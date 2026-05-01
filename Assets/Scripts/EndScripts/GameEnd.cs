using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Netcode = Unity.Netcode.NetworkManager;
using UnityEngine.SceneManagement;

public class GameEnd : MonoBehaviour
{
    public TextMeshProUGUI winText;

    void Start()
    {

        winText.text = $"{AccuseResult.winName} Solved the Crime!";
        winText.text += $"{AccuseResult.endMessage}";
    }

    public void exitToMenu()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}