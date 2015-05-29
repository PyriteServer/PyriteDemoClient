using Pyrite;
using UnityEngine;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    // Use this for initialization
    private void Start()
    {
        GliderButton = GameObject.Find("GliderStart");
        ExplorerButton = GameObject.Find("ExplorerStart");
        NotConnectedCanvas = GameObject.Find("NotConnected");

        SetButtonStates(Connection.State);
        connectionAttempts = 0;
    }

    private GameObject GliderButton;
    private GameObject ExplorerButton;
    private GameObject NotConnectedCanvas;

    private const int maximumConnectionAttempts = 5;
    private int connectionAttempts;

    // Update is called once per frame
    private void Update()
    {
        if (Connection.State != ConnectionState.Connected)
        {
            if (connectionAttempts >= maximumConnectionAttempts)
            {
                NotConnectedCanvas.GetComponentInChildren<Text>().text = "Failed to connect.";
            }
            else
            {
                StartCoroutine(Connection.CheckConnection());
                connectionAttempts++;
            }
        }
        SetButtonStates(Connection.State);
    }

    public void LaunchGlider()
    {
        AutoFade.LoadLevel("Glider", 0.5f, 0.5f, new Color(50, 9, 5));
    }

    /// <summary>
    /// Sets the button states states of the menu as appropriate for the current connection status
    /// </summary>
    /// <param name="connectionState">current connection state</param>
    private void SetButtonStates(ConnectionState connectionState)
    {
        if (connectionState != ConnectionState.Connected)
        {
            GliderButton.SetActive(false);
            ExplorerButton.SetActive(false);
            NotConnectedCanvas.SetActive(true);
        }
        else
        {
            GliderButton.SetActive(true);
            ExplorerButton.SetActive(true);
            NotConnectedCanvas.SetActive(false);
        }
    }
}