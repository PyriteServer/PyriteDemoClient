namespace Pyrite
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;

    public class GeometryBuffer
    {
        public enum ModelFormat
        {
            Ebo,
            Ebo2,
            Ctm
        }

        public byte[] Buffer { get; set; }
        public ModelFormat Format { get; set; }

        public Vector3[] Vertices { get; set; }
        public Vector2[] UVs { get; set; }
        public int[] Triangles { get; set; }

        public bool Processed { get; private set; }
        public float YOffset { get; set; }
        public bool InvertedData { get; set; }

        public GeometryBuffer(float yOffset = 0, bool invertedData = false)
        {
            YOffset = yOffset;
            InvertedData = invertedData;
            Format = ModelFormat.Ebo;
        }

        public void Process()
        {
            switch(Format)
            {
                case ModelFormat.Ebo:
                    ProcessEbo();
                    break;
                case ModelFormat.Ebo2:
                    ProcessEbo2();
                    break;
                case ModelFormat.Ctm:
                    ProcessCtmRaw();
                    break;
            }
        }

        private void ReadCtmString(BinaryReader br)
        {
            int stringLength = br.ReadInt32();
            br.BaseStream.Seek(stringLength, SeekOrigin.Current);
        }

        private void ProcessCtmRaw()
        {
            if (Processed) return;

            using (var s = new MemoryStream(Buffer))
            using (var br = new BinaryReader(s))
            {
                // File is prefixed with face count, times 3 for vertices

                s.Seek(12, SeekOrigin.Begin);

                var v_uv_pairCount = br.ReadInt32();
                var triangleCount = br.ReadInt32()*3;

                s.Seek(12, SeekOrigin.Current);

                ReadCtmString(br);

                s.Seek(4, SeekOrigin.Current); // writer.Write(0x58444e49); // "INDX"      

                Triangles = new int[triangleCount];

                for (int i = 0; i < triangleCount; i++)
                {
                    Triangles[i] = br.ReadInt32();
                }

                s.Seek(4, SeekOrigin.Current);  // writer.Write(0x54524556);

                Vertices = new Vector3[v_uv_pairCount];

                for (int i = 0; i < v_uv_pairCount; i++)
                {
                    float x, y, z;
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

                    Vertices[i] = new Vector3(x, y + YOffset, z);
                }

                s.Seek(4, SeekOrigin.Current); // writer.Write(0x43584554);
                ReadCtmString(br); // string 1 "Diffuse color"
                ReadCtmString(br); // string 2 "0_0.jpg"

                UVs = new Vector2[v_uv_pairCount];
                for (int i = 0; i < v_uv_pairCount; i++)
                {
                    UVs[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                }
            }

            if (InvertedData)
            {
                for (var i = 0; i < Triangles.Length; i += 3)
                {
                    var t1 = Triangles[i + 1];
                    var t2 = Triangles[i + 2];

                    Triangles[i + 1] = t2;
                    Triangles[i + 2] = t1;
                }
            }

            Processed = true;
            Buffer = null;
        }

        private void ProcessEbo2()
        {
            if (Processed) return;

            using (var s = new MemoryStream(Buffer))
            using (var br = new BinaryReader(s))
            {
                // File is prefixed with face count, times 3 for vertices
                var vertexCount = br.ReadUInt16()*3;
                var uniqueCount = br.ReadUInt32();
                int nextNewIndex = 0;
                int bufferIndex;

                float x, y, z;

                var verticesIndex = new int[vertexCount];
                var vertices = new Vector3[uniqueCount];
                var uvs = new Vector2[uniqueCount];

                Triangles = new int[vertexCount];

                // Enumerate vertices
                for (var i = 0; i < vertexCount; i++)
                {
                    try
                    {
                        switch ((int) br.ReadByte())
                        {
                            // Reuse vert and uv
                            case (0):
                                bufferIndex = (int) br.ReadUInt32();
                                verticesIndex[i] = verticesIndex[bufferIndex];
                                Triangles[i] = verticesIndex[bufferIndex];
                                break;
                            // reuse vert, new uv
                            case (64):
                                bufferIndex = (int) br.ReadUInt32();

                                vertices[nextNewIndex] = vertices[verticesIndex[bufferIndex]];
                                
                                verticesIndex[i] = nextNewIndex;

                                uvs[nextNewIndex] = new Vector2(br.ReadSingle(), br.ReadSingle());
                                Triangles[i] = nextNewIndex;
                                nextNewIndex++;
                                break;
                            case (128):
                                throw new EndOfStreamException("Unexpectedly hit end of EBO stream");
                            // new vert, new uv
                            case (255):
                                if (InvertedData)
                                {
                                    x = br.ReadSingle()*-1;
                                    z = br.ReadSingle()*-1;
                                    y = br.ReadSingle();
                                }
                                else
                                {
                                    x = br.ReadSingle();
                                    y = br.ReadSingle();
                                    z = br.ReadSingle();
                                }

                                vertices[nextNewIndex] = new Vector3(x, y + YOffset, z);
                                
                                verticesIndex[i] = nextNewIndex;

                                uvs[nextNewIndex] = new Vector2(br.ReadSingle(), br.ReadSingle());
                                Triangles[i] = nextNewIndex;
                                nextNewIndex++;
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        Debug.Log("Failure with : " + vertexCount);
                        throw;
                    }
                }

                Vertices = vertices;
                UVs = uvs;
            }

            if (InvertedData)
            {
                for (var i = 0; i < Triangles.Length; i += 3)
                {
                    var t1 = Triangles[i + 1];
                    var t2 = Triangles[i + 2];

                    Triangles[i + 1] = t2;
                    Triangles[i + 2] = t1;
                }
            }

            Processed = true;
            Buffer = null;
        }

        private void ProcessEbo()
        {
            if (Processed) return;

            using (var s = new MemoryStream(Buffer))
            using (var br = new BinaryReader(s))
            {
                // File is prefixed with face count, times 3 for vertices
                var vertexCount = br.ReadUInt16() * 3;
                int p;
                int bufferIndex;

                float x, y, z;

                var verticesIndex = new int[vertexCount];
                var vertices = new List<Vector3>();
                var uvs = new List<Vector2>();

                Triangles = new int[vertexCount];

                // Enumerate vertices
                for (var i = 0; i < vertexCount; i++)
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
                for (var i = 0; i < Triangles.Length; i += 3)
                {
                    var t1 = Triangles[i + 1];
                    var t2 = Triangles[i + 2];

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

            var m = (gameObject.GetComponent(typeof (MeshFilter)) as MeshFilter).mesh;
            m.vertices = Vertices;
            m.uv = UVs;
            m.triangles = Triangles;

            // m.RecalculateNormals();

            if (material != null)
            {
                var renderer = gameObject.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
            }
        }
    }
}