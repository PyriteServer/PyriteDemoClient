using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Microsoft.Xna.Framework
{
    internal class QuaternionHelper
    {
        public static Quaternion CreateFromRotationMatrix(Matrix matrix)
        {
            Quaternion result;
            if ((matrix.M11 + matrix.M22 + matrix.M33) > 0.0F)
            {
                float M1 = (float)System.Math.Sqrt((double)(matrix.M11 + matrix.M22 + matrix.M33 + 1.0F));
                result.w = M1 * 0.5F;
                M1 = 0.5F / M1;
                result.x = (matrix.M23 - matrix.M32) * M1;
                result.y = (matrix.M31 - matrix.M13) * M1;
                result.z = (matrix.M12 - matrix.M21) * M1;
                return result;
            }
            if ((matrix.M11 >= matrix.M22) && (matrix.M11 >= matrix.M33))
            {
                float M2 = (float)System.Math.Sqrt((double)(1.0F + matrix.M11 - matrix.M22 - matrix.M33));
                float M3 = 0.5F / M2;
                result.x = 0.5F * M2;
                result.y = (matrix.M12 + matrix.M21) * M3;
                result.z = (matrix.M13 + matrix.M31) * M3;
                result.w = (matrix.M23 - matrix.M32) * M3;
                return result;
            }
            if (matrix.M22 > matrix.M33)
            {
                float M4 = (float)System.Math.Sqrt((double)(1.0F + matrix.M22 - matrix.M11 - matrix.M33));
                float M5 = 0.5F / M4;
                result.x = (matrix.M21 + matrix.M12) * M5;
                result.y = 0.5F * M4;
                result.z = (matrix.M32 + matrix.M23) * M5;
                result.w = (matrix.M31 - matrix.M13) * M5;
                return result;
            }
            float M6 = (float)System.Math.Sqrt((double)(1.0F + matrix.M33 - matrix.M11 - matrix.M22));
            float M7 = 0.5F / M6;
            result.x = (matrix.M31 + matrix.M13) * M7;
            result.y = (matrix.M32 + matrix.M23) * M7;
            result.z = 0.5F * M6;
            result.w = (matrix.M12 - matrix.M21) * M7;
            return result;
        }

        public static Quaternion CreateFromYawPitchRoll(float yaw, float pitch, float roll)
        {
            Quaternion quaternion;
            quaternion.x = (((float)Math.Cos((double)(yaw * 0.5f)) * (float)Math.Sin((double)(pitch * 0.5f))) * (float)Math.Cos((double)(roll * 0.5f))) + (((float)Math.Sin((double)(yaw * 0.5f)) * (float)Math.Cos((double)(pitch * 0.5f))) * (float)Math.Sin((double)(roll * 0.5f)));
            quaternion.y = (((float)Math.Sin((double)(yaw * 0.5f)) * (float)Math.Cos((double)(pitch * 0.5f))) * (float)Math.Cos((double)(roll * 0.5f))) - (((float)Math.Cos((double)(yaw * 0.5f)) * (float)Math.Sin((double)(pitch * 0.5f))) * (float)Math.Sin((double)(roll * 0.5f)));
            quaternion.z = (((float)Math.Cos((double)(yaw * 0.5f)) * (float)Math.Cos((double)(pitch * 0.5f))) * (float)Math.Sin((double)(roll * 0.5f))) - (((float)Math.Sin((double)(yaw * 0.5f)) * (float)Math.Sin((double)(pitch * 0.5f))) * (float)Math.Cos((double)(roll * 0.5f)));
            quaternion.w = (((float)Math.Cos((double)(yaw * 0.5f)) * (float)Math.Cos((double)(pitch * 0.5f))) * (float)Math.Cos((double)(roll * 0.5f))) + (((float)Math.Sin((double)(yaw * 0.5f)) * (float)Math.Sin((double)(pitch * 0.5f))) * (float)Math.Sin((double)(roll * 0.5f)));
            return quaternion;
        }
    }
}
