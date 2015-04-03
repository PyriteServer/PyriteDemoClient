using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using RestSharp;
using System;

public class TextureLoader {
    public event EventHandler DownloadCompleted;

    public bool UseOldQuadShader { get; set; }

    public CubeQuery Query { get; private set; }
    public List<MaterialData> MaterialDataList { get; private set; }
    public int TextureCount { get; private set; }

    private Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    public Dictionary<string, Texture2D> Textures
    {
        get { return textures; }
    }

    private Material[] materials;
    public Material[] Materials
    {
        get { return materials; }
    }

    // holds the downloaded texture data
    private Dictionary<string, byte[]> textureDataCache = new Dictionary<string, byte[]>();

    public TextureLoader(CubeQuery query, List<MaterialData> materialDataList)
    {
        Query = query;
        MaterialDataList = materialDataList;
        TextureCount = Query.TextureSubdivide * Query.TextureSubdivide;
        materials = new Material[TextureCount];
    }

    public IEnumerator DownloadTextures()
    {
        foreach (var md in MaterialDataList)
        {
            for (int texX = 0; texX < Query.TextureSubdivide; texX++)
            {
                for (int texY = 0; texY < Query.TextureSubdivide; texY++)
                {
                    var texPath = Query.TexturePath.Replace("{x}", texX.ToString())
                        .Replace("{y}", texY.ToString());

                    yield return ThreadPool.QueueUserWorkItem(new WaitCallback(StartDownloadTexture), texPath);
                }
            }
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
                md.diffuseTexDivisions = Query.TextureSubdivide;
                md.dividedDiffuseTex = new Texture2D[Query.TextureSubdivide, Query.TextureSubdivide];

                for (int texX = 0; texX < Query.TextureSubdivide; texX++)
                {
                    for (int texY = 0; texY < Query.TextureSubdivide; texY++)
                    {
                        var texPath = Query.TexturePath.Replace("{x}", texX.ToString())
                        .Replace("{y}", texY.ToString());

                        byte[] textureData = textureDataCache[texPath];

                        Texture2D texture = new Texture2D(1, 1, TextureFormat.DXT1, false);
                        texture.LoadImage(textureData);
                        Textures.Add(texPath, texture);

                        md.dividedDiffuseTex[texX, texY] = texture;

                        
                    }
                }

                materials = GetMaterial(md);
                yield return null;
            }
        }
    }

    public IEnumerator MapTextures(Cube cube)
    {
        GameObject gameObject = cube.GameObject;
        Renderer renderer = gameObject.GetComponent<Renderer>();

        if(UseOldQuadShader)
        {
            renderer.materials = materials;
        }
        else
        {
            renderer.material = materials[0];
        }
                
        yield return null;
    }

    private Material[] GetMaterial(MaterialData md)
    {
        Material[] m;

        if (md.diffuseTexDivisions == 2)
        {
            if(UseOldQuadShader)
            {
                m = new Material[4];
                m[0] = new Material(Shader.Find(("Custom/QuadShader")));
                m[1] = new Material(Shader.Find(("Custom/QuadShader")));
                m[2] = new Material(Shader.Find(("Custom/QuadShader")));
                m[3] = new Material(Shader.Find(("Custom/QuadShader")));

                m[0].SetTexture("_MainTex", md.dividedDiffuseTex[0, 1]);
                m[0].SetVector("_UVExtents", new Vector4(0f, 0f, 0.5f, 0.5f));

                m[1].SetTexture("_MainTex", md.dividedDiffuseTex[1, 1]);
                m[1].SetVector("_UVExtents", new Vector4(0.5f, 0f, 1f, 0.5f));

                m[2].SetTexture("_MainTex", md.dividedDiffuseTex[0, 0]);
                m[2].SetVector("_UVExtents", new Vector4(0f, 0.5f, 0.5f, 1f));

                m[3].SetTexture("_MainTex", md.dividedDiffuseTex[1, 0]);
                m[3].SetVector("_UVExtents", new Vector4(0.5f, 0.5f, 1f, 1f));
            }
            else
            {
                m = new Material[1];

                m[0] = new Material(Shader.Find(("Custom/MPQuadShader")));

                m[0].SetTexture("_MainTex", md.dividedDiffuseTex[0, 1]);
                m[0].SetVector("_UVExtents", new Vector4(0f, 0f, 0.5f, 0.5f));

                m[0].SetTexture("_MainTex2", md.dividedDiffuseTex[1, 1]);
                m[0].SetVector("_UVExtents2", new Vector4(0.5f, 0f, 1f, 0.5f));

                m[0].SetTexture("_MainTex3", md.dividedDiffuseTex[0, 0]);
                m[0].SetVector("_UVExtents3", new Vector4(0f, 0.5f, 0.5f, 1f));

                m[0].SetTexture("_MainTex4", md.dividedDiffuseTex[1, 0]);
                m[0].SetVector("_UVExtents4", new Vector4(0.5f, 0.5f, 1f, 1f));
            }

        }
        else
        {

            // Use an unlit shader for the model if set
            //if (UseUnlitShader)
            //{
            //    m = new Material[] { new Material(Shader.Find(("Unlit/Texture"))) };
            //}
            //else
            //{
                if (md.illumType == 2)
                {
                    m = new Material[] { new Material(Shader.Find("Specular")) };
                    m[0].SetColor("_SpecColor", md.specular);
                    m[0].SetFloat("_Shininess", md.shininess);
                }
                else
                {
                    m = new Material[] { new Material(Shader.Find("Diffuse")) };
                }

                m[0].SetColor("_Color", md.diffuse);
            //}

            if (md.diffuseTex != null)
                m[0].SetTexture("_MainTex", md.diffuseTex);
        }
        return m;
    }
}
