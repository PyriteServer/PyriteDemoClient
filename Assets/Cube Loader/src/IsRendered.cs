namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using Extensions;
    using UnityEngine;

    public class IsRendered : MonoBehaviour
    {
        private readonly List<GameObject> _childDetectors = new List<GameObject>();
        private readonly List<GameObject> _cubes = new List<GameObject>();
        private int _lod;
        private PyriteLoader _manager;
        private MeshRenderer _meshRenderer;
        private PyriteQuery _pyriteQuery;
        private Renderer _render;
        private int _x, _y, _z;
        private LoadCubeRequest _loadCubeRequest;

        private bool Upgradable
        {
            get
            {
                return _manager != null && _childDetectors.Count == 0 && _pyriteQuery.DetailLevels.ContainsKey(_lod - 1);
            }
        }

        public void SetCubePosition(int x, int y, int z, int lod, PyriteQuery query, PyriteLoader manager)
        {
            _x = x;
            _y = y;
            _z = z;
            _lod = lod;
            _pyriteQuery = query;
            _manager = manager;
            var nameBuilder = new StringBuilder("PH_L");
            nameBuilder.Append(lod);
            nameBuilder.Append(':');
            nameBuilder.Append(x);
            nameBuilder.Append('_');
            nameBuilder.Append(y);
            nameBuilder.Append('_');
            nameBuilder.Append(z);
            name = nameBuilder.ToString();
        }

        // Use this for initialization
        private void Start()
        {
            _render = GetComponent<Renderer>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void OnWillRenderObject()
        {
            if (Camera.current == Camera.main)
            {
                if (_cubes.Count == 0 && _childDetectors.Count == 0)
                {
                    _meshRenderer.enabled = false;
                    StartCoroutine(OnRenderRoutine());
                }
            }
        }

        private IEnumerator OnRenderRoutine()
        {
            if (_manager != null)
            {
                _loadCubeRequest = new LoadCubeRequest(_x, _y, _z, _lod, _pyriteQuery, createdObject =>
                {
                    _cubes.Add(createdObject);
                    StartCoroutine(StopRenderCheck(Camera.main));
                });

                StartCoroutine(_manager.EnqueueLoadCubeRequest(_loadCubeRequest));
            }
            yield break;
        }

        private bool ShouldUpgrade(Component component)
        {
            return Vector3.Distance(transform.position, component.transform.position) < 500 &&
                   Math.Abs(transform.position.y - component.transform.position.y) < 120;
        }

        // Cleans up cube game object and deactivates it to return to object pool
        private void ReleaseCubeGameObject(GameObject cubeToRelease)
        {
            cubeToRelease.name = "Released: " + cubeToRelease.name;
            cubeToRelease.GetComponent<MeshFilter>().mesh.Clear();
            cubeToRelease.GetComponent<Renderer>().sharedMaterial = null;
            cubeToRelease.SetActive(false);
        }

        // Cleans up detection cube game object and deactivates it to return to object pool
        private void ReleaseDetectorCube(GameObject detectorCubeToRelease)
        {
            detectorCubeToRelease.name = "Released: " + detectorCubeToRelease.name;
            detectorCubeToRelease.SetActive(false);
        }

        private void DestroyChildren()
        {
            if (_loadCubeRequest != null)
            {
                _loadCubeRequest.Cancelled = true;
            }

            if (_cubes != null)
            {
                foreach (var cube in _cubes)
                {
                    ReleaseCubeGameObject(cube);
                }
                _cubes.Clear();
            }

            foreach (var detector in _childDetectors)
            {
                detector.GetComponent<IsRendered>().DestroyChildren();
                ReleaseDetectorCube(detector);
            }
            _childDetectors.Clear();
        }

        private IEnumerator StopRenderCheck(Camera cameraToCheckAgainst)
        {            
            while (true)
            {
                if (!GeometryUtility.TestPlanesAABB(_manager.CameraFrustrum, _render.bounds))
                {
                    if (_loadCubeRequest != null)
                    {
                        _loadCubeRequest.Cancelled = true;
                    }

                    _meshRenderer.enabled = true;
                    DestroyChildren();
                    Resources.UnloadUnusedAssets();
                    break;
                }
                if (Upgradable && ShouldUpgrade(cameraToCheckAgainst))
                {
                    yield return
                        StartCoroutine(_manager.AddUpgradedDetectorCubes(_pyriteQuery, _x, _y, _z, _lod,
                            addedDetectors =>
                            {
                                DestroyChildren();
                                Resources.UnloadUnusedAssets();
                                _childDetectors.AddRange(addedDetectors);
                            }));
                }

                // Run this at most 10 times per second
                yield return new WaitForSeconds(0.1F);
            }
        }
    }
}