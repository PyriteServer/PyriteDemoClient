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
            int p;
            int bufferIndex;

            float x, y, z;

            int[] verticesIndex = new int[vertexCount];
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            Triangles = new int[vertexCount];

            // Enumerate vertices
            for (int i = 0; i < vertexCount; i++)
            {
                try
                {
                    switch ((int)br.ReadByte())
                    {
                        // Reuse vert and uv
                        case (0):
                            bufferIndex = (int)br.ReadUInt32();
                            verticesIndex[i] = verticesIndex[bufferIndex];
                            Triangles[i] = verticesIndex[bufferIndex];
                            break;
                        // reuse vert, new uv
                        case (64):
                            bufferIndex = (int)br.ReadUInt32();

                            vertices.Add(vertices[verticesIndex[bufferIndex]]);
                            p = vertices.Count - 1;
                            verticesIndex[i] = p;

                            uvs.Add(new Vector2(br.ReadSingle(), br.ReadSingle()));
                            Triangles[i] = p;
                            break;
                        case (128):
                            throw new EndOfStreamException("Unexpectedly hit end of EBO stream");
                        // new vert, new uv
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

                            //vertices.Add(i, new Vector3(x, y + YOffset, z));
                            vertices.Add(new Vector3(x, y + YOffset, z));
                            p = vertices.Count - 1;
                            verticesIndex[i] = p;

                            uvs.Add(new Vector2(br.ReadSingle(), br.ReadSingle()));
                            Triangles[i] = p;
                            break;
                    }
                }
                catch (Exception)
                {
                    Debug.Log("Failure with : " + vertexCount);
                    throw;
                }
            }

            Vertices = vertices.ToArray();
            UVs = uvs.ToArray();

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
            renderer.sharedMaterial = material;
        }
    }
}