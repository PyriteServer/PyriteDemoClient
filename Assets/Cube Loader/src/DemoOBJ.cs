using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

public class DemoOBJ : MonoBehaviour {

    public string ObjectIndex;

    public int Viewport = 1;

    public bool UseUnlitShader = false;
    
    /* OBJ file tags */
    private const string O 	= "o";
    private const string G 	= "g";
    private const string V 	= "v";
    private const string VT = "vt";
    private const string VN = "vn";
    private const string F 	= "f";
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

    private readonly Dictionary<string, List<MaterialData>> materialCache = new Dictionary<string, List<MaterialData>>();
    private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    
    void Start ()
    {
        if (string.IsNullOrEmpty(ObjectIndex))
        {
            Debug.LogWarning("ObjectIndex needs to be set. Skipping cube loading attempt.");
        }
        else
        {
            StartCoroutine(Load());
        }
    }
    
    public IEnumerator Load()
    {
        CubeQuery query = new CubeQuery(ObjectIndex, this);
        yield return StartCoroutine(query.Load());

        var vlevel = query.VLevels[Viewport];
        var cubeMap = vlevel.CubeMap;

        int xMax = cubeMap.GetLength(0);
        int yMax = cubeMap.GetLength(1);
        int zMax = cubeMap.GetLength(2);

        var objectLocationFormat = query.CubeTemplate.Replace("{x}", "{0}")
            .Replace("{y}", "{1}")
            .Replace("{z}", "{2}")
            .Replace("{v}", Viewport.ToString());

        for (int x = 0; x<xMax; x++)
        {
            for (int y = 0; y<yMax; y++)
            {
                for(int z = 0; z<zMax; z++) 
                {
                    if (cubeMap[x, y, z])
                    {
                        String path = string.Format(objectLocationFormat, x, y, z);
                        if (basepath == null)
                        {
                            basepath = (path.IndexOf("/") == -1) ? "" : path.Substring(0, path.LastIndexOf("/") + 1);
                        }

                        GeometryBuffer buffer = new GeometryBuffer();
                        List<MaterialData> materialData = new List<MaterialData>();

                        WWW loader = new WWW(path);
                        yield return loader;
                        if (!string.IsNullOrEmpty(loader.error))
                        {
                            continue;
                        }
                        SetGeometryData(loader.text, buffer);

                        if (hasMaterials)
                        {
                            if (!materialCache.ContainsKey(mtllib))
                            {
                                loader = new WWW(basepath + mtllib);
                                yield return loader;
                                SetMaterialData(loader.text, materialData);
                                materialCache[mtllib] = materialData;
                            }
                            materialData = materialCache[mtllib];

                            foreach (MaterialData m in materialData)
                            {
                                if (m.diffuseTexPath != null)
                                {
                                    if (!textureCache.ContainsKey(m.diffuseTexPath))
                                    {
                                        WWW texloader = new WWW(basepath + m.diffuseTexPath);
                                        yield return texloader;
                                        textureCache[m.diffuseTexPath] = texloader.texture;
                                    }
                                    m.diffuseTex = textureCache[m.diffuseTexPath];
                                }
                            }
                        }

                        Build(buffer, materialData);
                    }
                    else
                    {
                        // Debug.Log(String.Format("Skipping [{0},{1},{2}]", x, y, z));
                    }
                }
            }
        }
    }
    
    private void SetGeometryData(string data, GeometryBuffer buffer) {
        string[] lines = data.Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        
        for(int i = 0; i < lines.Length; i++) {
            string l = lines[i].Trim ();
            
            if(l.IndexOf("#") != -1) l = l.Substring(0, l.IndexOf("#"));
            string[] p = l.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (p.Length > 1)
            {
                switch(p[0]) {
                case O:
                    buffer.PushObject(p[1].Trim());
                    break;
                case G:
                    buffer.PushGroup(p[1].Trim());
                    break;
                case V:
                    buffer.PushVertex( new Vector3(  cf(p[1]), cf(p[2]), cf(p[3]) ) );
                    break;
                case VT:
                    buffer.PushUV(new Vector2( cf(p[1]), cf(p[2]) ));
                    break;
                case VN:
                    buffer.PushNormal(new Vector3( cf(p[1]), cf(p[2]), cf(p[3]) ));
                    break;
                case F:
                    for(int j = 1; j < p.Length; j++) {
                        string[] c = p[j].Trim().Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        FaceIndices fi = new FaceIndices();
                        fi.vi = ci(c[0])-1;	
                        if(c.Length > 1 && c[1] != "") fi.vu = ci(c[1])-1;
                        if(c.Length > 2 && c[2] != "") fi.vn = ci(c[2])-1;
                        buffer.PushFace(fi);
                    }
                    break;
                case MTL:
                    mtllib = p[1].Trim();
                    // mtllib = "output.mtl";
                    break;
                case UML:
                    buffer.PushMaterialName(p[1].Trim());
                    break;
                }
            }
        }
        
        // buffer.Trace();
    }
    
    private float cf(string v) {
        return Convert.ToSingle(v.Trim(), new CultureInfo("en-US"));
    }
    
    private int ci(string v) {
        return Convert.ToInt32(v.Trim(), new CultureInfo("en-US"));
    }
    
    private bool hasMaterials {
        get {
            return mtllib != null;
        }
    }
    
    /* ############## MATERIALS */
    private class MaterialData {
        public string name;
        public Color ambient;
        public Color diffuse;
        public Color specular;
        public float shininess;
        public float alpha;
        public int illumType;
        public string diffuseTexPath;
        public Texture2D diffuseTex;
    }

    private void SetMaterialData(string data, List<MaterialData> materialData) {
        string[] lines = data.Split("\n".ToCharArray());

        MaterialData current = new MaterialData();
        
        for(int i = 0; i < lines.Length; i++) {
            string l = lines[i];
            
            if(l.IndexOf("#") != -1) l = l.Substring(0, l.IndexOf("#"));
            string[] p = l.Split(" ".ToCharArray());
            
            switch(p[0]) {
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
    
    private Material GetMaterial(MaterialData md) {
        Material m;

        // Use an unlit shader for the model if set
        if (UseUnlitShader)
        {
            m = new Material(Shader.Find(("Unlit/Texture")));
        }
        else
        {
            if (md.illumType == 2)
            {
                m = new Material(Shader.Find("Specular"));
                m.SetColor("_SpecColor", md.specular);
                m.SetFloat("_Shininess", md.shininess);
            }
            else
            {
                m = new Material(Shader.Find("Diffuse"));
            }

            m.SetColor("_Color", md.diffuse);
        }

        if(md.diffuseTex != null) m.SetTexture("_MainTex", md.diffuseTex);
        
        return m;
    }
    
    private Color gc(string[] p) {
        return new Color( cf(p[1]), cf(p[2]), cf(p[3]) );
    }
    
    private void Build(GeometryBuffer buffer, List<MaterialData> materialData) {
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        
        if(hasMaterials) {
            foreach(MaterialData md in materialData) {
                materials.Add(md.name, GetMaterial(md));
            }
        } else {
            materials.Add("default", new Material(Shader.Find("VertexLit")));
        }
        
        GameObject[] ms = new GameObject[buffer.numObjects];
        
        for(int i = 0; i < buffer.numObjects; i++) {
            GameObject go = new GameObject();
            go.transform.parent = gameObject.transform;
            go.AddComponent(typeof(MeshFilter));
            go.AddComponent(typeof(MeshRenderer));
            ms[i] = go;
        }
        
        buffer.PopulateMeshes(ms, materials);
    }
}








