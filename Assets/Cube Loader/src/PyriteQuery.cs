namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Extensions;
    using SimpleJSON;
    using UnityEngine;

    public class PyriteQuery
    {
        private const string StatusKey = "status";
        private const string ResultKey = "result";
        private const string NameKey = "name";
        private const string CubesKey = "cubes";
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
        private readonly string apiUrl = "http://az744221.vo.msecnd.net/";


        public PyriteQuery(MonoBehaviour manager, string setName, string version, string apiUrl = null)
        {
            if (!string.IsNullOrEmpty(apiUrl))
            {
                Debug.Log("Overriding PyriteServer url to " + apiUrl);
                this.apiUrl = apiUrl;
            }
            SetName = setName;
            Version = version;
            Loaded = false;
            DetailLevels = new Dictionary<int, PyriteSetVersionDetailLevel>();
            Manager = manager;
            SetUrl = this.apiUrl + "/sets/" + SetName + "/";
            VersionUrl = SetUrl + Version + "/";
        }

        public string SetName { get; private set; }
        public string Version { get; private set; }
        public bool Loaded { get; private set; }

        private readonly MonoBehaviour Manager;

        private readonly string VersionUrl;
        private readonly string SetUrl;

        public Dictionary<int, PyriteSetVersionDetailLevel> DetailLevels { get; private set; }

        public string GetModelPath(int lod, int x, int y, int z)
        {
            return GetModelPath(GetLODKey(lod), x, y, z);
        }

        public string GetModelPath(string lod, int x, int y, int z)
        {
            return string.Format("{0}/sets/{1}/{2}/models/{3}/{4},{5},{6}", apiUrl, SetName, Version, lod, x, y, z);
        }

        public string GetTexturePath(int lod, int x, int y)
        {
            return GetTexturePath(GetLODKey(lod), x, y);
        }

        public string GetTexturePath(string lod, int x, int y)
        {
            return string.Format("{0}/sets/{1}/{2}/textures/{3}/{4},{5}", apiUrl, SetName, Version, lod, x, y);
        }

        public string GetLODKey(int lod)
        {
            return "L" + lod;
        }

        public Vector3 GetNextCubeFactor(int lod)
        {
            var currentSetSize = DetailLevels[lod].SetSize;
            if (!DetailLevels.ContainsKey(lod - 1))
            {
                return Vector3.one;
            }

            var nextSetSize = DetailLevels[lod - 1].SetSize;
            return new Vector3(
                nextSetSize.x/currentSetSize.x,
                nextSetSize.y/currentSetSize.y,
                nextSetSize.z/currentSetSize.z
                );
        }

        public Vector3 GetPreviousCubeFactor(int lod)
        {
            var currentSetSize = DetailLevels[lod].SetSize;
            if (!DetailLevels.ContainsKey(lod + 1))
            {
                return Vector3.one;
            }

            var prevSetSize = DetailLevels[lod + 1].SetSize;
            return new Vector3(
                currentSetSize.x / prevSetSize.x,
                currentSetSize.y / prevSetSize.y,
                currentSetSize.z / prevSetSize.z
                );
        }

        private int GetDetailNumberFromName(string levelName)
        {
            return int.Parse(levelName.Substring(1));
        }

        public IEnumerator Load3x3(Vector3 queryPosition)
        {
            yield return Manager.StartCoroutine(LoadMetadata());

            var cubesUrl = VersionUrl + string.Format("query/3x3/{0},{1},{2}", queryPosition.x, queryPosition.y, queryPosition.z);

            WWW loader = WWWExtensions.CreateWWW(cubesUrl);
            yield return loader;
            var parsedContent = JSON.Parse(loader.GetDecompressedText());
            if (!parsedContent[StatusKey].Value.Equals(OKValue))
            {
                Debug.LogError("Failure getting cube query against: " + cubesUrl);
                yield break;
            }

            var parsedCubeGroups = parsedContent[ResultKey].AsArray;
            for (var l = 0; l < parsedCubeGroups.Count; l++)
            {
                string lodName = parsedCubeGroups[l][NameKey];
                var detailLevelNumber  = Int32.Parse(lodName.Substring(1));
                var parsedCubes = parsedCubeGroups[l][CubesKey].AsArray;
                DetailLevels[detailLevelNumber].Cubes = new PyriteCube[parsedCubes.Count];
                for (var c = 0; c < parsedCubes.Count; c++)
                {
                    DetailLevels[detailLevelNumber].Cubes[c] = new PyriteCube()
                    {
                        X = parsedCubes[c][0].AsInt,
                        Y = parsedCubes[c][1].AsInt,
                        Z = parsedCubes[c][2].AsInt
                    };
                }
            }
        }

        public IEnumerator LoadAll()
        {
            yield return Manager.StartCoroutine(LoadMetadata());
            foreach (var detailLevel in DetailLevels.Values)
            {
                var maxBoundingBoxQuery = string.Format("{0},{1},{2}/{3},{4},{5}",
                    detailLevel.WorldBoundsMin.x,
                    detailLevel.WorldBoundsMin.y,
                    detailLevel.WorldBoundsMin.z,
                    detailLevel.WorldBoundsMax.x,
                    detailLevel.WorldBoundsMax.y,
                    detailLevel.WorldBoundsMax.z
                    );

                var cubesUrl = VersionUrl + "query/" + detailLevel.Name + "/" +
                               maxBoundingBoxQuery;

                WWW loader = WWWExtensions.CreateWWW(cubesUrl);
                yield return loader;
                var parsedContent = JSON.Parse(loader.GetDecompressedText());
                if (!parsedContent[StatusKey].Value.Equals(OKValue))
                {
                    Debug.LogError("Failure getting cube query against: " + cubesUrl);
                    yield break;
                }

                var parsedCubes = parsedContent[ResultKey].AsArray;
                detailLevel.Cubes = new PyriteCube[parsedCubes.Count];
                for (var l = 0; l < detailLevel.Cubes.Length; l++)
                {
                    detailLevel.Cubes[l] = new PyriteCube
                    {
                        X = parsedCubes[l][0].AsInt,
                        Y = parsedCubes[l][1].AsInt,
                        Z = parsedCubes[l][2].AsInt
                    };
                }
            }
            Loaded = true;
        }

        private IEnumerator LoadMetadata()
        {
            Debug.Log("Metadata query started against: " + SetUrl);
            WWW loader = null;
            loader = WWWExtensions.CreateWWW(SetUrl);
            yield return loader;
            var parsedContent = JSON.Parse(loader.GetDecompressedText());
            if (!parsedContent[StatusKey].Value.Equals(OKValue))
            {
                Debug.LogError("Failure getting set info for " + SetName);
                yield break;
            }
            loader = WWWExtensions.CreateWWW(VersionUrl);
            yield return loader;
            parsedContent = JSON.Parse(loader.GetDecompressedText());
            if (!parsedContent[StatusKey].Value.Equals(OKValue))
            {
                Debug.LogError("Failure getting set version info for " + SetName + " - " + Version);
                yield break;
            }
            var parsedDetailLevels = parsedContent[ResultKey][DetailLevelsKey].AsArray;
            for (var k = 0; k < parsedDetailLevels.Count; k++)
            {
                var detailLevel = new PyriteSetVersionDetailLevel
                {
                    Name = parsedDetailLevels[k][NameKey],
                    Query = this
                };

                detailLevel.Value = Int32.Parse(detailLevel.Name.Substring(1));

                DetailLevels[GetDetailNumberFromName(detailLevel.Name)] = detailLevel;
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
            }
            Debug.Log("Metadata query completed.");
        }
    }

    public class PyriteCube : IEquatable<PyriteCube>
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public bool Equals(PyriteCube other)
        {
            return null != other && other.X == X && other.Y == Y && other.Z == Z;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PyriteCube);
        }

        public override int GetHashCode()
        {
            var hashCode = 0;
            hashCode ^= X.GetHashCode();
            hashCode ^= Y.GetHashCode();
            hashCode ^= Z.GetHashCode();
            return hashCode;
        }
    }

    public class PyriteSetVersionDetailLevel
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public PyriteQuery Query { get; set; }
        public Vector3 SetSize { get; set; }
        public Vector2 TextureSetSize { get; set; }
        public Vector3 WorldBoundsMax { get; set; }
        public Vector3 WorldBoundsMin { get; set; }
        public Vector3 WorldBoundsSize { get; set; }
        public Vector3 WorldCubeScale { get; set; }
        public PyriteCube[] Cubes { get; set; }

        public Vector2 TextureCoordinatesForCube(float cubeX, float cubeY)
        {
            var textureXPosition = (int) (cubeX/(SetSize.x/TextureSetSize.x));
            var textureYPosition = (int) (cubeY/(SetSize.y/TextureSetSize.y));
            return new Vector2(textureXPosition, textureYPosition);
        }

        // Returns the center of the cube (point at the middle of each axis distance) in world space
        public Vector3 GetWorldCoordinatesForCube(PyriteCube cube)
        {
            var xPos = WorldBoundsMin.x + WorldCubeScale.x*cube.X +
                       WorldCubeScale.x*0.5f;
            var yPos = WorldBoundsMin.y + WorldCubeScale.y*cube.Y +
                       WorldCubeScale.y*0.5f;
            var zPos = WorldBoundsMin.z + WorldCubeScale.z*cube.Z +
                       WorldCubeScale.z*0.5f;
            return new Vector3(xPos, yPos, zPos);
        }
    }
}