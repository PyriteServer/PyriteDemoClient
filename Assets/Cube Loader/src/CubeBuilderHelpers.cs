namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections.Generic;
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

        public static MaterialData GetDefaultMaterialData(int x, int y, int lod)
        {
            var current = new MaterialData();

            // newmtl material_0
            current.X = x;
            current.Y = y;
            current.Lod = lod;

            // Ka 0.200000 0.200000 0.200000
            current.Ambient = Gc(new[] {"Ka", "0.200000", "0.200000", "0.200000"});

            // Kd 0.800000 0.800000 0.800000
            current.Diffuse = Gc(new[] {"Kd", "0.800000", "0.800000", "0.800000"});

            // Ks 1.000000 1.000000 1.000000
            current.Specular = Gc(new[] {"Ks", "1.000000", "1.000000", "1.000000"});

            // Tr 1.000000
            current.Alpha = Cf("1.000000");

            // illum 2
            current.IllumType = Ci("2");

            // Ns 0.000000
            current.Shininess = Cf("0.000000")/1000;

            // map_Kd model.jpg
            current.DiffuseTexPath = "model.jpg";

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
                m = new Material(Shader.Find(("Unlit/Texture")));
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
                m.SetTexture("_MainTex", md.DiffuseTex);

            return m;
        }
    }
}