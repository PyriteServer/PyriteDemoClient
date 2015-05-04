using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Microsoft.Xna.Framework
{
    internal class Vector3Helper
    {
        public static Vector3 Cross(Vector3 vector1, Vector3 vector2)
        {
            Vector3 result;
            result.x = vector1.y * vector2.z - vector2.y * vector1.z;
            result.y = vector2.x * vector1.z - vector1.x * vector2.z;
            result.z = vector1.x * vector2.y - vector2.x * vector1.y;
            return result;
        }

        public static void Cross(ref Vector3 vector1, ref Vector3 vector2, out Vector3 result)
        {
            result.x = vector1.y * vector2.z - vector2.y * vector1.z;
            result.y = vector2.x * vector1.z - vector1.x * vector2.z;
            result.z = vector1.x * vector2.y - vector2.x * vector1.y;
        }

        public static float Dot(Vector3 vector1, Vector3 vector2)
        {
            return vector1.x * vector2.x + vector1.y * vector2.y + vector1.z * vector2.z;
        }

        public static Vector3 Normalize(ref Vector3 value)
        {
            float factor = 1f / (float)Math.Sqrt((double)(value.x * value.x + value.y * value.y + value.z * value.z));
            value.x *= factor;
            value.y *= factor;
            value.z *= factor;
            return value;
        }

        public static void Normalize(ref Vector3 value, out Vector3 result)
        {
            float factor = 1f / (float)Math.Sqrt((double)(value.x * value.x + value.y * value.y + value.z * value.z));
            result.x = value.x * factor;
            result.y = value.y * factor;
            result.z = value.z * factor;
        }

        public static Vector3 Transform(ref Vector3 position, Matrix matrix)
        {
            Transform(ref position, ref matrix, out position);
            return position;
        }

        public static void Transform(ref Vector3 position, ref Matrix matrix, out Vector3 result)
        {
            result = new Vector3((position.x * matrix.M11) + (position.y * matrix.M21) + (position.z * matrix.M31) + matrix.M41,
                                 (position.x * matrix.M12) + (position.y * matrix.M22) + (position.z * matrix.M32) + matrix.M42,
                                 (position.x * matrix.M13) + (position.y * matrix.M23) + (position.z * matrix.M33) + matrix.M43);
        }
    }
}
