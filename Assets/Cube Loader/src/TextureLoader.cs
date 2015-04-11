using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using RestSharp;
using System;
using Assets.Cube_Loader.src;

public class TextureLoader {
    public event EventHandler DownloadCompleted;

    public PyriteQuery Query { get; private set; }
    public PyriteSetVersionDetailLevel DetailLevel { get; set; }
    public List<MaterialData> MaterialDataList { get; private set; }
    public int TextureCount { get; private set; }

    private Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    public Dictionary<string, Texture2D> Textures
    {
        get { return textures; }
    }

    private readonly Dictionary<string, Material[]> materialCache = new Dictionary<string, Material[]>();

    // holds the downloaded texture data
    private Dictionary<string, byte[]> textureDataCache = new Dictionary<string, byte[]>();

    private bool UseUnlitShader { get; set; }

    public TextureLoader(PyriteQuery query, List<MaterialData> materialDataList, bool useUnlitShader)
    {
        Query = query;
        MaterialDataList = materialDataList;
        UseUnlitShader = useUnlitShader;
    }

    public IEnumerator DownloadTextures(PyriteSetVersionDetailLevel detailLevel)
    {
        DetailLevel = detailLevel;
        TextureCount = (int)detailLevel.TextureSetSize.x * (int)detailLevel.TextureSetSize.y;
        foreach (var md in MaterialDataList)
        {
            var texPath = Query.GetTexturePath(detailLevel.Name, md.x, md.y);
            md.diffuseTexPath = texPath;
            yield return ThreadPool.QueueUserWorkItem(StartDownloadTexture, texPath);    
        }
    }

    private void StartDownloadTexture(object state)
    {
        string texPath = (string)state;

        RestClient client = new RestClient();
        RestRequest texRequest = new RestRequest(texPath, Method.GET);
        client.ExecuteAsync(texRequest, (r, h) =>
        {
            if(r.ResponseStatus == ResponseStatus.Completed && r.RawBytes != null)
                textureDataCache.Add(texPath, r.RawBytes);

            // if this is the last texture to download, then signal that it's done
            if (textureDataCache.Count == TextureCount)
            {
                if (DownloadCompleted != null)
                    DownloadCompleted(this, EventArgs.Empty);
            }
        });
    }

    public IEnumerator CreateTexturesAndMaterials()
    {
        while(textures.Count < TextureCount)
        {
            foreach (var md in MaterialDataList)
            {
                if (!textures.ContainsKey(md.diffuseTexPath))
                {
                    byte[] textureData = textureDataCache[md.diffuseTexPath];
                    Texture2D texture = new Texture2D(1, 1, TextureFormat.DXT1, false);
                    texture.LoadImage(textureData);
                    textures.Add(md.diffuseTexPath, texture);
                    md.diffuseTex = texture;
                    materialCache[md.name] = CubeBuilderHelpers.GetMaterial(UseUnlitShader, md);
                    yield return null;
                }
            }
        }
    }

    public void MapTextures(Cube cube)
    {
        GameObject gameObject = cube.GameObject;
        if (gameObject != null)
        {
            var textureCoord = DetailLevel.TextureCoordinatesForCube(cube.MapPosition.x,
                cube.MapPosition.y);
            Renderer renderer = gameObject.GetComponent<Renderer>();
            renderer.materials = materialCache["material_" + (int) textureCoord.x + "_" + (int) textureCoord.y];
        }
    }
}
