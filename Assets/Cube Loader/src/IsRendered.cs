using System;
using UnityEngine;
using System.Collections;
using System.Runtime.CompilerServices;
using Assets.Cube_Loader.Extensions;

public class IsRendered : MonoBehaviour
{
    Material mat;
    Renderer render;
    Color origColor;
    private GameObject[] cubes = null;

    public void SetCubePosition(int x, int y, int z, CubeQuery query, DemoOBJ manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.query = query;
        this.manager = manager;
        this.name = string.Format("PH_{0}_{1}_{2}", x, y, z);
    }

    private int x, y, z;
    private CubeQuery query;
    private DemoOBJ manager;


    private int pauseCheck = 0;

    // Use this for initialization
    void Start()
    {
        render = GetComponent<Renderer>();
        mat = GetComponent<MeshRenderer>().material;
        origColor = mat.color;
    }

    // Update is called once per frame
    void Update()
    {
        if (pauseCheck == 0)
        {

            if (render.IsVisibleFrom(Camera.main))
            {
                GetComponent<MeshRenderer>().enabled = false;
                pauseCheck = 30;
                if (this.cubes == null)
                {
                    StartCoroutine(manager.LoadCube(query, x, y, z, (createdObjects) => { this.cubes = createdObjects; }));
                }

            }
            else
            {
                GetComponent<MeshRenderer>().enabled = true;
                mat.color = origColor;
                pauseCheck = 5;
                if (this.cubes != null)
                {
                    foreach (var cube in cubes)
                    {
                        Destroy(cube.gameObject);
                    }
                    this.cubes = null;
                }
            }
        }
        else
        {
            pauseCheck--;
        }

    }
}
