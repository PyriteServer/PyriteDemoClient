using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Microsoft.Xna.Framework
{
    internal class PlaneHelper
    {
        /// <summary>
        /// Returns a value indicating what side (positive/negative) of a plane a point is
        /// </summary>
        /// <param name="point">The point to check with</param>
        /// <param name="plane">The plane to check against</param>
        /// <returns>Greater than zero if on the positive side, less than zero if on the negative size, 0 otherwise</returns>
        public static float ClassifyPoint(ref Vector3 point, ref Plane plane)
        {
            return point.x * plane.normal.x + point.y * plane.normal.y + point.z * plane.normal.z + plane.distance;
        }

        /// <summary>
        /// Returns the perpendicular distance from a point to a plane
        /// </summary>
        /// <param name="point">The point to check</param>
        /// <param name="plane">The place to check</param>
        /// <returns>The perpendicular distance from the point to the plane</returns>
        public static float PerpendicularDistance(ref Vector3 point, ref Plane plane)
        {
            // dist = (ax + by + cz + d) / sqrt(a*a + b*b + c*c)
            return (float)Math.Abs((plane.normal.x * point.x + plane.normal.y * point.y + plane.normal.z * point.z)
                                    / Math.Sqrt(plane.normal.x * plane.normal.x + plane.normal.y * plane.normal.y + plane.normal.z * plane.normal.z));
        }

        public static Plane Normalize(Plane value)
        {
            return Normalize(value);
        }

        public static Plane Normalize(ref Plane value)
        {
            Plane result = new Plane();

            float factor;

            result.normal = Vector3.Normalize(value.normal);
            factor = (float)Math.Sqrt(result.normal.x * result.normal.x + result.normal.y * result.normal.y + result.normal.z * result.normal.z) /
                            (float)Math.Sqrt(value.normal.x * value.normal.x + value.normal.y * value.normal.y + value.normal.z * value.normal.z);
            result.distance = value.distance * factor;

            return result;
        }
    }
}