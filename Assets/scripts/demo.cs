using UnityEngine;
using System.Collections;

public class demo : MonoBehaviour {

	// Use this for initialization
	void Start () {
		for (int x = 22; x<25; x++)
		{
			for (int y = 22; y<25; y++)
			{
				OBJ obj;
				obj = gameObject.AddComponent<OBJ>();
				obj.objPath = string.Format("http://localhost:8080/output_{0}_{1}_0.obj", x, y);				
			}
		}

	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
