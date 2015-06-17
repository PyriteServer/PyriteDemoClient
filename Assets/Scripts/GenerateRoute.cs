using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class GenerateRoute : MonoBehaviour {

    public GameObject[] routePoints;

    bool routeEnabled = false;
    GameObject routeGroup;

	// Use this for initialization
	void Start () {

        routeGroup = GameObject.Find("RouteGroup");
        GameObject route = GameObject.Instantiate(Resources.Load("Route")) as GameObject;
        route.transform.parent = routeGroup.transform;

        LineRenderer lr = route.GetComponent<LineRenderer>();
        lr.SetVertexCount(2);
        int index = 0;
        for(int i=0; i<routePoints.Length; i++)
        {
            Debug.Log("Iterating i: " + i);
            lr.SetPosition(index, routePoints[i].transform.position);
            index++;

            if (index == 2 && i + 1 < routePoints.Length)
            {
                index = 0;
                route = GameObject.Instantiate(Resources.Load("Route")) as GameObject;
                route.transform.parent = routeGroup.transform;

                lr = route.GetComponent<LineRenderer>();
                lr.SetVertexCount(2);
                lr.SetPosition(index, routePoints[i].transform.position);
                index++;
            }
        }
            

        GameObject routeStart = GameObject.Find("Canvas/GliderStart").gameObject;
        routeStart.GetComponent<Button>().onClick.AddListener(() =>
        {
            ToggleRoute(routeStart);
        });


        routeGroup.SetActive(false);
	}
	
    void ToggleRoute(GameObject button)
    {
        if ( routeEnabled )
        {
            routeGroup.SetActive(false);
            routeEnabled = false;
        }
        else
        {
            routeGroup.SetActive(true);
            routeEnabled = true;
        }
    }

	// Update is called once per frame
	void Update () {
	
	}
}
