using UnityEngine;

namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    public static class CubeBuilderHelpers
    {
        public static void SetDefaultMaterialData(List<MaterialData> materialDatas, int x, int y)
        {
            MaterialData current = new MaterialData();

            // newmtl material_0
            current = new MaterialData();
            current.x = x;
            current.y = y;
            current.name = "material_" + x + "_" + y;
            materialDatas.Add(current);

            // Ka 0.200000 0.200000 0.200000
            current.ambient = gc(new[] { "Ka", "0.200000", "0.200000", "0.200000" });

            // Kd 0.800000 0.800000 0.800000
            current.diffuse = gc(new[] { "Kd", "0.800000", "0.800000", "0.800000" });

            // Ks 1.000000 1.000000 1.000000
            current.specular = gc(new string[] { "Ks", "1.000000", "1.000000", "1.000000" });

            // Tr 1.000000
            current.alpha = cf("1.000000");

            // illum 2
            current.illumType = ci("2");

            // Ns 0.000000
            current.shininess = cf("0.000000") / 1000;

            // map_Kd model.jpg
            current.diffuseTexPath = "model.jpg";
        }

        public static float cf(string v)
        {
            return Convert.ToSingle(v.Trim(), new CultureInfo("en-US"));
        }

        public static int ci(string v)
        {
            return Convert.ToInt32(v.Trim(), new CultureInfo("en-US"));
        }

        public static Color gc(string[] p)
        {
            return new Color(cf(p[1]), cf(p[2]), cf(p[3]));
        }

        public static Material[] GetMaterial(bool useUnlitShader, MaterialData md)
        {
            Material[] m;
            // Use an unlit shader for the model if set
            if (useUnlitShader)
            {
                m = new Material[] { new Material(Shader.Find(("Unlit/Texture"))) };
            }
            else
            {
                if (md.illumType == 2)
                {
                    m = new Material[] { new Material(Shader.Find("Specular")) };
                    m[0].SetColor("_SpecColor", md.specular);
                    m[0].SetFloat("_Shininess", md.shininess);
                }
                else
                {
                    m = new Material[] { new Material(Shader.Find("Diffuse")) };
                }

                m[0].SetColor("_Color", md.diffuse);
            }

            if (md.diffuseTex != null)
                m[0].SetTexture("_MainTex", md.diffuseTex);

            return m;
        }


    }
}
