namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Extensions;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    public class QueryLoader : PyriteLoader
    {
        // Position to use for query if object is not specified
        public Vector3 QueryPosition;
        // Object whose position will be used for query
        public GameObject TargetGameObject; 
        // Indicates if cubes from lower details should be skipped when a higher one is present within the cube space
        public bool SkipLowerDetailedCubes = true;

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


            DebugLog("+Start()");
            StartCoroutine(Load());
            DebugLog("-Start()");
        }

        public IEnumerator Load()
        {
            DebugLog("+Load()");

            var pyriteQuery = new PyriteQuery(this, SetName, ModelVersion, PyriteServer);
            if (TargetGameObject != null)
            {
                var transformedPosition = new Vector3(
                    -TargetGameObject.transform.position.x,
                    -TargetGameObject.transform.position.z,
                    TargetGameObject.transform.position.y - 600 
                    );

                yield return StartCoroutine(pyriteQuery.Load3X3(transformedPosition));
            }
            else
            {
                yield return StartCoroutine(pyriteQuery.Load3X3(QueryPosition));
            }
            DebugLog("CubeQuery complete.");

            
            float xmin = pyriteQuery.DetailLevels.Values.First().WorldBoundsMax.x;
            float ymin = pyriteQuery.DetailLevels.Values.First().WorldBoundsMax.y;
            float zmin = pyriteQuery.DetailLevels.Values.First().WorldBoundsMax.z;
            float xmax = pyriteQuery.DetailLevels.Values.First().WorldBoundsMin.x;
            float ymax = pyriteQuery.DetailLevels.Values.First().WorldBoundsMin.y;
            float zmax = pyriteQuery.DetailLevels.Values.First().WorldBoundsMin.z;

            Dictionary<int, HashSet<PyriteCube>> cubesToSkip = new Dictionary<int, HashSet<PyriteCube>>();
            foreach (var pyriteLevel in pyriteQuery.DetailLevels.Values)
            {
                xmin = pyriteLevel.WorldBoundsMax.x;
                ymin = pyriteLevel.WorldBoundsMax.y;
                zmin = pyriteLevel.WorldBoundsMax.z;
                xmax = pyriteLevel.WorldBoundsMin.x;
                ymax = pyriteLevel.WorldBoundsMin.y;
                zmax = pyriteLevel.WorldBoundsMin.z;
                cubesToSkip[pyriteLevel.Value+1] = new HashSet<PyriteCube>();
                if (!cubesToSkip.ContainsKey(pyriteLevel.Value))
                {
                    cubesToSkip[pyriteLevel.Value] = new HashSet<PyriteCube>();
                }

                for (var i = 0; i < pyriteLevel.Cubes.Length; i++)
                {
                    var x = pyriteLevel.Cubes[i].X;
                    var y = pyriteLevel.Cubes[i].Y;
                    var z = pyriteLevel.Cubes[i].Z;
                    var cubeFactor = pyriteQuery.GetPreviousCubeFactor(pyriteLevel.Value);
                    cubesToSkip[pyriteLevel.Value + 1].Add(new PyriteCube() { X = x / (int)cubeFactor.x, Y = y / (int)cubeFactor.y, Z = z / (int)cubeFactor.z });
                    if (cubesToSkip[pyriteLevel.Value].Contains(new PyriteCube() {X = x, Y = y, Z = z}))
                    {
                        if (SkipLowerDetailedCubes)
                        {
                            DebugLog("Skipping cube: L{0}: {1},{2},{3}", pyriteLevel.Value, x, y, z);
                            continue;
                        }
                    }
                    var cubePos = pyriteLevel.GetWorldCoordinatesForCube(pyriteLevel.Cubes[i]);
                    xmin = Math.Min(cubePos.x, xmin);
                    ymin = Math.Min(cubePos.y, ymin);
                    zmin = Math.Min(cubePos.z, zmin);

                    xmax = Math.Max(cubePos.x, xmax);
                    ymax = Math.Max(cubePos.y, ymax);
                    zmax = Math.Max(cubePos.z, zmax);
                    StartCoroutine(LoadCube(pyriteQuery, x, y, z, pyriteLevel.Value));
                }
            }

            if (CameraRig != null)
            {
                DebugLog("Moving camera");
                // Hardcoding some values for now
                var min = new Vector3(xmin, ymin, zmin);
                var max = new Vector3(xmax, ymax, zmax);
                var newCameraPosition = min + (max - min) / 2.0f;
                newCameraPosition += new Vector3(0, 0, (max - min).z * 1.4f);
                CameraRig.transform.position = newCameraPosition;

                CameraRig.transform.rotation = Quaternion.Euler(0, 180, 0);

                DebugLog("Done moving camera");
            }
            DebugLog("-Load()");
        }
    }
}