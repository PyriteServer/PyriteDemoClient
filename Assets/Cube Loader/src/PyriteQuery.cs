using System.Runtime.InteropServices;

namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Assets.Cube_Loader.Extensions;
    using SimpleJSON;
    using UnityEngine;


    public class PyriteQuery
    {
        private readonly string apiUrl = "http://az744221.vo.msecnd.net/";

        public Dictionary<string, PyriteSet> Sets { get; private set; }

        private const string StatusKey = "status";
        private const string ResultKey = "result";
        private const string NameKey = "name";
        private const string DetailLevelsKey = "detailLevels";
        private const string SetSizeKey = "setSize";
        private const string TextureSetSizeKey = "textureSetSize";
        private const string WorldBoundsKey = "worldBounds";
        private const string WorldCubeScaleKey = "worldCubeScale";
        private const string MaxKey = "max";
        private const string MinKey = "min";
        private const string XKey = "x";
        private const string YKey = "y";
        private const string ZKey = "z";
        private const string OKValue = "OK";

        public PyriteQuery(string apiUrl = null)
        {
            if (!string.IsNullOrEmpty(apiUrl))
            {
                Debug.Log("Overriding PyriteServer url to " + apiUrl);
                this.apiUrl = apiUrl;
            }
            Sets = new Dictionary<string, PyriteSet>();
        }

        public string GetModelPath(string set, string version, string lod, int x, int y, int z)
        {
            return string.Format("{0}/sets/{1}/{2}/models/{3}/{4},{5},{6}", apiUrl, set, version, lod, x, y, z);
        }

        public string GetTexturePath(string set, string version, string lod, int x, int y)
        {
            return string.Format("{0}/sets/{1}/{2}/textures/{3}/{4},{5}", apiUrl, set, version, lod, x, y);
        }

        public IEnumerator Load()
        {
            Debug.Log("CubeQuery started against: " + apiUrl);
            var setsUrl = apiUrl + "/sets/";
            WWW newLoader = WWWExtensions.CreateWWW(path: setsUrl);
            yield return newLoader;

            Debug.Log(newLoader.GetDecompressedText());
            var parsedContent = JSON.Parse(newLoader.GetDecompressedText());
            
            if (!parsedContent[StatusKey].Value.Equals(OKValue))
            {
                Debug.LogError("Failure getting set list");
                yield break;
            }
            var parsedSets = parsedContent[ResultKey].AsArray;
            
            for (int i = 0; i < parsedSets.Count; i++)
            {
                var set = new PyriteSet() { Name = parsedSets[i][NameKey] };
                Sets[set.Name] = set;
                var setUrl = setsUrl + set.Name + "/";
                newLoader = WWWExtensions.CreateWWW(path: setUrl);
                yield return newLoader;
                Debug.Log(newLoader.GetDecompressedText());
                parsedContent = JSON.Parse(newLoader.GetDecompressedText());
                if (!parsedContent[StatusKey].Value.Equals(OKValue))
                {
                    Debug.LogError("Failure getting set info for " + set.Name);
                    yield break;
                }
                var parsedVersions = parsedContent[ResultKey].AsArray;
                for (int j = 0; j < parsedVersions.Count; j++)
                {
                    string versionName = parsedVersions[j][NameKey];
                    var version = new PyriteSetVersion() { Name = versionName, Set = set};
                    set.Versions[versionName] = version;
                    string versionUrl = setUrl + versionName + "/";
                    newLoader = WWWExtensions.CreateWWW(versionUrl);
                    yield return newLoader;
                    parsedContent = JSON.Parse(newLoader.GetDecompressedText());
                    if (!parsedContent[StatusKey].Value.Equals(OKValue))
                    {
                        Debug.LogError("Failure getting set version info for " + set.Name + " - " + versionName);
                        yield break;
                    }
                    var parsedDetailLevels = parsedContent[ResultKey][DetailLevelsKey].AsArray;
                    for (int k = 0; k < parsedDetailLevels.Count; k++)
                    {
                        var detailLevel = new PyriteSetVersionDetailLevel()
                        {
                            Name = parsedDetailLevels[k][NameKey],
                            Version = version
                        };
                        version.DetailLevels[detailLevel.Name] = detailLevel;
                        detailLevel.SetSize = new Vector3(
                            parsedDetailLevels[k][SetSizeKey][XKey].AsFloat,
                            parsedDetailLevels[k][SetSizeKey][YKey].AsFloat,
                            parsedDetailLevels[k][SetSizeKey][ZKey].AsFloat
                            );

                        detailLevel.TextureSetSize = new Vector2(
                            parsedDetailLevels[k][TextureSetSizeKey][XKey].AsFloat,
                            parsedDetailLevels[k][TextureSetSizeKey][YKey].AsFloat
                            );

                        detailLevel.WorldBoundsMax = new Vector3(
                            parsedDetailLevels[k][WorldBoundsKey][MaxKey][XKey].AsFloat,
                            parsedDetailLevels[k][WorldBoundsKey][MaxKey][YKey].AsFloat,
                            parsedDetailLevels[k][WorldBoundsKey][MaxKey][ZKey].AsFloat
                            );

                        detailLevel.WorldBoundsMin = new Vector3(
                            parsedDetailLevels[k][WorldBoundsKey][MinKey][XKey].AsFloat,
                            parsedDetailLevels[k][WorldBoundsKey][MinKey][YKey].AsFloat,
                            parsedDetailLevels[k][WorldBoundsKey][MinKey][ZKey].AsFloat
                            );

                        detailLevel.WorldCubeScale = new Vector3(
                            parsedDetailLevels[k][WorldCubeScaleKey][XKey].AsFloat,
                            parsedDetailLevels[k][WorldCubeScaleKey][YKey].AsFloat,
                            parsedDetailLevels[k][WorldCubeScaleKey][ZKey].AsFloat);

                        detailLevel.WorldBoundsSize =
                            detailLevel.WorldBoundsMax -
                            detailLevel.WorldBoundsMin;

                        var maxBoundingBoxQuery = string.Format("{0},{1},{2}/{3},{4},{5}",
                            detailLevel.WorldBoundsMin.x,
                            detailLevel.WorldBoundsMin.y,
                            detailLevel.WorldBoundsMin.z,
                            detailLevel.WorldBoundsMax.x,
                            detailLevel.WorldBoundsMax.y,
                            detailLevel.WorldBoundsMax.z
                            );

                        var cubesUrl = versionUrl + "query/" + detailLevel.Name + "/" +
                                       maxBoundingBoxQuery;

                        newLoader = WWWExtensions.CreateWWW(cubesUrl);
                        yield return newLoader;
                        parsedContent = JSON.Parse(newLoader.GetDecompressedText());
                        if (!parsedContent[StatusKey].Value.Equals(OKValue))
                        {
                            Debug.LogError("Failure getting cube query against: " + cubesUrl);
                            yield break;
                        }

                        var parsedCubes = parsedContent[ResultKey].AsArray;
                        detailLevel.Cubes = new PyriteCube[parsedCubes.Count];
                        for (int l = 0; l < detailLevel.Cubes.Length; l++)
                        {
                            detailLevel.Cubes[l] = new PyriteCube()
                            {
                                X = parsedCubes[l][0].AsInt,
                                Y = parsedCubes[l][1].AsInt,
                                Z = parsedCubes[l][2].AsInt,
                            };
                        }
                    }
                }
            }

            yield return null;
        }
    }

    public class PyriteCube
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    public class PyriteSetVersion
    {
        public string Name { get; set; }
        public PyriteSet Set { get; set; }

        public Dictionary<string, PyriteSetVersionDetailLevel> DetailLevels { get; private set; }

        public PyriteSetVersion()
        {
            DetailLevels = new Dictionary<string, PyriteSetVersionDetailLevel>();
        }
    }

    public class PyriteSetVersionDetailLevel
    {
        public string Name { get; set; }
        public PyriteSetVersion Version { get; set; }
        public Vector3 SetSize { get; set; }
        public Vector2 TextureSetSize { get; set; }
        public Vector3 WorldBoundsMax { get; set; }
        public Vector3 WorldBoundsMin { get; set; }
        public Vector3 WorldBoundsSize { get; set; }
        public Vector3 WorldCubeScale { get; set; }
        public PyriteCube[] Cubes { get; set; }

        public Vector2 TextureCoordinatesForCube(float cubeX, float cubeY)
        {
            int textureXPosition = (int)(cubeX / (SetSize.x / TextureSetSize.x));
            int textureYPosition = (int)(cubeY / (SetSize.y / TextureSetSize.y));
            return new Vector2(textureXPosition, textureYPosition);
        }
    }

    public class PyriteSet
    {
        public string Name { get; set; }
        public Dictionary<string, PyriteSetVersion> Versions { get; private set; }

        public PyriteSet()
        {
            Versions = new Dictionary<string, PyriteSetVersion>();
        }
    }
}
