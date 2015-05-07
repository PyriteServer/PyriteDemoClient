using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class GeometryBuffer {
    public byte[] Buffer;

    public Vector3[] Vertices { get; set; }
    public Vector2[] UVs { get; set; }
    public int[] Triangles { get; set; }

    public bool Processed { get; private set; }
    public float YOffset { get; set; }
    public bool InvertedData { get; set; }

    public GeometryBuffer(float yOffset = 0, bool invertedData = false) {
        YOffset = yOffset;
        InvertedData = invertedData;
    }

    public void Process()
    {
        if (Processed) return;

        using (var s = new MemoryStream(Buffer))
        using (var br = new BinaryReader(s))
        {
            // File is prefixed with face count, times 3 for vertices
            int vertexCount = br.ReadUInt16() * 3;

            float x, y, z;

            SortedList<int, Vector3> vertices = new SortedList<int, Vector3>();
            SortedList<int, Vector2> uvs = new SortedList<int, Vector2>();

            Triangles = new int[vertexCount];

            // Enumerate vertices
            for (int i = 0; i < vertexCount; i++)
            {
                try
                {
                    switch ((int)br.ReadByte())
                    {
                        case (0):
                            int bufferIndex = (int)br.ReadUInt32();
                            Triangles[i] = vertices.IndexOfKey(bufferIndex);
                            break;
                        case (64):
                            bufferIndex = (int)br.ReadUInt32();
                            vertices.Add(i, vertices[bufferIndex]);
                            uvs.Add(i, new Vector2(br.ReadSingle(), br.ReadSingle()));
                            Triangles[i] = vertices.IndexOfKey(i);
                            break;
                        case (128):
                            throw new EndOfStreamException("Unexpectedly hit end of EBO stream");
                        case (255):
                            if (InvertedData)
                            {
                                x = br.ReadSingle() * -1;
                                z = br.ReadSingle() * -1;
                                y = br.ReadSingle();
                            }
                            else
                            {
                                x = br.ReadSingle();
                                y = br.ReadSingle();
                                z = br.ReadSingle();
                            }

                            vertices.Add(i, new Vector3(x, y + YOffset, z));

                            uvs.Add(i, new Vector2(br.ReadSingle(), br.ReadSingle()));
                            Triangles[i] = vertices.IndexOfKey(i);
                            break;
                    }
                }
                catch (Exception)
                {
                    Debug.Log("Failure with : " + vertexCount);
                    throw;
                }
            }

            Vertices = vertices.Values.ToArray();
            UVs = uvs.Values.ToArray();

        }

        if (InvertedData)
        {
            for (int i = 0; i < Triangles.Length; i += 3)
            {
                int t1 = Triangles[i + 1];
                int t2 = Triangles[i + 2];

                Triangles[i + 1] = t2;
                Triangles[i + 2] = t1;
            }
        }

        Processed = true;
        Buffer = null;
    }

    public void PopulateMeshes(GameObject gameObject, Material material) 
    {
        if (!Processed) Process();

        if (Vertices.Length > 65000)
        {
            Debug.LogErrorFormat("GameObject {0} had too many vertices", gameObject.name);
        }

        Mesh m = (gameObject.GetComponent(typeof(MeshFilter)) as MeshFilter).mesh;

        m.vertices = Vertices;
        m.uv = UVs;
        m.triangles = Triangles;

        // m.RecalculateNormals();

        if (material != null)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            renderer.materials = new Material[] { material };
        }
    }
}