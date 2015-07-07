namespace Pyrite
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;

    public class IsRendered : MonoBehaviour
    {
        private readonly List<GameObject> _childDetectors = new List<GameObject>();
        private readonly List<GameObject> _cubes = new List<GameObject>();
        private int _lodIndex;
        private PyriteLoader _manager;
        private MeshRenderer _meshRenderer;
        private PyriteQuery _pyriteQuery;
        private Renderer _render;
        private int _x, _y, _z;
        private LoadCubeRequest _loadCubeRequest;
        private bool _upgraded;
        private bool _upgrading;

        private bool Upgradable
        {
            get
            {
                return !_upgraded && !_upgrading && _manager != null && _childDetectors.Count == 0 && _lodIndex != 0;
            }
        }

        private bool Downgradable
        {
            get { return _upgraded && !_upgrading; }
        }

        public void SetCubePosition(int x, int y, int z, int lod, PyriteQuery query, PyriteLoader manager)
        {
            _x = x;
            _y = y;
            _z = z;
            _lodIndex = lod;
            _pyriteQuery = query;
            _manager = manager;
            var nameBuilder = new StringBuilder("PH_L");
            nameBuilder.Append(_pyriteQuery.DetailLevels[_lodIndex].Value);
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
                if (ShouldUpgrade(Camera.main))
                {
                    _upgrading = true;
                    StartCoroutine(StopRenderCheck(Camera.main));
                    yield return
                        StartCoroutine(_manager.AddUpgradedDetectorCubes(_pyriteQuery, _x, _y, _z, _lodIndex,
                            addedDetectors => { StartCoroutine(DestroyChildrenAfterLoading(addedDetectors)); }));
                }
                else
                {
                    yield return StartCoroutine(RequestCubeLoad());
                }
            }
        }

        private IEnumerator RequestCubeLoad()
        {
            CancelRequest();
            _loadCubeRequest = new LoadCubeRequest(_x, _y, _z, _lodIndex, _pyriteQuery, createdObject =>
            {
                DestroyChildren();
                _cubes.Add(createdObject);
                StartCoroutine(StopRenderCheck(Camera.main));
            });

            return _manager.EnqueueLoadCubeRequest(_loadCubeRequest);
        }

        private bool ShouldUpgrade(Component cameraThatDetects)
        {
            if (!Upgradable)
            {
                return false;
            }

            var distance = Vector3.Distance(transform.position, cameraThatDetects.transform.position);
            return distance < _pyriteQuery.DetailLevels[_lodIndex].UpgradeDistance;
        }

        private bool ShouldDowngrade(Component cameraThatDetects)
        {
            var distance = Vector3.Distance(transform.position, cameraThatDetects.transform.position);
            return distance > _pyriteQuery.DetailLevels[_lodIndex].DowngradeDistance;
        }

        // Cleans up cube game object and deactivates it to return to object pool
        private void ReleaseCubeGameObject(GameObject cubeToRelease)
        {
            cubeToRelease.name = "Released: " + cubeToRelease.name;
            cubeToRelease.GetComponent<MeshFilter>().mesh.Clear();
            var material = cubeToRelease.GetComponent<Renderer>().sharedMaterial;
            if (material != null)
            {
                cubeToRelease.GetComponent<Renderer>().sharedMaterial = null;
                lock (_manager.MaterialDataCache)
                {
                    _manager.MaterialDataCache.Release(material.mainTexture.name);
                }
            }

            cubeToRelease.SetActive(false);
        }

        // Cleans up detection cube game object and deactivates it to return to object pool
        private void ReleaseDetectorCube(GameObject detectorCubeToRelease)
        {
            detectorCubeToRelease.name = "Released: " + detectorCubeToRelease.name;
            detectorCubeToRelease.GetComponent<IsRendered>()._upgraded = false;
            detectorCubeToRelease.GetComponent<IsRendered>()._upgrading = false;
            detectorCubeToRelease.SetActive(false);
        }

        private void CancelRequest()
        {
            if (_loadCubeRequest != null)
            {
                _loadCubeRequest.Cancelled = true;
                _loadCubeRequest = null;
            }
        }

        private void RemoveChildModelObjects()
        {
            foreach (var cube in _cubes)
            {
                ReleaseCubeGameObject(cube);
            }
            _cubes.Clear();
        }

        private void RemoveChildDetectors()
        {
            foreach (var detector in _childDetectors)
            {
                detector.GetComponent<IsRendered>().DestroyChildren();
                ReleaseDetectorCube(detector);
            }
            _childDetectors.Clear();
        }

        private void DestroyChildren()
        {
            CancelRequest();

            RemoveChildModelObjects();

            RemoveChildDetectors();

            _upgraded = false;
            _upgrading = false;
            // Resources.UnloadUnusedAssets();
        }

        private IEnumerator WaitForChildrenToLoad(IEnumerable<GameObject> newDetectors)
        {
            while (newDetectors.Any(cd =>
            {
                var cdAsIsRendered = cd.GetComponent<IsRendered>();
                return cdAsIsRendered._loadCubeRequest != null;
            }))
            {
                yield return null;
                if (ShouldHideModel)
                {
                    _meshRenderer.enabled = true;
                    DestroyChildren();
                    break;
                }
            }
        }

        private IEnumerator DestroyChildrenAfterLoading(IEnumerable<GameObject> childDetectors)
        {
            yield return null;
            RemoveChildDetectors();
            _childDetectors.AddRange(childDetectors);
            yield return StartCoroutine(WaitForChildrenToLoad(_childDetectors));
            RemoveChildModelObjects();
            _upgrading = false;
            _upgraded = true;
        }

        public bool ShouldHideModel
        {
            get { return !GeometryUtility.TestPlanesAABB(_manager.CameraFrustrum, _render.bounds); }
        }

        private IEnumerator StopRenderCheck(Camera cameraToCheckAgainst)
        {
            while (true)
            {
                if (ShouldHideModel && !_upgrading)
                {
                    _meshRenderer.enabled = true;
                    DestroyChildren();
                    break;
                }
                if (ShouldUpgrade(cameraToCheckAgainst))
                {
                    _upgrading = true;
                    yield return
                        StartCoroutine(_manager.AddUpgradedDetectorCubes(_pyriteQuery, _x, _y, _z, _lodIndex,
                            addedDetectors => { StartCoroutine(DestroyChildrenAfterLoading(addedDetectors)); }));
                }
                else if (Downgradable && ShouldDowngrade(cameraToCheckAgainst))
                {
                    yield return StartCoroutine(RequestCubeLoad());
                }

                // Run this at most 10 times per second
                yield return new WaitForSeconds(0.1F);
            }
        }
    }
}