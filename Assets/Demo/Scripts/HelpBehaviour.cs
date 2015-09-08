using UnityEngine;
using System.Collections;

public class HelpBehaviour : MonoBehaviour {

    public GameObject HelpPanel;

    // Use this for initialization
    void Start () {
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
