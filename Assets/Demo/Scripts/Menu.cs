using Pyrite;
using UnityEngine;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{

    public class AboutDialog
    {
        GameObject frame = null;
        GameObject link = null;
        GameObject close = null;

        public AboutDialog()
        {
            frame = GameObject.Find("AboutDialog");

            link = frame.transform.FindChild("Link").gameObject;

            GameObject close = frame.transform.FindChild("Close").gameObject;
            close.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnClose(close);
            });

            link.GetComponent<Button>().onClick.AddListener(() =>
            {
                OpenBrowser(link);
            });

            frame.SetActive(false);
        }

        void OnClose(GameObject button)
        {
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            frame.SetActive(visible);
        }

        void OpenBrowser(GameObject button)
        {
            Application.OpenURL(button.transform.FindChild("Text").GetComponent<Text>().text);
        }
    }

    // Use this for initialization
    private void Start()
    {
        GliderButton = GameObject.Find("GliderStart");
        ExplorerButton = GameObject.Find("ExplorerStart");
        NotConnectedCanvas = GameObject.Find("NotConnected");

        aboutDialog = new AboutDialog();

        SetButtonStates(Connection.State);
        connectionAttempts = 0;
    }

    private GameObject GliderButton;
    private GameObject ExplorerButton;
    private GameObject NotConnectedCanvas;
    private AboutDialog aboutDialog;

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

    public void LaunchPerth()
    {
        AutoFade.LoadLevel("Perth", 0.5f, 0.5f, new Color(50, 9, 5));
    }

    public void ShowAboutDialog()
    {
        aboutDialog.SetVisible(true);    
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