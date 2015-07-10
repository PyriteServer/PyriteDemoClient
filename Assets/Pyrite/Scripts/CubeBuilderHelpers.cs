namespace Pyrite
{
    using System;
    using System.Globalization;
    using UnityEngine;

    public static class CubeBuilderHelpers
    {
        /* OBJ file tags */
        private const string O = "o";
        private const string G = "g";
        private const string V = "v";
        private const string Vt = "vt";
        private const string Vn = "vn";
        private const string F = "f";
        private const string Mtl = "mtllib";
        private const string Uml = "usemtl";

        private static readonly Color DefaultAmbient = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color DefaultDiffuse = new Color(0.8f, 0.8f, 0.8f);
        private static readonly Color DefaultSpecular = new Color(1.0f, 1.0f, 1.0f);

        private const float DefaultAlplha = 1.0f;
        private const int DefaultIllumType = 2;
        private const float DefaultShininess = 0f;

        private static readonly Shader UnlitShader = Shader.Find("Unlit/Texture");

        public static MaterialData GetDefaultMaterialData(int x, int y, int lod, string path)
        {
            var current = new MaterialData(path)
            {
                X = x,
                Y = y,
                Lod = lod,
                Ambient = DefaultAmbient,
                Diffuse = DefaultDiffuse,
                Specular = DefaultSpecular,
                Alpha = DefaultAlplha,
                IllumType = DefaultIllumType,
                Shininess = DefaultShininess,
                DiffuseTexPath = path
            };

            return current;
        }

        private static float Cf(string v)
        {
            return Convert.ToSingle(v.Trim(), new CultureInfo("en-US"));
        }

        private static int Ci(string v)
        {
            return Convert.ToInt32(v.Trim(), new CultureInfo("en-US"));
        }

        private static Color Gc(string[] p)
        {
            return new Color(Cf(p[1]), Cf(p[2]), Cf(p[3]));
        }

        public static Material GetMaterial(bool useUnlitShader, MaterialData md)
        {
            Material m;
            // Use an unlit shader for the model if set
            if (useUnlitShader)
            {
                m = new Material(UnlitShader);
            }
            else
            {
                if (md.IllumType == 2)
                {
                    m = new Material(Shader.Find("Specular"));
                    m.SetColor("_SpecColor", md.Specular);
                    m.SetFloat("_Shininess", md.Shininess);
                }
                else
                {
                    m = new Material(Shader.Find("Diffuse"));
                }

                m.SetColor("_Color", md.Diffuse);
            }

            if (md.DiffuseTex != null)
            {
                m.SetTexture("_MainTex", md.DiffuseTex);
            }


            return m;
        }
    }
}