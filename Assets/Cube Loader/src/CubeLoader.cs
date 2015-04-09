using Assets.Cube_Loader.Extensions;
using ICSharpCode.SharpZipLib.GZip;
using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Assets.Cube_Loader.src;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class CubeLoader : MonoBehaviour {

    private bool readyToBuild = false;
    private bool processTextures = true;

    private int cubeCount = -1;
    private int textureCount = 0;

    public PyriteQuery PyriteQuery { get; private set; }
    public string PyriteServer;
    public string SetName;
    public string ModelVersion;

    public int DetailLevel = 6;
    private string LOD
    {
        get { return "L" + DetailLevel; }
    }

    public bool UseUnlitShader = true;
    public bool UseEbo = true;
    public Camera Camera;

    public bool UseCameraDetection = false;
    public GameObject CameraRig;

    public bool EnableDebugLogs = false;
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public GameObject PlaceHolderCube;

    //queuing
    private Queue<Cube> loadingQueue = new Queue<Cube>();
    private Queue<Cube> buildingQueue = new Queue<Cube>();
    private Queue<Cube> textureQueue = new Queue<Cube>();

    private Color[] colorList = { Color.gray, Color.yellow, Color.cyan };

    private TextureLoader textureLoader;

    void DebugLog(string fmt, params object[] args)
    {
        if (EnableDebugLogs)
        {
            string content = string.Format(fmt, args);
            Debug.LogFormat("{0}: {1}", _sw.ElapsedMilliseconds, content);
        }
    }


    public CubeLoader()
    {

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
        StartCoroutine(Load());
    }

    void Update()
    {
        StartCoroutine(ProcessQueue());
    }

    public IEnumerator Load()
    {
        // load the cube meta data
        PyriteQuery = new PyriteQuery(PyriteServer);
        yield return StartCoroutine(PyriteQuery.Load());

        var pyriteLevel =
            PyriteQuery.Sets[SetName].Versions[ModelVersion].DetailLevels[LOD];

        // load the material files
        // yield return StartCoroutine(LoadMaterials());

        List<MaterialData> materialDataList = new List<MaterialData>();
        LoadDefaultMaterials(pyriteLevel, materialDataList);

        // testing preloading the textures data on a background thread - by this point, we should have the data we need to start downloading
        textureLoader = new TextureLoader(PyriteQuery, materialDataList, UseUnlitShader);
        textureLoader.DownloadCompleted += (s, e) =>
        {
            readyToBuild = true;
        };

        yield return StartCoroutine(textureLoader.DownloadTextures(SetName, ModelVersion, pyriteLevel));

        if (Camera != null || CameraRig != null)
        {
            Transform cTransform = Camera == null ? CameraRig.transform : Camera.transform;
            DebugLog("Moving camera");
            // Hardcoding some values for now   
            var newCameraPosition = pyriteLevel.WorldBoundsMin + (pyriteLevel.WorldBoundsSize) / 2.0f;
            newCameraPosition += new Vector3(0, 0, pyriteLevel.WorldBoundsSize.z * 1.4f);
            cTransform.position = newCameraPosition;

            cTransform.rotation = Quaternion.Euler(0, 180, 0);

            DebugLog("Done moving camera");
        }

        int colorSelector = 0;
        for (int i = 0; i < pyriteLevel.Cubes.Length; i++)
        {
            int x = pyriteLevel.Cubes[i].X;
            int y = pyriteLevel.Cubes[i].Y;
            int z = pyriteLevel.Cubes[i].Z;
            if (UseCameraDetection)
            {
                float xPos = pyriteLevel.WorldBoundsMin.x + pyriteLevel.WorldBoundsSize.x / pyriteLevel.SetSize.x * x +
                             pyriteLevel.WorldBoundsSize.x / pyriteLevel.SetSize.x * 0.5f;
                float yPos = pyriteLevel.WorldBoundsMin.y + pyriteLevel.WorldBoundsSize.y / pyriteLevel.SetSize.y * y +
                             pyriteLevel.WorldBoundsSize.y / pyriteLevel.SetSize.y * 0.5f;
                float zPos = pyriteLevel.WorldBoundsMin.z + pyriteLevel.WorldBoundsSize.z / pyriteLevel.SetSize.z * z +
                             pyriteLevel.WorldBoundsSize.z / pyriteLevel.SetSize.z * 0.5f;

                GameObject g =
                    (GameObject)
                        Instantiate(PlaceHolderCube, new Vector3(-xPos, zPos + 600, -yPos), Quaternion.identity);


                g.transform.parent = gameObject.transform;
                g.GetComponent<MeshRenderer>().material.color = colorList[colorSelector % 3];
                g.GetComponent<IsRendered>().SetCubePosition(x, y, z, PyriteQuery, this);

                g.transform.localScale = new Vector3(
                    pyriteLevel.WorldBoundsSize.x / pyriteLevel.SetSize.x,
                    pyriteLevel.WorldBoundsSize.z / pyriteLevel.SetSize.z,
                    pyriteLevel.WorldBoundsSize.y / pyriteLevel.SetSize.y);
                colorSelector++;
            }
            else
            {
                AddToQueue(new Cube() { MapPosition = new UnityEngine.Vector3(x, y, z), Query = PyriteQuery });
            }
        }

        yield return null;
    }

    private void LoadDefaultMaterials(PyriteSetVersionDetailLevel detailLevel, List<MaterialData> materiaDatas)
    {
        for (int textureX = 0; textureX < detailLevel.TextureSetSize.x; textureX++)
        {
            for (int textureY = 0; textureY < detailLevel.TextureSetSize.y; textureY++)
            {
                CubeBuilderHelpers.SetDefaultMaterialData(materiaDatas, textureX, textureY);
            }
        }
    }

    public void AddToQueue(Cube cube)
    {
        if (cubeCount == -1)
            cubeCount = 0;

        cubeCount++;
        loadingQueue.Enqueue(cube);
    }

    public IEnumerator ProcessQueue()
    {
        yield return StartCoroutine(ProcessLoadQueue());

        if (readyToBuild)
        {
            yield return StartCoroutine(textureLoader.CreateTexturesAndMaterials());
            yield return StartCoroutine(ProcessBuildQueue());
        }
    }

    private IEnumerator ProcessLoadQueue()
    {
        while (loadingQueue.Count > 0)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(LoadCubue), loadingQueue.Dequeue());
            yield return null;
        }
    }

    public IEnumerator ProcessBuildQueue()
    {
        while (buildingQueue.Count > 0)
        {
            Cube cube = buildingQueue.Dequeue();

            yield return StartCoroutine(BuildCube(cube));
            textureLoader.MapTextures(cube);
            DebugLog("Done building: {0} {1} {2}", cube.MapPosition.x, cube.MapPosition.y, cube.MapPosition.z);
        }
    }

    private void LoadCubue(object state)
    {
        Cube cube = (Cube)state;
        float x = cube.MapPosition.x;
        float y = cube.MapPosition.y;
        float z = cube.MapPosition.z;

        Debug.Log(string.Format("+LoadCube({0}_{1}_{2})", cube.MapPosition.x, cube.MapPosition.y, cube.MapPosition.z));

        var modelPath = PyriteQuery.GetModelPath(SetName, ModelVersion, LOD, (int)x, (int)y, (int)z);

        if(UseEbo)
        {
            ProcessEbo(modelPath, cube);
        }
    }

    private void ProcessEbo(string path, Cube cube)
    {
        var eboPath = path.Replace(".obj", ".ebo");

        GeometryBuffer buffer = new GeometryBuffer();
        cube.MaterialData = new List<MaterialData>();

        RestClient client = new RestClient(eboPath);
        RestRequest request = new RestRequest(Method.GET);
        request.AddHeader("Accept-Encoding", "gzip, deflate");
        client.ExecuteAsync(request, (r, h) =>
        {
            if(r.RawBytes !=null)
            {
                buffer.eboBuffer = r.RawBytes;
                cube.Buffer = buffer;
                
                buildingQueue.Enqueue(cube);
                textureQueue.Enqueue(cube);
            }
        });
    }

    private byte[] DecompressBytes(byte[] rawBytes)
    {
        byte[] buffer = new byte[4096];
        using (var stream = new MemoryStream(rawBytes))
        using (var gzip = new GZipInputStream(stream))
        using (var outMs = new MemoryStream(rawBytes.Length))
        {
            int bytesRead = 0;
            while ((bytesRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                outMs.Write(buffer, 0, bytesRead);
            }
            return outMs.ToArray();
        }
    }

    private IEnumerator BuildCube(Cube cube)
    {
        GameObject[] ms = new GameObject[cube.Buffer.numObjects];

        for (int i = 0; i < cube.Buffer.numObjects; i++)
        {
            GameObject go = new GameObject();
            go.name = String.Format("cube_{0}_{1}_{2}.{3}", cube.MapPosition.x, cube.MapPosition.y, cube.MapPosition.z, i);
            go.transform.parent = gameObject.transform;
            go.AddComponent(typeof(MeshFilter));
            go.AddComponent(typeof(MeshRenderer));
            ms[i] = go;
        }

        cube.GameObject = ms[0];
        cube.Buffer.PopulateMeshes(ms);
        yield return null;
    }
}
