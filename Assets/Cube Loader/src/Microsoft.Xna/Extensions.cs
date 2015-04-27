using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Microsoft.Xna.Framework
{
    public static class Extensions
    {
        public static Vector3 Transform(this Vector3 position, Matrix matrix)
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

        public static Vector3 Normalize(this Vector3 value)
        {
            float factor = 1f / (float)Math.Sqrt((double)(value.x * value.x + value.y * value.y + value.z * value.z));
            value.x *= factor;
            value.y *= factor;
            value.z *= factor;
            return value;
        }

        public static float? Intersects(this Ray ray, BoundingBox box)
        {
            //first test if start in box
            if (ray.origin.x >= box.Min.x
                    && ray.origin.x <= box.Max.x
                    && ray.origin.y >= box.Min.y
                    && ray.origin.y <= box.Max.y
                    && ray.origin.z >= box.Min.z
                    && ray.origin.z <= box.Max.z)
                return 0.0f;// here we concidere cube is full and origine is in cube so intersect at origine

            //Second we check each face
            Vector3 maxT = new Vector3(-1.0f, -1.0f, -1.0f);
            //Vector3 minT = new Vector3(-1.0f);
            //calcul intersection with each faces
            if (ray.origin.x < box.Min.x && ray.direction.x != 0.0f)
                maxT.x = (box.Min.x - ray.origin.x) / ray.direction.x;
            else if (ray.origin.x > box.Max.x && ray.direction.x != 0.0f)
                maxT.x = (box.Max.x - ray.origin.x) / ray.direction.x;
            if (ray.origin.y < box.Min.y && ray.direction.y != 0.0f)
                maxT.y = (box.Min.y - ray.origin.y) / ray.direction.y;
            else if (ray.origin.y > box.Max.y && ray.direction.y != 0.0f)
                maxT.y = (box.Max.y - ray.origin.y) / ray.direction.y;
            if (ray.origin.z < box.Min.z && ray.direction.z != 0.0f)
                maxT.z = (box.Min.z - ray.origin.z) / ray.direction.z;
            else if (ray.origin.z > box.Max.z && ray.direction.z != 0.0f)
                maxT.z = (box.Max.z - ray.origin.z) / ray.direction.z;

            //get the maximum maxT
            if (maxT.x > maxT.y && maxT.x > maxT.z)
            {
                if (maxT.x < 0.0f)
                    return null;// ray go on opposite of face
                //coordonate of hit point of face of cube
                float coord = ray.origin.z + maxT.x * ray.direction.z;
                // if hit point coord ( intersect face with ray) is out of other plane coord it miss 
                if (coord < box.Min.z || coord > box.Max.z)
                    return null;
                coord = ray.origin.y + maxT.x * ray.direction.y;
                if (coord < box.Min.y || coord > box.Max.y)
                    return null;
                return maxT.x;
            }
            if (maxT.y > maxT.x && maxT.y > maxT.z)
            {
                if (maxT.y < 0.0f)
                    return null;// ray go on opposite of face
                //coordonate of hit point of face of cube
                float coord = ray.origin.z + maxT.y * ray.direction.z;
                // if hit point coord ( intersect face with ray) is out of other plane coord it miss 
                if (coord < box.Min.z || coord > box.Max.z)
                    return null;
                coord = ray.origin.x + maxT.y * ray.direction.x;
                if (coord < box.Min.x || coord > box.Max.x)
                    return null;
                return maxT.y;
            }
            else //Z
            {
                if (maxT.z < 0.0f)
                    return null;// ray go on opposite of face
                //coordonate of hit point of face of cube
                float coord = ray.origin.x + maxT.z * ray.direction.x;
                // if hit point coord ( intersect face with ray) is out of other plane coord it miss 
                if (coord < box.Min.x || coord > box.Max.x)
                    return null;
                coord = ray.origin.y + maxT.z * ray.direction.y;
                if (coord < box.Min.y || coord > box.Max.y)
                    return null;
                return maxT.z;
            }
        }


        //public void Intersects(ref BoundingBox box, out float? result)
        //{
        //    result = Intersects(box);
        //}


        //public float? Intersects(BoundingFrustum frustum)
        //{
        //    throw new NotImplementedException();
        //}


        //public float? Intersects(BoundingSphere sphere)
        //{
        //    float? result;
        //    Intersects(ref sphere, out result);
        //    return result;
        //}

        //public float? Intersects(Plane plane)
        //{
        //    throw new NotImplementedException();
        //}

        //public void Intersects(ref Plane plane, out float? result)
        //{
        //    throw new NotImplementedException();
        //}

        public static float? Intersects(this Ray ray, BoundingSphere sphere)
        {
            float? result = null;

            // Find the vector between where the ray starts the the sphere's centre
            Vector3 difference = sphere.Center - ray.origin;

            float differenceLengthSquared = difference.sqrMagnitude;
            float sphereRadiusSquared = sphere.Radius * sphere.Radius;

            float distanceAlongRay;

            // If the distance between the ray start and the sphere's centre is less than
            // the radius of the sphere, it means we've intersected. N.B. checking the LengthSquared is faster.
            if (differenceLengthSquared < sphereRadiusSquared)
            {
                return result = 0.0f;
            }

            //Vector3Helper.Dot(ref this.Direction, ref difference, out distanceAlongRay);
            distanceAlongRay = Vector3Helper.Dot(ray.direction, difference);
            // If the ray is pointing away from the sphere then we don't ever intersect
            if (distanceAlongRay < 0)
            {
                return result = null;
            }

            // Next we kinda use Pythagoras to check if we are within the bounds of the sphere
            // if x = radius of sphere
            // if y = distance between ray position and sphere centre
            // if z = the distance we've travelled along the ray
            // if x^2 + z^2 - y^2 < 0, we do not intersect
            float dist = sphereRadiusSquared + distanceAlongRay * distanceAlongRay - differenceLengthSquared;

            return result = (dist < 0) ? null : distanceAlongRay - (float?)Math.Sqrt(dist);
        }
        
    }
}
