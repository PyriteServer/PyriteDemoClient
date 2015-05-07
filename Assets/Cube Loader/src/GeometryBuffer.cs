    using System;
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
	using System.Linq;

    public class GeometryBuffer {
        public byte[] Buffer;   
        
        public GeometryBuffer() {
           
        }

        public void PopulateMeshes(GameObject gameObject, Material material) 
        {
            int vertexCount;
            Vector3[] tvertices;
            Vector2[] tuvs;

            int[] triangles;
            using (var s = new MemoryStream(Buffer))
            using (var br = new BinaryReader(s))
            {
                // File is prefixed with face count, times 3 for vertices
                vertexCount = br.ReadUInt16() * 3;
				
				SortedList<int, Vector3> vertices = new SortedList<int, Vector3>();
				SortedList<int, Vector2> uvs = new SortedList<int, Vector2>();

                triangles = new int[vertexCount];

				// Enumerate vertices
                for (int i = 0; i < vertexCount; i++)
                {
                    try
                    {
                        switch ((int) br.ReadByte())
                        {
                            case (0):
                                int bufferIndex = (int) br.ReadUInt32();
								triangles[i] = vertices.IndexOfKey(bufferIndex);
                                break;
                            case (64):
                                bufferIndex = (int) br.ReadUInt32();
								vertices.Add(i, vertices[bufferIndex]);
								uvs.Add(i, new Vector2(br.ReadSingle(), br.ReadSingle()));
								triangles[i] = vertices.IndexOfKey(i);
                                break;
                            case (128):
                                throw new EndOfStreamException("Unexpectedly hit end of EBO stream");
                            case (255):
                                vertices.Add(i, new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                                uvs.Add(i, new Vector2(br.ReadSingle(), br.ReadSingle()));
								triangles[i] = vertices.IndexOfKey(i);
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        Debug.Log("Failure with : "+ vertexCount);
                        throw;
                    }
                }

				tvertices = vertices.Values.ToArray();
				tuvs = uvs.Values.ToArray();

            }

            Mesh m = (gameObject.GetComponent(typeof(MeshFilter)) as MeshFilter).mesh;

            // RPL HACK FIX
            for (int i = 0; i < tvertices.Length; i++)
            {
                Vector3 t = new Vector3(-tvertices[i].x, tvertices[i].z + 600, -tvertices[i].y);
                tvertices[i] = t;
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int t0 = triangles[i];
                int t1 = triangles[i + 1];
                int t2 = triangles[i + 2];

                triangles[i] = t0;
                triangles[i + 1] = t2;
                triangles[i + 2] = t1;
            }
            // END HACK 
                   
            if (tvertices.Length > 65000)
            {
                Debug.LogErrorFormat("GameObject {0} had too many vertices", gameObject.name);
            }

            m.vertices = tvertices;
            m.uv = tuvs;
            m.triangles = triangles;

            // m.RecalculateNormals();

            if (material != null)
            {
                Renderer renderer = gameObject.GetComponent<Renderer>();
                renderer.materials = new Material[] { material };
            }
        }
    }