namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Assets.Cube_Loader.src;
    using UnityEngine;

    public class Cube
    {
        public Vector3 MapPosition { get; set; }
        public PyriteQuery Query { get; set; }
        public GeometryBuffer Buffer { get; set; }
        public List<MaterialData> MaterialData { get; set; }
        public GameObject GameObject { get; set; }
        public int LOD { get; set; }
    }

    public class MaterialData
    {
        private const string nameFormat = "materialData_L{0}_{1}_{2}";

        public Color ambient;
        public Color diffuse;
        public Color specular;
        public float shininess;
        public float alpha;
        public int illumType;
        public string diffuseTexPath;
        public Texture2D diffuseTex;
        public int x;
        public int y;
        public int lod;

        public string Name
        {
            get { return string.Format(nameFormat, lod, x, y); }
        }
    }
}
