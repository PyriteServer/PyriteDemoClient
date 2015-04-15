namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Extensions;
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
            get { return manager != null && childDetectors.Count == 0 && pyriteQuery.DetailLevels.ContainsKey(lod-1); }
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
            // Debug.LogFormat("Init: {0}", this);
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

        public void DestroyChildren()
        {
            // Debug.LogFormat("DestroyChildren {0}", this);
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
                detector.GetComponent<IsRendered>().DestroyChildren();
                Destroy(detector);
            }
            childDetectors.Clear();
        }

        public override string ToString()
        {
            return string.Format("ph L{0}:{1},{2},{3}", lod, x, y, z);
        }

        private IEnumerator StopRenderCheck(Camera camera)
        {
            while (true)
            {
                if (!render.IsVisibleFrom(camera))
                {
                    meshRenderer.enabled = true;
                    DestroyChildren();
                    break;
                }
                if (Upgradable && ShouldUpgrade(camera))
                {
                    // Debug.LogFormat("Upgrading: {0}", this);
                    yield return
                        StartCoroutine(manager.AddUpgradedDetectorCubes(pyriteQuery, x, y, z, lod,
                            addedDetectors =>
                            {
                                DestroyChildren();
                                childDetectors.AddRange(addedDetectors);
                            }));
                }

                yield return null;
            }
        }
    }
}