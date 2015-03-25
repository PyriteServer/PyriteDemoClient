using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System;
using Assets.Cube_Loader.Extensions;

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

    public Dictionary<int, VLevelQuery> VLevels { get; set; }

    private readonly string indexUrl;

    private readonly MonoBehaviour behavior;

    public CubeQuery(string sceneIndexUrl, MonoBehaviour behaviour)
    {
        indexUrl = sceneIndexUrl;
        this.behavior = behaviour;
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

    public IEnumerator Load()
    {
        WWW loader = WWWExtensions.CreateWWW(path: metadataUrl);
        yield return loader;

        // POPULATE THE BOOL ARRAY...
        var metadata = JSON.Parse(loader.GetDecompressedText());
        if (metadata != null)
        {
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

            Debug.Log("Viewport: " + ViewportLevel);
            Debug.LogFormat("Exists: {0} {1} {2}", CubeMap.GetLength(0), CubeMap.GetLength(1), CubeMap.GetLength(2));
            float xBlockSize = Size.x / CubeMap.GetLength(0);
            float yBlockSize = Size.y / CubeMap.GetLength(1);
            float zBlockSize = Size.z / CubeMap.GetLength(2);
            Debug.LogFormat("CubeSize: {0} {1} {2}", xBlockSize, yBlockSize, zBlockSize);
        }
    }
}