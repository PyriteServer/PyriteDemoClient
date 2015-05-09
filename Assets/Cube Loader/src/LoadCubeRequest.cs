namespace Assets.Cube_Loader.src
{
    using System;
    using System.Text;
    using UnityEngine;

    public class LoadCubeRequest : IEquatable<LoadCubeRequest>
    {
        public readonly int X, Y, Z, Lod;
        public readonly PyriteQuery Query;
        public readonly Action<GameObject> RegisterCreatedObjects;
        private string _name;

        public LoadCubeRequest(int x, int y, int z, int lod, PyriteQuery query,
            Action<GameObject> registerCreatedObjects)
        {
            X = x;
            Y = y;
            Z = z;
            Lod = lod;
            Query = query;
            RegisterCreatedObjects = registerCreatedObjects;
        }

        public GeometryBuffer GeometryBuffer { get; set; }
        public MaterialData MaterialData { get; set; }
        public int Failures { get; set; }

        public bool Equals(LoadCubeRequest other)
        {
            return
                X == other.X && Y == other.Y && Z == other.Z && Lod == other.Lod;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LoadCubeRequest);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public bool Cancelled { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(_name))
            {
                var sb = new StringBuilder("lcr_L");
                sb.Append(Lod);
                sb.Append(":");
                sb.Append(X);
                sb.Append("_");
                sb.Append(Y);
                sb.Append("_");
                sb.Append(Z);
                _name = sb.ToString();
            }
            return _name;
        }
    }
}