using UnityEngine;
using System.Collections;

public class LoadLevelOnClickHelper : MonoBehaviour {

    public string LevelToLoad = "DemoMenu";

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    public void OnClick()
    {
        AutoFade.LoadLevel(LevelToLoad, 0.5f, 0.5f, new Color(50, 9, 5));
    }
}
