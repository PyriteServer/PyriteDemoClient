namespace Assets.Cube_Loader.src
{
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

    public class CubeLoader : MonoBehaviour
    {

        private bool readyToBuild = false;
        private bool processTextures = true;

        private int cubeCount = -1;
        private int textureCount = 0;

        public PyriteQuery PyriteQuery { get; private set; }
        public string PyriteServer;
        public string SetName;

        public int DetailLevel = 6;
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

        private Color[] colorList = {Color.gray, Color.yellow, Color.cyan};

        private TextureLoader textureLoader;

        public string ModelVersion = "V1";

        private void DebugLog(string fmt, params object[] args)
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
            StartCoroutine(Load());
        }

        private void Update()
        {
            StartCoroutine(ProcessQueue());
        }

        public IEnumerator Load()
        {
            // load the cube meta data
            PyriteQuery = new PyriteQuery(SetName, ModelVersion, PyriteServer);
            yield return StartCoroutine(PyriteQuery.Load());

            var pyriteLevel =
                PyriteQuery.DetailLevels[DetailLevel];

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

            yield return StartCoroutine(textureLoader.DownloadTextures(pyriteLevel));

            if (Camera != null || CameraRig != null)
            {
                Transform cTransform = Camera == null ? CameraRig.transform : Camera.transform;
                DebugLog("Moving camera");
                // Hardcoding some values for now   
                var newCameraPosition = pyriteLevel.WorldBoundsMin + (pyriteLevel.WorldBoundsSize)/2.0f;
                newCameraPosition += new Vector3(0, 0, pyriteLevel.WorldBoundsSize.z*1.4f);
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
                    var cubePos = pyriteLevel.GetWorldCoordinatesForCube(pyriteLevel.Cubes[i]);

                    // Move cube to the orientation we want also move it up since the model is around -600
                    GameObject g =
                        (GameObject)
                            Instantiate(PlaceHolderCube, new Vector3(-cubePos.x, cubePos.z + 600, -cubePos.y),
                                Quaternion.identity);


                    g.transform.parent = gameObject.transform;
                    g.GetComponent<MeshRenderer>().material.color = colorList[colorSelector%3];
                    g.GetComponent<IsRendered>().SetCubePosition(x, y, z, DetailLevel, PyriteQuery, this);

                    g.transform.localScale = new Vector3(
                        pyriteLevel.WorldCubeScale.x,
                        pyriteLevel.WorldCubeScale.z,
                        pyriteLevel.WorldCubeScale.y);
                    colorSelector++;
                }
                else
                {
                    AddToQueue(new Cube()
                    {
                        MapPosition = new UnityEngine.Vector3(x, y, z),
                        Query = PyriteQuery,
                        LOD = DetailLevel
                    });
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
            Cube cube = (Cube) state;
            float x = cube.MapPosition.x;
            float y = cube.MapPosition.y;
            float z = cube.MapPosition.z;

            Debug.Log(string.Format("+LoadCube({0}_{1}_{2})", cube.MapPosition.x, cube.MapPosition.y, cube.MapPosition.z));

            var modelPath = PyriteQuery.GetModelPath(DetailLevel, (int) x, (int) y, (int) z);

            if (UseEbo)
            {
                ProcessEbo(modelPath, cube);
            }
            else
            {
                ProcessObj(modelPath, cube);
            }
        }

        private void ProcessObj(string eboPath, Cube cube)
        {
            var objpath = eboPath + "?fmt=obj";
            GeometryBuffer buffer = new GeometryBuffer();
            cube.MaterialData = new List<MaterialData>();

            RestClient client = new RestClient(objpath);
            RestRequest request = new RestRequest(Method.GET);
            request.AddHeader("Accept-Encoding", "gzip, deflate");
            client.ExecuteAsync(request, (r, h) =>
            {
                if (r.Content != null)
                {
                    CubeBuilderHelpers.SetGeometryData(r.Content, buffer);
                    cube.Buffer = buffer;

                    buildingQueue.Enqueue(cube);
                    textureQueue.Enqueue(cube);
                }
            });
        }

        private void ProcessEbo(string eboPath, Cube cube)
        {
            GeometryBuffer buffer = new GeometryBuffer();
            cube.MaterialData = new List<MaterialData>();

            RestClient client = new RestClient(eboPath);
            RestRequest request = new RestRequest(Method.GET);
            request.AddHeader("Accept-Encoding", "gzip, deflate");
            client.ExecuteAsync(request, (r, h) =>
            {
                if (r.RawBytes != null)
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
                go.name = String.Format("cube_{0}_{1}_{2}.{3}", cube.MapPosition.x, cube.MapPosition.y,
                    cube.MapPosition.z, i);
                go.transform.parent = gameObject.transform;
                go.AddComponent(typeof (MeshFilter));
                go.AddComponent(typeof (MeshRenderer));
                ms[i] = go;
            }

            cube.GameObject = ms[0];
            cube.Buffer.PopulateMeshes(ms);
            yield return null;
        }
    }
}