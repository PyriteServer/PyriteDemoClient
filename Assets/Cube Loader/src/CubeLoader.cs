using Assets.Cube_Loader.Extensions;
using ICSharpCode.SharpZipLib.GZip;
using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;

public class CubeLoader : MonoBehaviour {
    /* OBJ file tags */
    private const string O = "o";
    private const string G = "g";
    private const string V = "v";
    private const string VT = "vt";
    private const string VN = "vn";
    private const string F = "f";
    private const string MTL = "mtllib";
    private const string UML = "usemtl";

    /* MTL file tags */
    private const string NML = "newmtl";
    private const string NS = "Ns"; // Shininess
    private const string KA = "Ka"; // Ambient component (not supported)
    private const string KD = "Kd"; // Diffuse component
    private const string KS = "Ks"; // Specular component
    private const string D = "d"; 	// Transparency (not supported)
    private const string TR = "Tr";	// Same as 'd'
    private const string ILLUM = "illum"; // Illumination model. 1 - diffuse, 2 - specular
    private const string MAP_KD = "map_Kd"; // Diffuse texture (other textures are not supported)

    private string basepath;
    private string mtllib;
    private string mtlOverride;

    private bool readyToBuild = false;
    private bool processTextures = true;

    private int cubeCount = -1;
    private int cubesBuilt = 0;
    private int textureCount = 0;

    public CubeQuery Query { get; private set; }

    public string ObjectIndex;
    public int Viewport = 1;
    public bool UseUnlitShader = false;
    public bool UseDividedTexture = false;
    public bool UseEbo = false;
    public Camera Camera;
    public bool UseOldQuadShader = false;

    public int LOD0 = 2;
    public int LOD1 = 4;
    public int LOD2 = 6;

    // caching
    private readonly Dictionary<string, List<MaterialData>> materialDataCache = new Dictionary<string, List<MaterialData>>();
    private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, Material[]> materialCache = new Dictionary<string, Material[]>();

    //queuing
    private Queue<Cube> loadingQueue = new Queue<Cube>();
    private Queue<Cube> buildingQueue = new Queue<Cube>();
    private Queue<Cube> materialQueue = new Queue<Cube>();
    private Queue<Cube> textureQueue = new Queue<Cube>();

    private List<MaterialData> materialDataList = new List<MaterialData>();

    private event EventHandler MeshCompleted;

    private TextureLoader textureLoader;

    private LODGroup lodGroup;

    public CubeLoader()
    {

    }

    void Start()
    {
        StartCoroutine(Load());
    }

    void Update()
    {
        StartCoroutine(ProcessQueue());
    }

    public IEnumerator Load()
    {
        lodGroup = gameObject.GetComponent<LODGroup>();

        // load the cube meta data
        Query = new CubeQuery(ObjectIndex, this);
        yield return StartCoroutine(Query.NewLoad());
        yield return StartCoroutine(Query.LoadViewPorts());

        mtlOverride = Query.MtlTemplate.Replace("{v}", Viewport.ToString());

        // load the material files
        yield return StartCoroutine(LoadMaterials());
        //yield return StartCoroutine(ProcessTextures()); 
        
        // testing preloading the textures data on a background thread - by this point, we should have the data we need to start downloading
        textureLoader = new TextureLoader(Query, materialDataList);
        textureLoader.UseOldQuadShader = UseOldQuadShader;
        textureLoader.DownloadCompleted += (s, e) =>
        {
            readyToBuild = true;
        };

        yield return StartCoroutine(textureLoader.DownloadTextures());

        var vlevel = Query.VLevels[Viewport];
        var cubeMap = vlevel.CubeMap;

        if (Camera != null)
        {
            //DebugLog("Moving camera");
            // Hardcoding some values for now   
            var x = vlevel.MinExtent.x + (vlevel.Size.x / 2.0f);
            var y = vlevel.MinExtent.y + (vlevel.Size.y / 2.0f);
            var z = vlevel.MinExtent.z + (vlevel.Size.z / 2.0f) + (vlevel.Size.z * 5);
            Camera.transform.position = new Vector3(x, y, z);

            Camera.transform.rotation = Quaternion.Euler(0, 180, 0);

            //DebugLog("Done moving camera");
        }

        int xMax = cubeMap.GetLength(0);
        int yMax = cubeMap.GetLength(1);
        int zMax = cubeMap.GetLength(2);

        for (int x = 0; x < xMax; x++)
        {
            for (int y = 0; y < yMax; y++)
            {
                for (int z = 0; z < zMax; z++)
                {
                    if (cubeMap[x, y, z])
                    {
                        AddToQueue(new Cube() { MapPosition = new UnityEngine.Vector3(x, y, z), Query = Query});
                    }
                }
            }
        }
        yield return null;
    }

    private IEnumerator LoadMaterials()
    {
        RestClient client = new RestClient(mtlOverride);
        RestRequest request = new RestRequest(Method.GET);
        RestRequestAsyncHandle handle = client.ExecuteAsync(request, (r, h) => {
            SetMaterialData(r.Content, materialDataList);
        });

        while(!handle.WebRequest.HaveResponse)
            yield return null;
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
        //yield return StartCoroutine(ProcessBuildQueue());

        if (readyToBuild)
        {
            yield return StartCoroutine(textureLoader.CreateTexturesAndMaterials());
            yield return StartCoroutine(ProcessBuildQueue());
        }

        //if(cubesBuilt == cubeCount)
        //{
        //    yield return StartCoroutine(textureLoader.CreateTexturesAndMaterials());
        //    yield return StartCoroutine(ProcessTextureQueue());
        //}
    }

    private IEnumerator ProcessLoadQueue()
    {
        while (loadingQueue.Count > 0)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessCube), loadingQueue.Dequeue());
            yield return null;
        }
    }

    public IEnumerator ProcessBuildQueue()
    {
        while (buildingQueue.Count > 0)
        {
            Cube cube = buildingQueue.Dequeue();

            yield return StartCoroutine(BuildCube(cube));
            yield return StartCoroutine(textureLoader.MapTextures(cube));
        }
    }

    private void ProcessCube(object state)
    {
        Cube cube = (Cube)state;
        float x = cube.MapPosition.x;
        float y = cube.MapPosition.y;
        float z = cube.MapPosition.z;

        Debug.Log(string.Format("+LoadCube({0}_{1}_{2})", cube.MapPosition.x, cube.MapPosition.y, cube.MapPosition.z));

        var objectLocationFormat = cube.Query.CubeTemplate.Replace("{x}", "{0}")
            .Replace("{y}", "{1}")
                .Replace("{z}", "{2}")
                .Replace("{v}", Viewport.ToString());

        String path = string.Format(objectLocationFormat, x, y, z);

        if(UseEbo)
        {
            ProcessEbo(path, cube);
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
                mtllib = "model.mtl";

                cube.Buffer = buffer;
                
                buildingQueue.Enqueue(cube);
                textureQueue.Enqueue(cube);
            }
        });
    }

    private void ProcessObj()
    {

    }

    private void ProcessMaterials(Cube cube)
    {
        RestClient client = new RestClient(mtlOverride);
        RestRequest request = new RestRequest(Method.GET);

        client.ExecuteAsync(request, (r, h) =>
        {
            if(!materialCache.ContainsKey(mtllib))
            {

            }

            SetMaterialData(r.Content, cube.MaterialData);

            buildingQueue.Enqueue(cube);
        });
    }

    private void SetMaterialData(string data, List<MaterialData> materialData)
    {
        string[] lines = data.Split("\n".ToCharArray());

        MaterialData current = new MaterialData();
        current.diffuseTexDivisions = 1;

        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i];

            if (l.IndexOf("#") != -1)
                l = l.Substring(0, l.IndexOf("#"));
            string[] p = l.Split(" ".ToCharArray());

            switch (p[0])
            {
                case NML:
                    current = new MaterialData();
                    current.name = p[1].Trim();
                    materialData.Add(current);
                    break;
                case KA:
                    current.ambient = gc(p);
                    break;
                case KD:
                    current.diffuse = gc(p);
                    break;
                case KS:
                    current.specular = gc(p);
                    break;
                case NS:
                    current.shininess = cf(p[1]) / 1000;
                    break;
                case D:
                case TR:
                    current.alpha = cf(p[1]);
                    break;
                case MAP_KD:
                    current.diffuseTexPath = p[1].Trim();
                    break;
                case ILLUM:
                    current.illumType = ci(p[1]);
                    break;

            }
        }
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

    private void SetGeometryData(string data, GeometryBuffer buffer)
    {
        string[] lines = data.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].Trim();

            if (l.IndexOf("#") != -1)
                l = l.Substring(0, l.IndexOf("#"));
            string[] p = l.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (p.Length > 1)
            {
                switch (p[0])
                {
                    case O:
                        buffer.PushObject(p[1].Trim());
                        break;
                    case G:
                        buffer.PushGroup(p[1].Trim());
                        break;
                    case V:
                        buffer.PushVertex(new Vector3(cf(p[1]), cf(p[2]), cf(p[3])));
                        break;
                    case VT:
                        buffer.PushUV(new Vector2(cf(p[1]), cf(p[2])));
                        break;
                    case VN:
                        buffer.PushNormal(new Vector3(cf(p[1]), cf(p[2]), cf(p[3])));
                        break;
                    case F:
                        for (int j = 1; j < p.Length; j++)
                        {
                            string[] c = p[j].Trim().Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            FaceIndices fi = new FaceIndices();
                            fi.vi = ci(c[0]) - 1;
                            if (c.Length > 1 && c[1] != "")
                                fi.vu = ci(c[1]) - 1;
                            if (c.Length > 2 && c[2] != "")
                                fi.vn = ci(c[2]) - 1;
                            buffer.PushFace(fi);
                        }
                        break;
                    case MTL:
                        mtllib = p[1].Trim();
                        break;
                    case UML:
                        buffer.PushMaterialName(p[1].Trim());
                        break;
                }
            }
        }

        // buffer.Trace();
    }
    
    private float cf(string v)
    {
        return Convert.ToSingle(v.Trim(), new CultureInfo("en-US"));
    }

    private int ci(string v)
    {
        return Convert.ToInt32(v.Trim(), new CultureInfo("en-US"));
    }

    private Color gc(string[] p)
    {
        return new Color(cf(p[1]), cf(p[2]), cf(p[3]));
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
        cubesBuilt++;
        yield return null;
    }
}
