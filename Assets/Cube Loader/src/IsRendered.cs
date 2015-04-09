using System;
using UnityEngine;
using System.Collections;
using System.Runtime.CompilerServices;
using Assets.Cube_Loader.Extensions;
using Assets.Cube_Loader.src;

public class IsRendered : MonoBehaviour
{
    MeshRenderer meshRenderer;
    Renderer render;
    private GameObject[] cubes = null;
    private Cube cube = null;

    public void SetCubePosition(int x, int y, int z, PyriteQuery query, DemoOBJ manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.pyriteQuery = query;
        this.manager = manager;
        this.name = string.Format("PH_{0}_{1}_{2}", x, y, z);
    }

    public void SetCubePosition(int x, int y, int z, PyriteQuery query, CubeLoader manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.pyriteQuery = query;
        this.cubeLoader = manager;
        this.name = string.Format("PH_{0}_{1}_{2}", x, y, z);
    }

    private int x, y, z;
    private PyriteQuery pyriteQuery;
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
            if (pyriteQuery != null)
            {
                yield return StartCoroutine(
                     manager.LoadCube(pyriteQuery, x, y, z, (createdObjects) => { this.cubes = createdObjects; }));
            }
            yield return StartCoroutine(StopRenderCheck(Camera.main));
        }
        else if (cubeLoader != null)
        {
            cube = new Cube() {MapPosition = new Vector3(x, y, z), Query = pyriteQuery};
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
                    cube.GameObject = null;
                    cube = null;
                }
                break;
            }
            yield return null;
        }
    }
}
