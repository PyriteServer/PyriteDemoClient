using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System;
using Assets.Cube_Loader.Extensions;
using RestSharp;

public class CubeQuery
{
    public int MinimumViewport { get; private set; }
    public int MaximumViewport { get; private set; }
    public string CubeTemplate { get; private set; }
    public string MtlTemplate { get; private set; }
    public string JpgTemplate { get; private set; }
    public string MetadataTemplate { get; private set; }
    public int TextureSubdivide { get; private set; }
    public string TexturePath { get; private set; }
    public string BasePath { get; set; }

    public Dictionary<int, VLevelQuery> VLevels { get; set; }

    private readonly string indexUrl;

    private readonly MonoBehaviour behavior;

    private JSONNode index;

    public CubeQuery(string sceneIndexUrl, MonoBehaviour behaviour)
    {
        indexUrl = sceneIndexUrl;
        this.behavior = behaviour;
    }

    public IEnumerator NewLoad()
    {
        Debug.Log("CubeQuery started against: " + indexUrl);
        RestClient client = new RestClient(indexUrl);
        

        //yield return client.GetAsync(new RestRequest(), NewLoadCallback);
        var handle = client.GetAsync(new RestRequest(), (r, h) =>
        {
            index = JSON.Parse(r.Content);
            MinimumViewport = index["MinimumViewport"].AsInt;
            MaximumViewport = index["MaximumViewport"].AsInt;
            CubeTemplate = index["CubeTemplate"].Value;
            MtlTemplate = index["MtlTemplate"].Value;
            JpgTemplate = index["JpgTemplate"].Value;
            MetadataTemplate = index["MetadataTemplate"].Value;
            TextureSubdivide = index["TextureSubdivide"].AsInt;
            TexturePath = index["TexturePath"].Value;

            BasePath = (CubeTemplate.IndexOf("/") == -1) ? "" : CubeTemplate.Substring(0, CubeTemplate.LastIndexOf("/") + 1);

            PopulateViewports();
        });

        while (!handle.WebRequest.HaveResponse)
            yield return null;
    }

    public void PopulateViewports()
    {
        //Populate Viewports
        VLevels = new Dictionary<int, VLevelQuery>();
        for (int i = MinimumViewport; i <= MaximumViewport; i++)
        {
            string path = MetadataTemplate.Replace("{v}", i.ToString());
            var vlevel = new VLevelQuery(i, path);
            //yield return behavior.StartCoroutine(vlevel.Load());
            VLevels.Add(i, vlevel);
        }
    }

    public IEnumerator LoadViewPorts()
    {
        foreach(var vlevel in VLevels.Values)
        {
            //yield return behavior.StartCoroutine(vlevel.Load());
            yield return behavior.StartCoroutine(vlevel.NewLoad());
        }
    }

    public IEnumerator Load()
    {
        Debug.Log("CubeQuery started against: " + indexUrl);
        WWW loader = WWWExtensions.CreateWWW(path: indexUrl);
        yield return loader;

        var index = JSON.Parse(loader.GetDecompressedText());

        MinimumViewport = index["MinimumViewport"].AsInt;
        MaximumViewport = index["MaximumViewport"].AsInt;
        CubeTemplate = index["CubeTemplate"].Value;
        MtlTemplate = index["MtlTemplate"].Value;
        JpgTemplate = index["JpgTemplate"].Value;
        MetadataTemplate = index["MetadataTemplate"].Value;
        TextureSubdivide = index["TextureSubdivide"].AsInt;
        TexturePath = index["TexturePath"].Value;


        // Populate Viewports
        VLevels = new Dictionary<int, VLevelQuery>();
        for (int i = MinimumViewport; i <= MaximumViewport; i++)
        {
            string path = MetadataTemplate.Replace("{v}", i.ToString());
            var vlevel = new VLevelQuery(i, path);
            yield return behavior.StartCoroutine(vlevel.Load());
            VLevels.Add(i, vlevel);
        }
    }

}

// Describes a viewport level metadata set. 
// Yes, I've madeup 'viewport level' or 'vlevel' as the term for the 
// level of accuracy you might need in a given viewport.
public class VLevelQuery
{
    public int ViewportLevel { get; private set; }
    public bool[, ,] CubeMap { get; private set; }

    private readonly string metadataUrl;

    public Vector3 MinExtent { get; private set; }
    public Vector3 MaxExtent { get; private set; }
    public Vector3 Size { get; private set; }

    public VLevelQuery(int viewportLevel, string viewportMetadataUrl)
    {
        ViewportLevel = viewportLevel;
        metadataUrl = viewportMetadataUrl;
    }

    public IEnumerator NewLoad()
    {
        RestClient client = new RestClient(metadataUrl);
        RestRequest request = new RestRequest(Method.GET);
        RestRequestAsyncHandle handle = client.ExecuteAsync(request, (r, h) =>
        {
            // POPULATE THE BOOL ARRAY...
            var metadata = JSON.Parse(r.Content);
            int xMax = metadata["GridSize"]["X"].AsInt;
            int yMax = metadata["GridSize"]["Y"].AsInt;
            int zMax = metadata["GridSize"]["Z"].AsInt;

            CubeMap = new bool[xMax, yMax, zMax];

            var cubeExists = metadata["CubeExists"];
            for (int x = 0; x < xMax; x++)
            {
                for (int y = 0; y < yMax; y++)
                {
                    for (int z = 0; z < zMax; z++)
                    {
                        CubeMap[x, y, z] = cubeExists[x][y][z].AsBool;
                    }
                }
            }

            var extents = metadata["Extents"];
            MinExtent = new Vector3(extents["XMin"].AsFloat, extents["YMin"].AsFloat, extents["ZMin"].AsFloat);
            MaxExtent = new Vector3(extents["XMax"].AsFloat, extents["YMax"].AsFloat, extents["ZMax"].AsFloat);
            Size = new Vector3(extents["XSize"].AsFloat, extents["YSize"].AsFloat, extents["ZSize"].AsFloat);
        });

        while (!handle.WebRequest.HaveResponse)
            yield return null;
    }

    public IEnumerator Load()
    {
        WWW loader = WWWExtensions.CreateWWW(path: metadataUrl);
        yield return loader;

        // POPULATE THE BOOL ARRAY...
        var metadata = JSON.Parse(loader.GetDecompressedText());
        int xMax = metadata["GridSize"]["X"].AsInt;
        int yMax = metadata["GridSize"]["Y"].AsInt;
        int zMax = metadata["GridSize"]["Z"].AsInt;

        CubeMap = new bool[xMax,yMax,zMax];

        var cubeExists = metadata["CubeExists"];
        for (int x = 0; x < xMax; x++)
        {
            for (int y = 0; y < yMax; y++)
            {
                for (int z = 0; z < zMax; z++)
                {
                    CubeMap[x, y, z] = cubeExists[x][y][z].AsBool;
                }
            }
        }

        var extents = metadata["Extents"];
        MinExtent = new Vector3(extents["XMin"].AsFloat, extents["YMin"].AsFloat, extents["ZMin"].AsFloat);
        MaxExtent = new Vector3(extents["XMax"].AsFloat, extents["YMax"].AsFloat, extents["ZMax"].AsFloat);
        Size = new Vector3(extents["XSize"].AsFloat, extents["YSize"].AsFloat, extents["ZSize"].AsFloat);
    }
}