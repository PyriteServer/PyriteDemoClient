using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Assets.Cube_Loader.Extensions;
using Assets.Cube_Loader.src;

public class IsRendered : MonoBehaviour
{
    MeshRenderer meshRenderer;
    Renderer render;
    private readonly List<GameObject> cubes = new List<GameObject>();
    private readonly List<GameObject> childDetectors = new List<GameObject>();
    private Cube cube = null;

    public void SetCubePosition(int x, int y, int z, int lod, PyriteQuery query, DemoOBJ manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.lod = lod;
        this.pyriteQuery = query;
        this.manager = manager;
        this.name = string.Format("PH_L{3}:{0}_{1}_{2}", x, y, z, lod);
    }

    public void SetCubePosition(int x, int y, int z,int lod, PyriteQuery query, CubeLoader manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.lod = lod;
        this.pyriteQuery = query;
        this.cubeLoader = manager;
        this.name = string.Format("PH_L{3}:{0}_{1}_{2}", x, y, z, lod);
    }

    private int x, y, z;
    private int lod;
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
            if (this.cubes.Count == 0 && this.childDetectors.Count == 0)
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
                    manager.LoadCube(pyriteQuery, x, y, z,lod, (createdObjects) =>
                    {
                        this.cubes.AddRange(createdObjects);
                    }));
            yield return StartCoroutine(StopRenderCheck(Camera.main));
        }
        else if (cubeLoader != null)
        {
            cube = new Cube() {MapPosition = new Vector3(x, y, z), Query = pyriteQuery, LOD = lod};
            cubeLoader.AddToQueue(cube);
            while (cube.GameObject == null)
            {
                yield return null;
            }
            this.cubes.AddRange(new [] { cube.GameObject});
            yield return StartCoroutine(StopRenderCheck(Camera.main));
        }
        
    }

    private bool Upgradable
    {
        get { return manager != null && childDetectors.Count == 0 && lod > 1; }
    }

    private bool ShouldUpgrade(Component camera)
    {
        return Vector3.Distance(this.transform.position, camera.transform.position) < 500 &&
               Math.Abs(this.transform.position.y - camera.transform.position.y) < 120;
    }

    IEnumerator StopRenderCheck(Camera camera)
    {
        while (true)
        {
            if (!render.IsVisibleFrom(camera))
            {
                Debug.LogFormat("L{0}:{1},{2},{3} Not visible @ {4}", lod, x, y, z, Vector3.Distance(this.transform.position, camera.transform.position));
                meshRenderer.enabled = true;
                if (this.cubes != null)
                {
                    foreach (var cube in cubes)
                    {
                        Destroy(cube);
                    }
                    this.cubes.Clear();
                }

                if (this.cube != null)
                {
                    cube.GameObject = null;
                    cube = null;
                }

                foreach (var detector in childDetectors)
                {
                    Destroy(detector);
                }
                childDetectors.Clear();
                break;
            }
            else if (Upgradable && ShouldUpgrade(camera))
            {
                Debug.LogFormat("Orig do: {0} {1} {2}", x, y, z);
                foreach (var cube in cubes)
                {
                    Destroy(cube);
                }
                this.cubes.Clear();
                yield return
                    StartCoroutine(manager.AddUpgradedDetectorCubes(pyriteQuery, x, y, z, lod, (addedDetectors) =>
                    {
                        this.childDetectors.AddRange(addedDetectors);
                    }));
            }

            yield return null;
        }
    }
}
