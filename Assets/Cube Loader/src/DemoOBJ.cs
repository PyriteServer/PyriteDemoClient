namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Extensions;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    public class DemoOBJ : MonoBehaviour
    {
        public string ModelVersion = "V2";
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private readonly Color[] colorList =
        {
            Color.gray, Color.yellow, Color.cyan
        };

        private readonly Dictionary<string, byte[]> eboCache = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, Material[]> materialCache = new Dictionary<string, Material[]>();

        private readonly Dictionary<string, List<MaterialData>> materialDataCache =
            new Dictionary<string, List<MaterialData>>();

        private readonly Dictionary<string, string> objCache = new Dictionary<string, string>();
        private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        public GameObject CameraRig;
        private int colorSelector;
        public int DetailLevel = 6;
        public bool EnableDebugLogs = false;
        public GameObject PlaceHolderCube;
        public string PyriteServer;
        public string SetName;
        public bool UseCameraDetection = false;
        public bool UseEbo = true;
        public bool UseUnlitShader = true;

        private void DebugLog(string fmt, params object[] args)
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

            var pyriteQuery = new PyriteQuery(SetName, ModelVersion, PyriteServer);
            yield return StartCoroutine(pyriteQuery.Load());
            DebugLog("CubeQuery complete.");

            var pyriteLevel =
                pyriteQuery.DetailLevels[DetailLevel];


            float xmin, ymin, zmin, xmax, ymax, zmax;
            xmin = pyriteLevel.WorldBoundsMax.x;
            ymin = pyriteLevel.WorldBoundsMax.y;
            zmin = pyriteLevel.WorldBoundsMax.z;
            xmax = pyriteLevel.WorldBoundsMin.x;
            ymax = pyriteLevel.WorldBoundsMin.y;
            zmax = pyriteLevel.WorldBoundsMin.z;
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
                    g.GetComponent<MeshRenderer>().material.color = colorList[colorSelector%colorList.Length];
                    g.GetComponent<IsRendered>().SetCubePosition(x, y, z, DetailLevel, pyriteQuery, this);

                    g.transform.localScale = new Vector3(
                        pyriteLevel.WorldCubeScale.x,
                        pyriteLevel.WorldCubeScale.z,
                        pyriteLevel.WorldCubeScale.y);
                    colorSelector++;
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
                var newCameraPosition = min + (max - min) / 2.0f;
                newCameraPosition += new Vector3(0, 0, (max - min).z * 1.4f);
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
                            g.GetComponent<MeshRenderer>().material.color = colorList[colorSelector%colorList.Length];
                            g.GetComponent<IsRendered>().SetCubePosition(ix, iy, iz, newLod, pyriteQuery, this);

                            g.transform.localScale = new Vector3(
                                pyriteLevel.WorldCubeScale.x,
                                pyriteLevel.WorldCubeScale.z,
                                pyriteLevel.WorldCubeScale.y);
                            colorSelector++;
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
                if (!objCache.ContainsKey(modelPath))
                {
                    objCache[modelPath] = null;
                    loader = WWWExtensions.CreateWWW(modelPath + "?fmt=obj");
                    yield return loader;
                    if (!string.IsNullOrEmpty(loader.error))
                    {
                        Debug.LogError(loader.error);
                        yield break;
                    }
                    objCache[modelPath] = loader.GetDecompressedText();
                }
                while (objCache[modelPath] == null)
                {
                    yield return null;
                }
                CubeBuilderHelpers.SetGeometryData(objCache[modelPath], buffer);
            }
            else
            {
                if (!eboCache.ContainsKey(modelPath))
                {
                    eboCache[modelPath] = null;
                    loader = WWWExtensions.CreateWWW(modelPath);
                    yield return loader;
                    if (!string.IsNullOrEmpty(loader.error))
                    {
                        Debug.LogError(loader.error);
                        yield break;
                    }
                    eboCache[modelPath] = loader.GetDecompressedBytes();
                }
                // Loop while other tasks finish getting ebo data
                while (eboCache[modelPath] == null)
                {
                    // Another task is in the process of filling out this cache entry.
                    // Loop until it is set
                    yield return null;
                }
                buffer.eboBuffer = eboCache[modelPath];
            }

            var textureCoordinates = pyriteLevel.TextureCoordinatesForCube(x, y);
            var materialDataKey = string.Format("model.mtl_{0}_{1}_{2}", textureCoordinates.x, textureCoordinates.y, lod);
            if (!materialDataCache.ContainsKey(materialDataKey))
            {
                var materialData = new List<MaterialData>();
                materialDataCache[materialDataKey] = null;
                CubeBuilderHelpers.SetDefaultMaterialData(materialData, (int) textureCoordinates.x,
                    (int) textureCoordinates.y, lod);

                foreach (var m in materialData)
                {
                    var texturePath = query.GetTexturePath(query.GetLODKey(lod), (int) textureCoordinates.x,
                        (int) textureCoordinates.y);
                    if (!textureCache.ContainsKey(texturePath))
                    {
                        // Set to null to signal to other tasks that the key is in the process
                        // of being filled
                        textureCache[texturePath] = null;
                        // Do not request compression for textures
                        var texloader = WWWExtensions.CreateWWW(texturePath, false);
                        yield return texloader;
                        
                        if (!string.IsNullOrEmpty(texloader.error))
                        {
                            Debug.LogError("Error getting texture: " + texloader.error);
                        }
                        var texture = new Texture2D(1, 1, TextureFormat.DXT1, false);
                        texture.LoadImage(texloader.bytes);
                        textureCache[texturePath] = texture;
                    }

                    // Loop while other tasks finish creating texture
                    while (textureCache[texturePath] == null)
                    {
                        // Another task is in the process of filling out this cache entry.
                        // Loop until it is set
                        yield return null;
                    }
                    m.diffuseTex = textureCache[texturePath];
                }
                materialDataCache[materialDataKey] = materialData;
            }
            while (materialDataCache[materialDataKey] == null)
            {
                // Another task is in the process of filling out this cache entry.
                // Loop until it is set
                yield return null;
            }
            Build(buffer, materialDataCache[materialDataKey], x, y, z, lod, registerCreatedObjects);
            DebugLog("-LoadCube(L{3}:{0}_{1}_{2})", x, y, z, lod);
        }

        private void Build(GeometryBuffer buffer, List<MaterialData> materialData, int x, int y, int z, int lod,
            Action<GameObject[]> registerCreatedObjects)
        {
            var materials = new Dictionary<string, Material[]>();

            foreach (var md in materialData)
            {
                if (!materialCache.ContainsKey(md.Name))
                {
                    materialCache[md.Name] = CubeBuilderHelpers.GetMaterial(UseUnlitShader, md);
                }
                materials.Add(md.Name, materialCache[md.Name]);
            }

            var ms = new GameObject[buffer.numObjects];

            for (var i = 0; i < buffer.numObjects; i++)
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