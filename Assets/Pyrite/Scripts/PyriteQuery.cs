namespace Pyrite
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Extensions;
    using Microsoft.Xna.Framework;
    using Model;
    using Pyrite.Client.Model;
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
        private const string ModelBoundsKey = "modelBounds";
        private const string WorldBoundsKey = "worldBounds";
        private const string WorldCubeScaleKey = "worldCubeScale";
        private const string MaxKey = "max";
        private const string MinKey = "min";
        private const string XKey = "x";
        private const string YKey = "y";
        private const string ZKey = "z";
        private const string OkValue = "OK";
        private readonly string _apiUrl = "http://api.pyrite3d.org/";

        public PyriteQuery(MonoBehaviour manager, string setName, string version, string apiUrl)
            : this(manager, setName, version, apiUrl, 1.05f, 0f, 1.05f, 0f)
        {
        }

        public PyriteQuery(MonoBehaviour manager, string setName, string version, string apiUrl, float upgradeFactor,
            float upgradeConstant, float downgradeFactor, float downgradeConstant)
        {
            if (!string.IsNullOrEmpty(apiUrl))
            {
                Debug.Log("Overriding PyriteServer url to " + apiUrl);
                _apiUrl = apiUrl;
            }
            SetName = setName;
            Version = version;
            Loaded = false;
            _manager = manager;
            _setUrl = _apiUrl + "/sets/" + SetName + "/";
            _versionUrl = _setUrl + Version + "/";
            _modelUrlPart = _versionUrl + "models/L";
            _textureUrlPart = _versionUrl + "textures/L";

            _upgradeFactor = upgradeFactor;
            _upgradeConstant = upgradeConstant;

            _downgradeFactor = downgradeFactor;
            _downgradeConstant = downgradeConstant;
        }

        public string SetName { get; private set; }
        public string Version { get; private set; }
        public bool Loaded { get; private set; }

        private readonly MonoBehaviour _manager;

        private readonly string _versionUrl;
        private readonly string _setUrl;
        private readonly string _modelUrlPart;
        private readonly string _textureUrlPart;

        private readonly float _upgradeFactor;
        private readonly float _upgradeConstant;
        private readonly float _downgradeFactor;
        private readonly float _downgradeConstant;

        public PyriteSetVersionDetailLevel[] DetailLevels { get; private set; }

        public string GetModelPath(int lodIndex, int x, int y, int z, string modelFormat = null)
        {
            var modelPathBuilder = new StringBuilder(_modelUrlPart);
            modelPathBuilder.Append(DetailLevels[lodIndex].Value);
            modelPathBuilder.Append("/");
            modelPathBuilder.Append(x);
            modelPathBuilder.Append(',');
            modelPathBuilder.Append(y);
            modelPathBuilder.Append(',');
            modelPathBuilder.Append(z);

            if (!string.IsNullOrEmpty(modelFormat))
            {
                modelPathBuilder.Append("?fmt=");
                modelPathBuilder.Append(modelFormat);
            }
            
            return modelPathBuilder.ToString();
        }

        public string GetTexturePath(int lodIndex, int x, int y)
        {
            var texturePathBuilder = new StringBuilder(_textureUrlPart);
            texturePathBuilder.Append(DetailLevels[lodIndex].Value);
            texturePathBuilder.Append("/");
            texturePathBuilder.Append(x);
            texturePathBuilder.Append(',');
            texturePathBuilder.Append(y);
            return texturePathBuilder.ToString();
        }

        public Vector3 GetNextCubeFactor(int lodIndex)
        {
            if (lodIndex == 0)
            {
                return Vector3.one;
            }

            var currentSetSize = DetailLevels[lodIndex].SetSize;
            var nextSetSize = DetailLevels[lodIndex - 1].SetSize;

            return new Vector3(
                nextSetSize.x/currentSetSize.x,
                nextSetSize.y/currentSetSize.y,
                nextSetSize.z/currentSetSize.z
                );
        }

        public Vector3 GetPreviousCubeFactor(int lodIndex)
        {
            if (lodIndex == DetailLevels.Length - 1)
            {
                return Vector3.one;
            }

            var currentSetSize = DetailLevels[lodIndex].SetSize;
            var prevSetSize = DetailLevels[lodIndex + 1].SetSize;

            return new Vector3(
                currentSetSize.x/prevSetSize.x,
                currentSetSize.y/prevSetSize.y,
                currentSetSize.z/prevSetSize.z
                );
        }

        public IEnumerator Load3X3(string reference, Vector3 queryPosition)
        {
            yield return _manager.StartCoroutine(LoadMetadata());

            var cubesUrl = _versionUrl +
                           string.Format("query/3x3/{0}/{1},{2},{3}", reference, queryPosition.x, queryPosition.y,
                               queryPosition.z);

            var loader = WwwExtensions.CreateWWW(cubesUrl, true);
            yield return loader;
            var parsedContent = JSON.Parse(loader.GetDecompressedText());
            if (!parsedContent[StatusKey].Value.Equals(OkValue))
            {
                Debug.LogError("Failure getting cube query against: " + cubesUrl);
                yield break;
            }

            var parsedCubeGroups = parsedContent[ResultKey].AsArray;
            for (var l = 0; l < parsedCubeGroups.Count; l++)
            {
                string lodName = parsedCubeGroups[l][NameKey];
                var detailLevelindex = Int32.Parse(lodName.Substring(1)) - 1;
                var parsedCubes = parsedCubeGroups[l][CubesKey].AsArray;
                DetailLevels[detailLevelindex].Cubes = new PyriteCube[parsedCubes.Count];
                for (var c = 0; c < parsedCubes.Count; c++)
                {
                    DetailLevels[detailLevelindex].Cubes[c] = new PyriteCube
                    {
                        X = parsedCubes[c][0].AsInt,
                        Y = parsedCubes[c][1].AsInt,
                        Z = parsedCubes[c][2].AsInt
                    };
                }
            }
        }

        public IEnumerator LoadAll(List<int> detailLevelsToFilter)
        {
            yield return _manager.StartCoroutine(LoadMetadata(detailLevelsToFilter));
            foreach (var detailLevel in DetailLevels)
            {
                var maxBoundingBoxQuery = string.Format("{0},{1},{2}/{3},{4},{5}",
                    detailLevel.WorldBoundsMin.x,
                    detailLevel.WorldBoundsMin.y,
                    detailLevel.WorldBoundsMin.z,
                    detailLevel.WorldBoundsMax.x,
                    detailLevel.WorldBoundsMax.y,
                    detailLevel.WorldBoundsMax.z
                    );

                var cubesUrl = _versionUrl + "query/" + detailLevel.Name + "/" +
                               maxBoundingBoxQuery;

                var loader = WwwExtensions.CreateWWW(cubesUrl, true);
                yield return loader;
                var parsedContent = JSON.Parse(loader.GetDecompressedText());
                if (!parsedContent[StatusKey].Value.Equals(OkValue))
                {
                    Debug.LogError("Failure getting cube query against: " + cubesUrl);
                    yield break;
                }

                var parsedCubes = parsedContent[ResultKey].AsArray;
                detailLevel.Cubes = new PyriteCube[parsedCubes.Count];
                detailLevel.Octree = new OcTree<CubeBounds>();
                for (var l = 0; l < detailLevel.Cubes.Length; l++)
                {
                    detailLevel.Cubes[l] = new PyriteCube
                    {
                        X = parsedCubes[l][0].AsInt,
                        Y = parsedCubes[l][1].AsInt,
                        Z = parsedCubes[l][2].AsInt
                    };
                    var min = new Vector3(detailLevel.Cubes[l].X, detailLevel.Cubes[l].Y, detailLevel.Cubes[l].Z);
                    var max = min + Vector3.one;
                    detailLevel.Octree.Add(new CubeBounds {BoundingBox = new BoundingBox(min, max)});
                }

                detailLevel.Octree.UpdateTree();
            }
            Loaded = true;
        }

        private IEnumerator LoadMetadata(List<int> detailLevelsToFilter = null)
        {
            Debug.Log("Metadata query started against: " + _setUrl);
            WWW loader = null;
            loader = WwwExtensions.CreateWWW(_setUrl);
            yield return loader;
            var parsedContent = JSON.Parse(loader.GetDecompressedText());
            if (!parsedContent[StatusKey].Value.Equals(OkValue))
            {
                Debug.LogError("Failure getting set info for " + SetName);
                yield break;
            }
            loader = WwwExtensions.CreateWWW(_versionUrl);
            yield return loader;
            parsedContent = JSON.Parse(loader.GetDecompressedText());
            if (!parsedContent[StatusKey].Value.Equals(OkValue))
            {
                Debug.LogError("Failure getting set version info for " + SetName + " - " + Version);
                yield break;
            }
            var parsedDetailLevels = parsedContent[ResultKey][DetailLevelsKey].AsArray;
            var sortedDetailLevels = new SortedDictionary<int, PyriteSetVersionDetailLevel>();

            for (var k = 0; k < parsedDetailLevels.Count; k++)
            {
                var detailLevel = new PyriteSetVersionDetailLevel
                {
                    Name = parsedDetailLevels[k][NameKey],
                    Query = this
                };

                detailLevel.Value = Int32.Parse(detailLevel.Name.Substring(1));

                if (detailLevelsToFilter != null && detailLevelsToFilter.Contains(detailLevel.Value))
                {
                    Debug.Log("Skipping lod " + detailLevel.Value);
                    continue;
                }

                sortedDetailLevels[detailLevel.Value] = detailLevel;
                detailLevel.SetSize = new Vector3(
                    parsedDetailLevels[k][SetSizeKey][XKey].AsFloat,
                    parsedDetailLevels[k][SetSizeKey][YKey].AsFloat,
                    parsedDetailLevels[k][SetSizeKey][ZKey].AsFloat
                    );

                detailLevel.TextureSetSize = new Vector2(
                    parsedDetailLevels[k][TextureSetSizeKey][XKey].AsFloat,
                    parsedDetailLevels[k][TextureSetSizeKey][YKey].AsFloat
                    );

                detailLevel.ModelBoundsMax = new Vector3(
                    parsedDetailLevels[k][ModelBoundsKey][MaxKey][XKey].AsFloat,
                    parsedDetailLevels[k][ModelBoundsKey][MaxKey][YKey].AsFloat,
                    parsedDetailLevels[k][ModelBoundsKey][MaxKey][ZKey].AsFloat
                    );

                detailLevel.ModelBoundsMin = new Vector3(
                    parsedDetailLevels[k][ModelBoundsKey][MinKey][XKey].AsFloat,
                    parsedDetailLevels[k][ModelBoundsKey][MinKey][YKey].AsFloat,
                    parsedDetailLevels[k][ModelBoundsKey][MinKey][ZKey].AsFloat
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

                detailLevel.UpgradeDistance = detailLevel.WorldCubeScale.magnitude*_upgradeFactor + _upgradeConstant;

                detailLevel.DowngradeDistance = detailLevel.WorldCubeScale.magnitude*_downgradeFactor +
                                                _downgradeConstant;

                detailLevel.WorldBoundsSize =
                    detailLevel.WorldBoundsMax -
                    detailLevel.WorldBoundsMin;
            }

            DetailLevels = sortedDetailLevels.Values.ToArray();

            for (int i = DetailLevels.Length - 1; i > 0; i--)
            {
                if (DetailLevels[i].Value != DetailLevels[i - 1].Value + 1)
                {
                    DetailLevels[i].UpgradeDistance *= 0.5f;
                    DetailLevels[i].DowngradeDistance *= 0.5f;
                }
            }
            Debug.Log("Metadata query completed.");
        }
    }

    public struct PyriteCube : IEquatable<PyriteCube>
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public bool Equals(PyriteCube other)
        {
            return other.X == X && other.Y == Y && other.Z == Z;
        }

        public override int GetHashCode()
        {
            var hashCode = 0;
            hashCode ^= X.GetHashCode();
            hashCode ^= Y.GetHashCode();
            hashCode ^= Z.GetHashCode();
            return hashCode;
        }

        public string GetKey()
        {
            return (string.Format("{0},{1},{2}", X, Y, Z));
        }
    }

    public class PyriteSetVersionDetailLevel
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public PyriteQuery Query { get; set; }
        public Vector3 SetSize { get; set; }
        public Vector2 TextureSetSize { get; set; }
        public Vector3 ModelBoundsMin { get; set; }
        public Vector3 ModelBoundsMax { get; set; }
        public Vector3 WorldBoundsMax { get; set; }
        public Vector3 WorldBoundsMin { get; set; }
        public Vector3 WorldBoundsSize { get; set; }
        public Vector3 WorldCubeScale { get; set; }

        public float UpgradeDistance { get; set; }
        public float DowngradeDistance { get; set; }

        public PyriteCube[] Cubes { get; set; }
        public OcTree<CubeBounds> Octree { get; set; }

        public Vector2 TextureCoordinatesForCube(float cubeX, float cubeY)
        {
            var textureXPosition = (int) (cubeX/(SetSize.x/TextureSetSize.x));
            var textureYPosition = (int) (cubeY/(SetSize.y/TextureSetSize.y));
            return new Vector2(textureXPosition, textureYPosition);
        }

        // Returns the center of the cube (point at the middle of each axis distance) in world space
        public Vector3 GetWorldCoordinatesForCube(PyriteCube cube)
        {
            var xPos = WorldBoundsMin.x + WorldCubeScale.x*cube.X + WorldCubeScale.x*0.5f;
            var yPos = WorldBoundsMin.y + WorldCubeScale.y*cube.Y + WorldCubeScale.y*0.5f;
            var zPos = WorldBoundsMin.z + WorldCubeScale.z * cube.Z + WorldCubeScale.z * 0.5f;            
            return new Vector3(xPos, yPos, zPos);
        }

        public PyriteCube GetCubeForWorldCoordinates(Vector3 pos)
        {
            var cx = (int)((pos.x - WorldBoundsMin.x) / WorldCubeScale.x);
            var cy = (int)((pos.y - WorldBoundsMin.y) / WorldCubeScale.y);
            var cz = (int)((pos.z - WorldBoundsMin.z) / WorldCubeScale.z);
            return new PyriteCube() { X = cx, Y = cy, Z = cz };
        }

        public Vector3 GetUnityWorldCoordinatesForCube(PyriteCube cube)
        {
            var xPos = WorldBoundsMin.x + WorldCubeScale.x * cube.X + WorldCubeScale.x * 0.5f;            
            var yPos = WorldBoundsMin.z + WorldCubeScale.z * cube.Z + WorldCubeScale.z * 0.5f;
            var zPos = WorldBoundsMin.y + WorldCubeScale.y * cube.Y + WorldCubeScale.y * 0.5f;
            return new Vector3(xPos, yPos, zPos);
        }

        public PyriteCube GetCubeForUnityWorldCoordinates(Vector3 pos)
        {
            var cx = (int)((pos.x - WorldBoundsMin.x) / WorldCubeScale.x);            
            var cy = (int)((pos.z - WorldBoundsMin.y) / WorldCubeScale.z);
            var cz = (int)((pos.y - WorldBoundsMin.z) / WorldCubeScale.y);
            return new PyriteCube() { X = cx, Y = cy, Z = cz };
        }
    }
}