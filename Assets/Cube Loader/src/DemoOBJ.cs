using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Assets.Cube_Loader.Extensions;
using Assets.Cube_Loader.src;
using Debug = UnityEngine.Debug;

public class DemoOBJ : MonoBehaviour
{
    public int DetailLevel = 6;
    public bool UseUnlitShader = false;
    public bool UseEbo = true;
    public string SetName;
    
    public string PyriteServer;

    public bool EnableDebugLogs = false;

    public bool UseCameraDetection = false;

    public GameObject PlaceHolderCube;

    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public GameObject CameraRig;

    private Color[] colorList = {Color.gray, Color.yellow, Color.cyan};

    int colorSelector = 0;

    private readonly Dictionary<string, List<MaterialData>> materialDataCache = new Dictionary<string, List<MaterialData>>();
    private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, Material[]> materialCache = new Dictionary<string, Material[]>();

    private const string ModelVersion = "V1";

    void DebugLog(string fmt, params object[] args)
    {
        if (EnableDebugLogs)
        {
            string content = string.Format(fmt, args);
            Debug.LogFormat("{0}: {1}", _sw.ElapsedMilliseconds, content);
        }
    }

    void Start()
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

        PyriteQuery pyriteQuery = new PyriteQuery(SetName, ModelVersion, PyriteServer);
        yield return StartCoroutine(pyriteQuery.Load());
        DebugLog("CubeQuery complete.");
        
        var pyriteLevel =
            pyriteQuery.Set.Version.DetailLevels[DetailLevel];

        if (CameraRig != null)
        {
            DebugLog("Moving camera");
            // Hardcoding some values for now   
            var newCameraPosition = pyriteLevel.WorldBoundsMin + (pyriteLevel.WorldBoundsSize) / 2.0f;
            newCameraPosition += new Vector3(0,0,pyriteLevel.WorldBoundsSize.z * 1.4f);
            CameraRig.transform.position = newCameraPosition;

            CameraRig.transform.rotation = Quaternion.Euler(0, 180, 0);

            DebugLog("Done moving camera");
        }

        for (int i = 0; i < pyriteLevel.Cubes.Length; i++)
        {
            int x = pyriteLevel.Cubes[i].X;
            int y = pyriteLevel.Cubes[i].Y;
            int z = pyriteLevel.Cubes[i].Z;
            var cubePos = pyriteLevel.GetWorldCoordinatesForCube(pyriteLevel.Cubes[i]);
            if (UseCameraDetection)
            {
                // Move cube to the orientation we want also move it up since the model is around -600
                GameObject g =
                    (GameObject)
                        Instantiate(PlaceHolderCube, new Vector3(-cubePos.x, cubePos.z + 600, -cubePos.y), Quaternion.identity);

                g.transform.parent = gameObject.transform;
                g.GetComponent<MeshRenderer>().material.color = colorList[colorSelector%3];
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

        DebugLog("-Load()");
    }

    public IEnumerator AddUpgradedDetectorCubes(PyriteQuery pyriteQuery, int x, int y, int z, int lod,
        Action<IEnumerable<GameObject>> registerCreatedDetectorCubes)
    {
        int newLod = lod - 1;
        List<GameObject> createdDetectors = new List<GameObject>();
        var pyriteLevel = pyriteQuery.Set.Version.DetailLevels[newLod];

        var cubeFactor = pyriteQuery.GetNextCubeFactor(lod);
        for (int ix = x*(int) cubeFactor.x; ix < (x + 1)*cubeFactor.x; ix++)
        {
            for (int iy = y*(int) cubeFactor.y; iy < (y + 1)*cubeFactor.y; iy++)
            {
                for (int iz = z*(int) cubeFactor.z; iz < (z + 1)*cubeFactor.z; iz++)
                {
                    var possibleNewCube = new PyriteCube()
                    {
                        X = ix,
                        Y = iy,
                        Z = iz
                    };

                    if (
                        pyriteQuery.Set.Version.DetailLevels[lod - 1].Cubes.Contains(possibleNewCube))
                    {
                        var cubePos = pyriteLevel.GetWorldCoordinatesForCube(possibleNewCube);
                        GameObject g =
                         (GameObject)
                             Instantiate(PlaceHolderCube, new Vector3(-cubePos.x, cubePos.z + 600, -cubePos.y), Quaternion.identity);

                        g.transform.parent = gameObject.transform;
                        g.GetComponent<MeshRenderer>().material.color = colorList[colorSelector % 3];
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

    public IEnumerator LoadCube(PyriteQuery query, int x, int y, int z, int lod, Action<GameObject[]> registerCreatedObjects = null)
    {
        DebugLog("+LoadCube(L{3}:{0}_{1}_{2})", x, y, z, lod);
        var modelPath = query.GetModelPath(lod, x, y, z);
        var pyriteLevel =
            query.Set.Version.DetailLevels[lod];

        GeometryBuffer buffer = new GeometryBuffer();
        List<MaterialData> materialData = new List<MaterialData>();

        WWW loader;
        if (!UseEbo)
        {
            loader = WWWExtensions.CreateWWW(path: modelPath + "?fmt=obj");
            yield return loader;
            if (!string.IsNullOrEmpty(loader.error))
            {
                Debug.LogError(loader.error);
                yield break;
            }
            CubeBuilderHelpers.SetGeometryData(loader.GetDecompressedText(), buffer);
        }
        else
        {
            loader = WWWExtensions.CreateWWW(path: modelPath);
            yield return loader;
            if (!string.IsNullOrEmpty(loader.error))
            {
                Debug.LogError(loader.error);
                yield break;
            }
            buffer.eboBuffer = loader.GetDecompressedBytes();
        }
        var textureCoordinates = pyriteLevel.TextureCoordinatesForCube(x, y);
        var materialDataKey = string.Format("model.mtl_{0}_{1}", textureCoordinates.x, textureCoordinates.y);
        if (!materialDataCache.ContainsKey(materialDataKey))
        {
            materialDataCache[materialDataKey] = null;
            CubeBuilderHelpers.SetDefaultMaterialData(materialData, (int)textureCoordinates.x, (int)textureCoordinates.y);

            foreach (MaterialData m in materialData)
            {

                var texturePath = query.GetTexturePath(query.GetLODKey(lod), (int)textureCoordinates.x,
                    (int)textureCoordinates.y);
                if (!textureCache.ContainsKey(texturePath))
                {
                    // Set to null to signal to other tasks that the key is in the process
                    // of being filled
                    textureCache[texturePath] = null;
                    // Do not request compression for textures
                    var texloader = WWWExtensions.CreateWWW(path: texturePath, requestCompression: false);
                    yield return texloader;
                    Texture2D texture = new Texture2D(1, 1, TextureFormat.DXT1, false);
                    texture.LoadImage(texloader.bytes);
                    //textures.Add(md.diffuseTexPath, texture);
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
        materialData = materialDataCache[materialDataKey];
        Build(buffer, materialData, x, y, z, lod, registerCreatedObjects);
        DebugLog("-LoadCube({0}_{1}_{2})", x, y, z);
    }

    private void Build(GeometryBuffer buffer, List<MaterialData> materialData, int x, int y, int z, int lod, Action<GameObject[]> registerCreatedObjects)
    {
        Dictionary<string, Material[]> materials = new Dictionary<string, Material[]>();

        foreach (MaterialData md in materialData)
        {

            if (!materialCache.ContainsKey(md.name))
            {
                materialCache[md.name] = CubeBuilderHelpers.GetMaterial(UseUnlitShader, md);
            }
            materials.Add(md.name, materialCache[md.name]);

        }

        GameObject[] ms = new GameObject[buffer.numObjects];

        for (int i = 0; i < buffer.numObjects; i++)
        {
            GameObject go = new GameObject();
            go.name = String.Format("cube_L{4}:{0}_{1}_{2}.{3}", x, y, z, i, lod);
            go.transform.parent = gameObject.transform;
            go.AddComponent(typeof(MeshFilter));
            go.AddComponent(typeof(MeshRenderer));
            ms[i] = go;
        }

        if (registerCreatedObjects != null)
        {
            registerCreatedObjects(ms);
        }

        buffer.PopulateMeshes(ms, materials);
    }
}