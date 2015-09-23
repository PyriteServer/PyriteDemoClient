namespace Pyrite
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;

    public class DetectionCube : MonoBehaviour
    {
        private readonly List<GameObject> _childDetectors = new List<GameObject>();
        private GameObject _model = null;
        private int _lodIndex;
        private PyriteLoader _manager;
        private MeshRenderer _meshRenderer;
        private PyriteQuery _pyriteQuery;
        private Renderer _render;
        private int _x, _y, _z;
        private LoadCubeRequest _loadCubeRequest;

        private State _state;

        enum State
        {
            NotVisible = 0,
            AboutToBeVisible,
            ModelLoading,
            ModelLoaded,
            Upgrading,
            Upgraded
        }

        private bool Upgradable
        {
            get
            {
                return _state != State.Upgraded
                    && _state != State.Upgrading
                    && _manager != null 
                    && _childDetectors.Count == 0 
                    && _lodIndex != 0;
            }
        }

        private bool Downgradable
        {
            get { return _state == State.Upgraded; }
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
            _state = State.NotVisible;
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
                if(_state == State.NotVisible)
                {
                    _state = State.AboutToBeVisible;
                    _meshRenderer.enabled = false;
                    StartCoroutine(StopRenderCheck(Camera.main));
                }
            }
        }

        private IEnumerator RequestCubeLoad()
        {
            _state = State.ModelLoading;
            CancelRequest();
            _loadCubeRequest = new LoadCubeRequest(_x, _y, _z, _lodIndex, _pyriteQuery, OnModelLoaded);
            return _manager.EnqueueLoadCubeRequest(_loadCubeRequest);
        }

        private void OnModelLoaded(GameObject modelObject)
        {
            if(_state != State.ModelLoading && _state != State.NotVisible)
            {
                Debug.LogError("OnModelLoaded called when in invalid state: " + _state);
            }

            DestroyChildren();
            _model = modelObject;
            if (_state == State.NotVisible)
            {
                DestroyChildren();
            }
            else
            {
                _state = State.ModelLoaded;
            }
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
            detectorCubeToRelease.GetComponent<DetectionCube>()._state = State.NotVisible;
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
            if (_model != null)
            {
                ReleaseCubeGameObject(_model);
                _model = null;
            }
        }

        private void RemoveChildDetectors()
        {
            foreach (var detector in _childDetectors)
            {
                detector.GetComponent<DetectionCube>().DestroyChildren();
                ReleaseDetectorCube(detector);
            }
            _childDetectors.Clear();
        }

        private void DestroyChildren()
        {
            CancelRequest();

            RemoveChildModelObjects();

            RemoveChildDetectors();
            // Resources.UnloadUnusedAssets();
        }

        private IEnumerator WaitForChildrenToLoad(List<GameObject> newDetectors)
        {
            // Loop until hidden or children have loaded
            // When a child is loaded its loadCubeRequest is set to null
            // It is also in this state when no loading has been requested
            while (newDetectors.Any(cd =>
            {
                var cdAsIsRendered = cd.GetComponent<DetectionCube>();
                return cdAsIsRendered._loadCubeRequest != null;
            }))
            {
                yield return null;
                if (ShouldHideModel)
                {
                    _state = State.NotVisible;
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
            if (_state == State.NotVisible)
            {
                DestroyChildren();
            }
            else
            {
                _state = State.Upgraded;
            }
        }

        public bool ShouldHideModel
        {
            get { return !GeometryUtility.TestPlanesAABB(_manager.CameraFrustrum, _render.bounds); }
        }

        private IEnumerator StopRenderCheck(Camera cameraToCheckAgainst)
        {
            while (true)
            {
                if (ShouldHideModel)
                {
                    // The detection cube is no longer visible
                    _state = State.NotVisible;
                    _meshRenderer.enabled = true;
                    DestroyChildren();
                    break;
                }

                if (ShouldUpgrade(cameraToCheckAgainst))
                {
                    _state = State.Upgrading;
                    CancelRequest();
                    yield return
                        StartCoroutine(_manager.AddUpgradedDetectorCubes(_pyriteQuery, _x, _y, _z, _lodIndex,
                            OnUpgradedDetectorCubesCreated));
                }
                else if (_state == State.AboutToBeVisible || (Downgradable && ShouldDowngrade(cameraToCheckAgainst)))
                {
                    _state = State.ModelLoading;
                    yield return StartCoroutine(RequestCubeLoad());
                }

                // Run this at most 10 times per second
                yield return new WaitForSeconds(0.1F);
            }
        }

        private void OnUpgradedDetectorCubesCreated(IEnumerable<GameObject> addedDetectors)
        {
            if (_state != State.Upgrading && _state != State.NotVisible)
            {
                Debug.LogError("OnUpgradedDetectorCubesCreated called when in invalid state: " + _state);
            }

            StartCoroutine(DestroyChildrenAfterLoading(addedDetectors));
        }
    }
}