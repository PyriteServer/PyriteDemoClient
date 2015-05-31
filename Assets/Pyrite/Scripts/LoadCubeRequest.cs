namespace Pyrite
{
    using System;
    using System.Text;
    using UnityEngine;

    public class LoadCubeRequest : IEquatable<LoadCubeRequest>
    {
        public readonly int X, Y, Z, LodIndex;
        public readonly PyriteQuery Query;
        public readonly Action<GameObject> RegisterCreatedObjects;
        public readonly GameObject gameObject;
        private string _name;

        public LoadCubeRequest(int x, int y, int z, int lod, PyriteQuery query, Action<GameObject> registerCreatedObjects)
        {
            X = x;
            Y = y;
            Z = z;
            LodIndex = lod;
            Query = query;
            RegisterCreatedObjects = registerCreatedObjects;
        }

        public LoadCubeRequest(int x, int y, int z, int lod, GameObject obj)
        {
            X = x;
            Y = y;
            Z = z;
            LodIndex = lod;
            gameObject = obj;

            Query = null;
            RegisterCreatedObjects = null;
        }

        public GeometryBuffer GeometryBuffer { get; set; }
        public MaterialData MaterialData { get; set; }
        public int Failures { get; set; }

        public bool Equals(LoadCubeRequest other)
        {
            return
                X == other.X && Y == other.Y && Z == other.Z && LodIndex == other.LodIndex;
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
                var sb = new StringBuilder("lcr_Li");
                sb.Append(LodIndex);
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