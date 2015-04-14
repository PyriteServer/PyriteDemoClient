using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Cube_Loader.Extensions;
using Assets.Cube_Loader.src;
using UnityEngine;

public class IsRendered : MonoBehaviour
{
    private readonly List<GameObject> childDetectors = new List<GameObject>();
    private readonly List<GameObject> cubes = new List<GameObject>();
    private Cube cube;
    private CubeLoader cubeLoader;
    private int lod;
    private DemoOBJ manager;
    private MeshRenderer meshRenderer;
    private PyriteQuery pyriteQuery;
    private Renderer render;
    private int x, y, z;

    private bool Upgradable
    {
        get { return manager != null && childDetectors.Count == 0 && lod > 1; }
    }

    public void SetCubePosition(int x, int y, int z, int lod, PyriteQuery query, DemoOBJ manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.lod = lod;
        pyriteQuery = query;
        this.manager = manager;
        name = string.Format("PH_L{3}:{0}_{1}_{2}", x, y, z, lod);
    }

    public void SetCubePosition(int x, int y, int z, int lod, PyriteQuery query, CubeLoader manager)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.lod = lod;
        pyriteQuery = query;
        cubeLoader = manager;
        name = string.Format("PH_L{3}:{0}_{1}_{2}", x, y, z, lod);
    }

    // Use this for initialization
    private void Start()
    {
        render = GetComponent<Renderer>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnWillRenderObject()
    {
        if (Camera.current == Camera.main)
        {
            if (cubes.Count == 0 && childDetectors.Count == 0)
            {
                meshRenderer.enabled = false;
                StartCoroutine(OnRenderRoutine());
            }
        }
    }

    private IEnumerator OnRenderRoutine()
    {
        if (manager != null)
        {
            yield return StartCoroutine(
                manager.LoadCube(pyriteQuery, x, y, z, lod, createdObjects => { cubes.AddRange(createdObjects); }));
            yield return StartCoroutine(StopRenderCheck(Camera.main));
        }
        else if (cubeLoader != null)
        {
            cube = new Cube {MapPosition = new Vector3(x, y, z), Query = pyriteQuery, LOD = lod};
            cubeLoader.AddToQueue(cube);
            while (cube.GameObject == null)
            {
                yield return null;
            }
            cubes.AddRange(new[] {cube.GameObject});
            yield return StartCoroutine(StopRenderCheck(Camera.main));
        }
    }

    private bool ShouldUpgrade(Component camera)
    {
        return Vector3.Distance(transform.position, camera.transform.position) < 500 &&
               Math.Abs(transform.position.y - camera.transform.position.y) < 120;
    }

    private IEnumerator StopRenderCheck(Camera camera)
    {
        while (true)
        {
            if (!render.IsVisibleFrom(camera))
            {
                meshRenderer.enabled = true;
                if (cubes != null)
                {
                    foreach (var cube in cubes)
                    {
                        Destroy(cube);
                    }
                    cubes.Clear();
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
            if (Upgradable && ShouldUpgrade(camera))
            {
                foreach (var cube in cubes)
                {
                    Destroy(cube);
                }
                cubes.Clear();
                yield return
                    StartCoroutine(manager.AddUpgradedDetectorCubes(pyriteQuery, x, y, z, lod,
                        addedDetectors => { childDetectors.AddRange(addedDetectors); }));
            }

            yield return null;
        }
    }
}