using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class Cube
{
    public Vector3 MapPosition { get; set; }
    public CubeQuery Query { get; set; }
    public GeometryBuffer Buffer { get; set; }
    public List<MaterialData> MaterialData { get; set; }
    public GameObject GameObject { get; set; }
}

public class MaterialData
{
    public string name;
    public Color ambient;
    public Color diffuse;
    public Color specular;
    public float shininess;
    public float alpha;
    public int illumType;
    public int diffuseTexDivisions;
    public string diffuseTexPath;
    public Texture2D diffuseTex;
    public Texture2D[,] dividedDiffuseTex;
}
