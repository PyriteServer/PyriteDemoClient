namespace Assets.Cube_Loader.src
{
    using System;
    using UnityEngine;

    public class LoadCubeRequest : IEquatable<LoadCubeRequest>
    {
        public readonly int X, Y, Z, Lod;
        public readonly PyriteQuery Query;
        public readonly Action<GameObject[]> RegisterCreatedObjects;
        private string _name;

        public LoadCubeRequest(int x, int y, int z, int lod, PyriteQuery query,
            Action<GameObject[]> registerCreatedObjects)
        {
            X = x;
            Y = y;
            Z = z;
            Lod = lod;
            Query = query;
            RegisterCreatedObjects = registerCreatedObjects;
        }

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
                _name = string.Format("lcr_{0}_{1}_{2}_{3}", X, Y, Z, Lod);
            }
            return _name;
        }
    }
}