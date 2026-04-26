using UnityEngine;
using Unity.Netcode;
using TMPro;

public class GameEnd : MonoBehaviour
{
    public TextMeshProUGUI winText;

    void Start()
    {
        if (AccuseResult.gameEnd)
        {
            winText.text = $"{AccuseResult.winName} Solved the Crime!";
        }
    }
}