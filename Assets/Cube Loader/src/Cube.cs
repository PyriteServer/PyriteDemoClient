namespace Assets.Cube_Loader.src
{
    using System.Collections.Generic;
    using UnityEngine;

    public class Cube
    {
        public Vector3 MapPosition { get; set; }
        public PyriteQuery Query { get; set; }
        public GeometryBuffer Buffer { get; set; }
        public List<MaterialData> MaterialData { get; set; }
        public GameObject GameObject { get; set; }
        public int Lod { get; set; }
    }

    public class MaterialData
    {
        private const string NameFormat = "materialData_L{0}_{1}_{2}";

        public Color Ambient;
        public Color Diffuse;
        public Color Specular;
        public float Shininess;
        public float Alpha;
        public int IllumType;
        public string DiffuseTexPath;
        public Texture2D DiffuseTex;
        public int X;
        public int Y;
        public int Lod;

        public string Name
        {
            get { return string.Format(NameFormat, Lod, X, Y); }
        }
    }
}