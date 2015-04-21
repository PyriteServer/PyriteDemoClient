namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Extensions;
    using RestSharp;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    public class PyriteLoader : MonoBehaviour
    {
        public string ModelVersion = "V2";
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private readonly Color[] _colorList =
        {
            Color.gray, Color.yellow, Color.cyan
        };

        private readonly Dictionary<string, byte[]> _eboCache = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, Material[]> _materialCache = new Dictionary<string, Material[]>();

        private readonly Dictionary<string, List<MaterialData>> _materialDataCache =
            new Dictionary<string, List<MaterialData>>();

        private readonly Dictionary<string, string> _objCache = new Dictionary<string, string>();
        private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        public GameObject CameraRig;
        private int _colorSelector;
        public int DetailLevel = 6;
        public bool EnableDebugLogs = false;
        public GameObject PlaceHolderCube;
        public string PyriteServer;
        public string SetName;
        public bool UseCameraDetection = false;
        public bool UseEbo = true;
        public bool UseUnlitShader = true;
        public bool UseWww = false;

        [Range(0, 100)] 
        public int MaxConcurrentRequests = 8;

        private readonly HashSet<string> _activeRequests = new HashSet<string>();

        private IEnumerator StartRequest(string path)
        {
            if (MaxConcurrentRequests == 0)
            {
                // Not limiting requests
                yield break;
            }
            while (_activeRequests.Count >= MaxConcurrentRequests)
            {
                yield return null;
            }
            _activeRequests.Add(path);
        }

        private void EndRequest(string path)
        {
            if (MaxConcurrentRequests == 0)
            {
                // Not limiting requests
                return;
            }
            _activeRequests.Remove(path);
        }

        protected void DebugLog(string fmt, params object[] args)
        {
            if (EnableDebugLogs)
            {
                var content = string.Format(fmt, args);
                Debug.LogFormat("{0}: {1}", _sw.ElapsedMilliseconds, content);
            }
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(SetName))
            {
                Debug.LogError("Must specify SetName");
                return;
            }

            if (string.IsNullOrEmpty(ModelVersion))
            {
                Debug.LogError("Must specify ModelVersion");
                return;
            }


            DebugLog("+Start()");
            StartCoroutine(Load());
            DebugLog("-Start()");
        }

        public IEnumerator Load()
        {
            DebugLog("+Load()");

            var pyriteQuery = new PyriteQuery(this, SetName, ModelVersion, PyriteServer);
            yield return StartCoroutine(pyriteQuery.LoadAll());
            DebugLog("CubeQuery complete.");

            var pyriteLevel =
                pyriteQuery.DetailLevels[DetailLevel];

            var xmin = pyriteLevel.WorldBoundsMax.x;
            var ymin = pyriteLevel.WorldBoundsMax.y;
            var zmin = pyriteLevel.WorldBoundsMax.z;
            var xmax = pyriteLevel.WorldBoundsMin.x;
            var ymax = pyriteLevel.WorldBoundsMin.y;
            var zmax = pyriteLevel.WorldBoundsMin.z;

            for (var i = 0; i < pyriteLevel.Cubes.Length; i++)
            {
                var x = pyriteLevel.Cubes[i].X;
                var y = pyriteLevel.Cubes[i].Y;
                var z = pyriteLevel.Cubes[i].Z;
                var cubePos = pyriteLevel.GetWorldCoordinatesForCube(pyriteLevel.Cubes[i]);
                xmin = Math.Min(cubePos.x, xmin);
                ymin = Math.Min(cubePos.y, ymin);
                zmin = Math.Min(cubePos.z, zmin);

                xmax = Math.Max(cubePos.x, xmax);
                ymax = Math.Max(cubePos.y, ymax);
                zmax = Math.Max(cubePos.z, zmax);

                if (UseCameraDetection)
                {
                    // Move cube to the orientation we want also move it up since the model is around -600
                    var g =
                        (GameObject)
                            Instantiate(PlaceHolderCube, new Vector3(-cubePos.x, cubePos.z + 600, -cubePos.y),
                                Quaternion.identity);

                    g.transform.parent = gameObject.transform;
                    g.GetComponent<MeshRenderer>().material.color = _colorList[_colorSelector%_colorList.Length];
                    g.GetComponent<IsRendered>().SetCubePosition(x, y, z, DetailLevel, pyriteQuery, this);

                    g.transform.localScale = new Vector3(
                        pyriteLevel.WorldCubeScale.x,
                        pyriteLevel.WorldCubeScale.z,
                        pyriteLevel.WorldCubeScale.y);
                    _colorSelector++;
                }
                else
                {
                    StartCoroutine(LoadCube(pyriteQuery, x, y, z, DetailLevel));
                }
            }

            if (CameraRig != null)
            {
                DebugLog("Moving camera");
                // Hardcoding some values for now
                var min = new Vector3(xmin, ymin, zmin);
                var max = new Vector3(xmax, ymax, zmax);
                var newCameraPosition = min + (max - min)/2.0f;
                newCameraPosition += new Vector3(0, 0, (max - min).z*1.4f);
                CameraRig.transform.position = newCameraPosition;

                CameraRig.transform.rotation = Quaternion.Euler(0, 180, 0);

                DebugLog("Done moving camera");
            }
            DebugLog("-Load()");
        }

        public IEnumerator AddUpgradedDetectorCubes(PyriteQuery pyriteQuery, int x, int y, int z, int lod,
            Action<IEnumerable<GameObject>> registerCreatedDetectorCubes)
        {
            var newLod = lod - 1;
            var createdDetectors = new List<GameObject>();
            var pyriteLevel = pyriteQuery.DetailLevels[newLod];

            var cubeFactor = pyriteQuery.GetNextCubeFactor(lod);
            for (var ix = x*(int) cubeFactor.x; ix < (x + 1)*cubeFactor.x; ix++)
            {
                for (var iy = y*(int) cubeFactor.y; iy < (y + 1)*cubeFactor.y; iy++)
                {
                    for (var iz = z*(int) cubeFactor.z; iz < (z + 1)*cubeFactor.z; iz++)
                    {
                        var possibleNewCube = new PyriteCube
                        {
                            X = ix,
                            Y = iy,
                            Z = iz
                        };
                        if (
                            pyriteQuery.DetailLevels[newLod].Cubes.Contains(possibleNewCube))
                        {
                            var cubePos = pyriteLevel.GetWorldCoordinatesForCube(possibleNewCube);
                            var g =
                                (GameObject)
                                    Instantiate(PlaceHolderCube, new Vector3(-cubePos.x, cubePos.z + 600, -cubePos.y),
                                        Quaternion.identity);

                            g.transform.parent = gameObject.transform;
                            g.GetComponent<MeshRenderer>().material.color = _colorList[_colorSelector%_colorList.Length];
                            g.GetComponent<IsRendered>().SetCubePosition(ix, iy, iz, newLod, pyriteQuery, this);

                            g.transform.localScale = new Vector3(
                                pyriteLevel.WorldCubeScale.x,
                                pyriteLevel.WorldCubeScale.z,
                                pyriteLevel.WorldCubeScale.y);
                            _colorSelector++;
                            createdDetectors.Add(g);
                        }
                    }
                }
            }
            registerCreatedDetectorCubes(createdDetectors);
            yield break;
        }

        public IEnumerator LoadCube(PyriteQuery query, int x, int y, int z, int lod,
            Action<GameObject[]> registerCreatedObjects = null)
        {
            DebugLog("+LoadCube(L{3}:{0}_{1}_{2})", x, y, z, lod);
            var modelPath = query.GetModelPath(lod, x, y, z);
            var pyriteLevel =
                query.DetailLevels[lod];

            var buffer = new GeometryBuffer();

            WWW loader;
            if (!UseEbo)
            {
                if (!_objCache.ContainsKey(modelPath))
                {
                    _objCache[modelPath] = null;
                    yield return StartCoroutine(StartRequest(modelPath));
                    if (!UseWww)
                    {
                        var client = new RestClient(modelPath);
                        var request = new RestRequest(Method.GET);
                        request.AddHeader("Accept-Encoding", "gzip, deflate");
                        client.ExecuteAsync(request, (r, h) =>
                        {
                            if (r.RawBytes != null)
                            {
                                _objCache[modelPath] = r.Content;
                            }
                            else
                            {
                                Debug.LogError("Error getting model data");
                            }
                        });
                    }
                    else
                    {
                        loader = WwwExtensions.CreateWWW(modelPath + "?fmt=obj");
                        yield return loader;
                        if (!string.IsNullOrEmpty(loader.error))
                        {
                            Debug.LogError(loader.error);
                            yield break;
                        }
                        _objCache[modelPath] = loader.GetDecompressedText();
                    }
                    while (_objCache[modelPath] == null)
                    {
                        yield return null;
                    }
                    EndRequest(modelPath);
                }
                while (_objCache[modelPath] == null)
                {
                    yield return null;
                }
                CubeBuilderHelpers.SetGeometryData(_objCache[modelPath], buffer);
            }
            else
            {
                if (!_eboCache.ContainsKey(modelPath))
                {
                    _eboCache[modelPath] = null;
                    yield return StartCoroutine(StartRequest(modelPath));
                    if (!UseWww)
                    {
                        var client = new RestClient(modelPath);
                        var request = new RestRequest(Method.GET);
                        request.AddHeader("Accept-Encoding", "gzip, deflate");
                        client.ExecuteAsync(request, (r, h) =>
                        {
                            if (r.RawBytes != null)
                            {
                                _eboCache[modelPath] = r.RawBytes;
                            }
                            else
                            {
                                Debug.LogError("Error getting model data");
                            }
                        });
                    }
                    else
                    {
                        loader = WwwExtensions.CreateWWW(modelPath);
                        yield return loader;
                        if (!string.IsNullOrEmpty(loader.error))
                        {
                            Debug.LogError(loader.error);
                            yield break;
                        }
                        _eboCache[modelPath] = loader.GetDecompressedBytes();
                    }
                    while (_eboCache[modelPath] == null)
                    {
                        yield return null;
                    }
                    EndRequest(modelPath);
                }
                // Loop while other tasks finish getting ebo data
                while (_eboCache[modelPath] == null)
                {
                    // Another task is in the process of filling out this cache entry.
                    // Loop until it is set
                    yield return null;
                }
                buffer.EboBuffer = _eboCache[modelPath];
            }

            var textureCoordinates = pyriteLevel.TextureCoordinatesForCube(x, y);
            var materialDataKey = string.Format("model.mtl_{0}_{1}_{2}", textureCoordinates.x, textureCoordinates.y, lod);
            if (!_materialDataCache.ContainsKey(materialDataKey))
            {
                var materialData = new List<MaterialData>();
                _materialDataCache[materialDataKey] = null;
                CubeBuilderHelpers.SetDefaultMaterialData(materialData, (int) textureCoordinates.x,
                    (int) textureCoordinates.y, lod);

                foreach (var m in materialData)
                {
                    var texturePath = query.GetTexturePath(query.GetLodKey(lod), (int) textureCoordinates.x,
                        (int) textureCoordinates.y);
                    if (!_textureCache.ContainsKey(texturePath))
                    {
                        // Set to null to signal to other tasks that the key is in the process
                        // of being filled
                        _textureCache[texturePath] = null;
                        yield return StartCoroutine(StartRequest(texturePath));
                        byte[] textureData = null;
                        if (!UseWww)
                        {
                            var client = new RestClient(texturePath);
                            var request = new RestRequest(Method.GET);
                            request.AddHeader("Accept-Encoding", "gzip, deflate");
                            client.ExecuteAsync(request, (r, h) =>
                            {
                                if (r.RawBytes != null)
                                {
                                    textureData = r.RawBytes;
                                }
                                else
                                {
                                    Debug.LogError("Error getting texture data");
                                }
                            });
                        }
                        else
                        {
                            // Do not request compression for textures
                            var texloader = WwwExtensions.CreateWWW(texturePath, false);
                            yield return texloader;
                            if (!string.IsNullOrEmpty(texloader.error))
                            {
                                Debug.LogError("Error getting texture: " + texloader.error);
                            }
                            textureData = texloader.bytes;
                        }

                        while (textureData == null)
                        {
                            yield return null;
                        }

                        var texture = new Texture2D(1, 1, TextureFormat.DXT1, false);
                        texture.LoadImage(textureData);
                        _textureCache[texturePath] = texture;
                        EndRequest(texturePath);
                    }

                    // Loop while other tasks finish creating texture
                    while (_textureCache[texturePath] == null)
                    {
                        // Another task is in the process of filling out this cache entry.
                        // Loop until it is set
                        yield return null;
                    }
                    m.DiffuseTex = _textureCache[texturePath];
                }
                _materialDataCache[materialDataKey] = materialData;
            }
            while (_materialDataCache[materialDataKey] == null)
            {
                // Another task is in the process of filling out this cache entry.
                // Loop until it is set
                yield return null;
            }
            Build(buffer, _materialDataCache[materialDataKey], x, y, z, lod, registerCreatedObjects);
            DebugLog("-LoadCube(L{3}:{0}_{1}_{2})", x, y, z, lod);
        }

        private void Build(GeometryBuffer buffer, List<MaterialData> materialData, int x, int y, int z, int lod,
            Action<GameObject[]> registerCreatedObjects)
        {
            var materials = new Dictionary<string, Material[]>();

            foreach (var md in materialData)
            {
                if (!_materialCache.ContainsKey(md.Name))
                {
                    _materialCache[md.Name] = CubeBuilderHelpers.GetMaterial(UseUnlitShader, md);
                }
                materials.Add(md.Name, _materialCache[md.Name]);
            }

            var ms = new GameObject[buffer.NumObjects];

            for (var i = 0; i < buffer.NumObjects; i++)
            {
                var go = new GameObject();
                go.name = String.Format("cube_L{4}:{0}_{1}_{2}.{3}", x, y, z, i, lod);
                go.transform.parent = gameObject.transform;
                go.AddComponent(typeof (MeshFilter));
                go.AddComponent(typeof (MeshRenderer));
                ms[i] = go;
            }

            if (registerCreatedObjects != null)
            {
                registerCreatedObjects(ms);
            }

            buffer.PopulateMeshes(ms, materials);
        }
    }
}