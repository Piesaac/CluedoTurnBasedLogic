using UnityEngine;

public class camToggle : MonoBehaviour
{
    public Camera cam1;
    public Camera cam2;

    public void Start()
    {
        cam1.gameObject.SetActive(true) ;
        cam2.gameObject.SetActive(false);
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            cam1.gameObject.SetActive(!cam1.gameObject.activeSelf);
            cam2.gameObject.SetActive(!cam2.gameObject.activeSelf);
        }

    }
}
