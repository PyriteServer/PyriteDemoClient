    using System;
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
	using System.Linq;

    public class GeometryBuffer {

        private List<ObjectData> _objects;
        public List<Vector3> Vertices;
        public List<Vector2> Uvs;
        public List<Vector3> Normals;
        public byte[] EboBuffer;
        
        private ObjectData _current;
        private class ObjectData {
            public string Name;
            public List<GroupData> Groups;
            public List<FaceIndices> AllFaces;
            public ObjectData() {
                Groups = new List<GroupData>();
                AllFaces = new List<FaceIndices>();
            }
        }
        
        private GroupData _curgr;
        private class GroupData {
            public string Name;
            public string MaterialName;
            public List<FaceIndices> Faces;
            public GroupData() {
                Faces = new List<FaceIndices>();
            }
            public bool IsEmpty { get { return Faces.Count == 0; } }
        }
        
        public GeometryBuffer() {
            _objects = new List<ObjectData>();
            ObjectData d = new ObjectData();
            d.Name = "default";
            _objects.Add(d);
            _current = d;
            
            GroupData g = new GroupData();
            g.Name = "default";
            d.Groups.Add(g);
            _curgr = g;
            
            Vertices = new List<Vector3>();
            Uvs = new List<Vector2>();
            Normals = new List<Vector3>();
        }
        
        public void PushObject(string name) {
            //Debug.Log("Adding new object " + name + ". Current is empty: " + isEmpty);
            if(IsEmpty) _objects.Remove(_current);
            
            ObjectData n = new ObjectData();
            n.Name = name;
            _objects.Add(n);
            
            GroupData g = new GroupData();
            g.Name = "default";
            n.Groups.Add(g);
            
            _curgr = g;
            _current = n;
        }
        
        public void PushGroup(string name) {
            if(_curgr.IsEmpty) _current.Groups.Remove(_curgr);
            GroupData g = new GroupData();
            g.Name = name;
            _current.Groups.Add(g);
            _curgr = g;
        }
        
        public void PushMaterialName(string name) {
            //Debug.Log("Pushing new material " + name + " with curgr.empty=" + curgr.isEmpty);
            if(!_curgr.IsEmpty) PushGroup(name);
            if(_curgr.Name == "default") _curgr.Name = name;
            _curgr.MaterialName = name;
        }
        
        public void PushVertex(Vector3 v) {
            Vertices.Add(v);
        }
        
        public void PushUv(Vector2 v) {
            Uvs.Add(v);
        }
        
        public void PushNormal(Vector3 v) {
            Normals.Add(v);
        }
        
        public void PushFace(FaceIndices f) {
            _curgr.Faces.Add(f);
            _current.AllFaces.Add(f);
        }
        
        public void Trace() {
            Debug.Log("OBJ has " + _objects.Count + " object(s)");
            Debug.Log("OBJ has " + Vertices.Count + " vertice(s)");
            Debug.Log("OBJ has " + Uvs.Count + " uv(s)");
            Debug.Log("OBJ has " + Normals.Count + " normal(s)");
            foreach(ObjectData od in _objects) {
                Debug.Log(od.Name + " has " + od.Groups.Count + " group(s)");
                foreach(GroupData gd in od.Groups) {
                    Debug.Log(od.Name + "/" + gd.Name + " has " + gd.Faces.Count + " faces(s)");
                }
            }
            
        }
        
        public int NumObjects { get { return _objects.Count; } }	
        public bool IsEmpty { get { return Vertices.Count == 0; } }
        public bool HasUVs { get { return Uvs.Count > 0; } }
        public bool HasNormals { get { return Normals.Count > 0; } }

        public void PopulateMeshes(GameObject[] gs, Dictionary<string, Material[]> mats = null) 
        {
            if (EboBuffer == null)
            {
                PopulateMeshesObj(gs, mats);
            }
            else
            {
                PopulateMeshesEbo(gs, mats);
            }
        }

        public void PopulateMeshesEbo(GameObject[] gs, Dictionary<string, Material[]> mats)
        {
            int vertexCount;
            Vector3[] tvertices;
            Vector2[] tuvs;

            int[] triangles;
            using (var s = new MemoryStream(EboBuffer))
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

            Mesh m = (gs[0].GetComponent(typeof(MeshFilter)) as MeshFilter).mesh;

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
                Debug.LogErrorFormat("GameObject {0} had too many vertices", gs[0].name);
            }
            m.vertices = tvertices;
            m.uv = tuvs;
            m.triangles = triangles;
            // m.RecalculateNormals();
            if (mats != null)
            {
                GroupData gd = _objects[0].Groups[0];

                if (gd.MaterialName == null)
                {
                    Dictionary<string, Material[]>.KeyCollection.Enumerator keys = mats.Keys.GetEnumerator();
                    keys.MoveNext();
                    gd.MaterialName = keys.Current;
                }

                Renderer renderer = gs[0].GetComponent<Renderer>();
                renderer.materials = mats[gd.MaterialName];
            }
        }

        public void PopulateMeshesObj(GameObject[] gs, Dictionary<string, Material[]> mats) {
            if(gs.Length != NumObjects) return; // Should not happen unless obj file is corrupt...
            
            for(int i = 0; i < gs.Length; i++) {
                ObjectData od = _objects[i];
                
                if(od.Name != "default") gs[i].name = od.Name;
                
                Vector3[] tvertices = new Vector3[od.AllFaces.Count];
                Vector2[] tuvs = new Vector2[od.AllFaces.Count];
                Vector3[] tnormals = new Vector3[od.AllFaces.Count];
                int[] triangles = new int[od.AllFaces.Count];
            
                int k = 0;
                foreach(FaceIndices fi in od.AllFaces)
                {
                    triangles[k] = k;
                    tvertices[k] = Vertices[fi.Vi];
                    if(HasUVs)
                    {
                        try
                        {
                            tuvs[k] = Uvs[fi.Vu];
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("Exception: " + ex.ToString());
                            Debug.LogError("vu vs uvs.Count: " + fi.Vu + " " + Uvs.Count);
                            throw;

                        }
                    }
                    if(HasNormals)
                    {
                        tnormals[k] = Normals[fi.Vn];
                    }
                    k++;
                }
            
                Mesh m = (gs[i].GetComponent(typeof(MeshFilter)) as MeshFilter).mesh;

                // RPL HACK FIX
                for (int h = 0; h < tvertices.Length; h++)
                {
                    Vector3 t = new Vector3(-tvertices[h].x, tvertices[h].z + 600, -tvertices[h].y);
                    tvertices[h] = t;
                }
                for (int h = 0; h < triangles.Length; h += 3)
                {
                    int t0 = triangles[h];
                    int t1 = triangles[h + 1];
                    int t2 = triangles[h + 2];

                    triangles[h] = t0;
                    triangles[h + 1] = t2;
                    triangles[h + 2] = t1;
                }
                // END HACK 

                m.vertices = tvertices;
                m.triangles = triangles;
                if(HasUVs) m.uv = tuvs;
                if(HasNormals) m.normals = tnormals;
                if (mats != null)
                {
                    if (od.Groups.Count == 1)
                    {
                        GroupData gd = od.Groups[0];

                        if (gd.MaterialName == null)
                        {
                            Dictionary<string, Material[]>.KeyCollection.Enumerator keys = mats.Keys.GetEnumerator();
                            keys.MoveNext();
                            gd.MaterialName = keys.Current;
                        }

                        Renderer renderer = gs[i].GetComponent<Renderer>();
                        renderer.materials = mats[gd.MaterialName];

                    }
                    else
                    {

                        throw new UnityException("Material handling for objects with groups not yet implemented.");
                        /*
                     int gl = od.groups.Count;
                    Material[] sml = new Material[gl];
                    m.subMeshCount = gl;
                    int c = 0;
                    
                    for(int j = 0; j < gl; j++) {
                        sml[j] = mats[od.groups[j].materialName]; 
                        int[] triangles = new int[od.groups[j].faces.Count];
                        int l = od.groups[j].faces.Count + c;
                        int s = 0;
                        for(; c < l; c++, s++) triangles[s] = c;
                        m.SetTriangles(triangles, j);
                    }
                    
                    gs[i].GetComponent<Renderer>().materials = sml;
                    */
                    }
                }
                // m.RecalculateNormals();
            }
        }
    }