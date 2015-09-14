using UnityEngine;
using System.Collections;

public class HelpBehaviour : MonoBehaviour {

    public GameObject HelpPanel;
    public GameObject TouchPanel;
    public GameObject KeyboardPanel;
    public GameObject MousePanel;

    // Use this for initialization
    void Start ()
    {
        if(!Input.touchSupported)
        {
            TouchPanel.SetActive(false);
        }

#if (UNITY_ANDROID || UNITY_IOS)
        KeyboardPanel.SetActive(false);
        MousePanel.SetActive(false);
#endif

    }

    void OnEnable()
    {
        StartCoroutine(DisableAfterTime(5, HelpPanel));
    }

    IEnumerator DisableAfterTime(int secondsToDisableAfter, GameObject objectToDisable)
    {
        yield return new WaitForSeconds(secondsToDisableAfter);
        objectToDisable.SetActive(false);
    }
    
    // Update is called once per frame
    void Update () {
    
    }

    public void Toggle()
    {
        HelpPanel.SetActive(!HelpPanel.activeInHierarchy);
    }
}
