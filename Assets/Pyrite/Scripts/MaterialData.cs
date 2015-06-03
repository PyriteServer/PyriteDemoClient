namespace Pyrite
{
    using System.Text;
    using UnityEngine;

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

        public Material Material
        {
            get
            {
                if (_material == null)
                {
                    _material = CubeBuilderHelpers.GetMaterial(true, this);
                }
                return _material;
            }
        }

        private string _name;
        private Material _material;

        public MaterialData(string name)
        {
            _name = name;
        }

        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(_name))
                {
                    var sb = new StringBuilder("materialData_L");
                    sb.Append(Lod);
                    sb.Append("_");
                    sb.Append(X);
                    sb.Append("_");
                    sb.Append(Y);
                    _name = sb.ToString();
                }
                return _name;
            }
        }
    }
}