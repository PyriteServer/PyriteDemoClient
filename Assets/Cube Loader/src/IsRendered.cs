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
        private Cube _cube;
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
            StringBuilder nameBuilder = new StringBuilder("PH_L");
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
                    if (!_loadCubeRequest.Cancelled)
                    {
                        _cubes.Add(createdObject);
                        StartCoroutine(StopRenderCheck(Camera.main));
                    }
                    else
                    {
                        Destroy(createdObject);
                    }
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

        public void DestroyChildren()
        {
            if (_loadCubeRequest != null)
            {
                _loadCubeRequest.Cancelled = true;
            }

            if (_cubes != null)
            {
                foreach (var cube in _cubes)
                {
                    Destroy(cube);
                }
                _cubes.Clear();
            }

            if (_cube != null)
            {
                Destroy(_cube.GameObject);
                _cube = null;
            }

            foreach (var detector in _childDetectors)
            {
                detector.GetComponent<IsRendered>().DestroyChildren();
                Destroy(detector);
            }
            _childDetectors.Clear();
        }

        private IEnumerator StopRenderCheck(Camera cameraToCheckAgainst)
        {
            while (true)
            {
                if (!_render.IsVisibleFrom(cameraToCheckAgainst))
                {
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

                yield return null;
            }
        }
    }
}