using System;
using UnityEngine;
using System.Collections;
using System.Runtime.CompilerServices;
using Assets.Cube_Loader.Extensions;

public class IsRendered : MonoBehaviour
{
    MeshRenderer meshRenderer;
    Renderer render;
    private GameObject[] cubes = null;
    private Cube cube = null;

    public void SetCubePosition(int x, int y, int z, CubeQuery query, DemoOBJ manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.query = query;
        this.manager = manager;
        this.name = string.Format("PH_{0}_{1}_{2}", x, y, z);
    }

    public void SetCubePosition(int x, int y, int z, CubeQuery query, CubeLoader manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.query = query;
        this.cubeLoader = manager;
        this.name = string.Format("PH_{0}_{1}_{2}", x, y, z);
    }

    private int x, y, z;
    private CubeQuery query;
    private DemoOBJ manager;
    private CubeLoader cubeLoader;

    // Use this for initialization
    void Start()
    {
        render = GetComponent<Renderer>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void OnWillRenderObject()
    {
        if (Camera.current == Camera.main)
        {
            if (this.cubes == null)
            {
                meshRenderer.enabled = false;
                StartCoroutine(OnRenderRoutine());
            }
        }
    }

    IEnumerator OnRenderRoutine()
    {
        if (manager != null)
        {
            yield return StartCoroutine(
                manager.LoadCube(query, x, y, z, (createdObjects) => { this.cubes = createdObjects; }));
            yield return StartCoroutine(StopRenderCheck(Camera.main));
        }
        else if (cubeLoader != null)
        {
            cube = new Cube() {MapPosition = new Vector3(x, y, z), Query = query};
            cubeLoader.AddToQueue(cube);
            while (cube.GameObject == null)
            {
                yield return null;
            }
            yield return StartCoroutine(StopRenderCheck(Camera.main));
        }
        
    }


    IEnumerator StopRenderCheck(Camera camera)
    {
        // Debug.LogFormat("{0}_{1}_{2}: {3}",x,y,z,Vector3.Distance(camera.transform.position, this.transform.position));

        while (true)
        {
            if (!render.IsVisibleFrom(camera))
            {
                meshRenderer.enabled = true;
                if (this.cubes != null)
                {
                    foreach (var cube in cubes)
                    {
                        Destroy(cube.gameObject);
                    }
                    this.cubes = null;
                }

                if (this.cube != null)
                {
                    Destroy(cube.GameObject);
                    cube = null;
                }
                break;
            }
            yield return null;
        }
    }
}
