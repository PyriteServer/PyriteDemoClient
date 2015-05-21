using UnityEngine;
using System.Collections;

public class Menu : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    public void LaunchGlider()
    {
        AutoFade.LoadLevel("Glider", 0.5f, 0.5f, new Color(50,9,5));
    }
}
