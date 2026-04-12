using UnityEngine;
using TMPro;

public class DoorUI : MonoBehaviour
{
    public TextMeshProUGUI doorNotif;
    public GameObject enterButton;
    public GameObject exitButton;
    public GameObject pickDoor;

    void start()
    {
        doorNotif.gameObject.SetActive(false);
        enterButton.SetActive(false);
    }


    public void notifDoor(string popentry)
    {
        doorNotif.text = popentry;
        doorNotif.gameObject.SetActive(true);
        enterButton.SetActive(true);

    }

    public void noMoDoor()
    {
        doorNotif.gameObject.SetActive(false);
        enterButton.SetActive(false);
    }

    public void roomExit()
    {
        exitButton.SetActive(true);
    }

    // Triggers the Guess button to appear
    public void anyDoor(string popexit)
    {
        exitButton.SetActive(false);
        doorNotif.text = popexit;
        doorNotif.gameObject.SetActive(true);
    }
}
