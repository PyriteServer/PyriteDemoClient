#region License
/*
MIT License
Copyright © 2006 The Mono.Xna Team

All rights reserved.

Authors:
Olivier Dufour (Duff)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion License

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.Xna.Framework
{
    public struct BoundingBox : IEquatable<BoundingBox>
    {

        #region Public Fields

        public Vector3 Min;
        public Vector3 Max;
        public const int CornerCount = 8;

        #endregion Public Fields


        #region Public Constructors

        public BoundingBox(Vector3 min, Vector3 max)
        {
            this.Min = min;
            this.Max = max;
        }

        #endregion Public Constructors


        #region Public Methods

        public ContainmentType Contains(BoundingBox box)
        {
            //test if all corner is in the same side of a face by just checking min and max
            if (box.Max.x < Min.x
                || box.Min.x > Max.x
                || box.Max.y < Min.y
                || box.Min.y > Max.y
                || box.Max.z < Min.z
                || box.Min.z > Max.z)
                return ContainmentType.Disjoint;


            if (box.Min.x >= Min.x
                && box.Max.x <= Max.x
                && box.Min.y >= Min.y
                && box.Max.y <= Max.y
                && box.Min.z >= Min.z
                && box.Max.z <= Max.z)
                return ContainmentType.Contains;

            return ContainmentType.Intersects;
        }

        public void Contains(ref BoundingBox box, out ContainmentType result)
        {
            result = Contains(box);
        }

        public ContainmentType Contains(BoundingFrustum frustum)
        {
            //TODO: bad done here need a fix. 
            //Because question is not frustum contain box but reverse and this is not the same
            int i;
            ContainmentType contained;
            Vector3[] corners = frustum.GetCorners();

            // First we check if frustum is in box
            for (i = 0; i < corners.Length; i++)
            {
                this.Contains(ref corners[i], out contained);
                if (contained == ContainmentType.Disjoint)
                    break;
            }

            if (i == corners.Length) // This means we checked all the corners and they were all contain or instersect
                return ContainmentType.Contains;

            if (i != 0)             // if i is not equal to zero, we can fastpath and say that this box intersects
                return ContainmentType.Intersects;


            // If we get here, it means the first (and only) point we checked was actually contained in the frustum.
            // So we assume that all other points will also be contained. If one of the points is disjoint, we can
            // exit immediately saying that the result is Intersects
            i++;
            for (; i < corners.Length; i++)
            {
                this.Contains(ref corners[i], out contained);
                if (contained != ContainmentType.Contains)
                    return ContainmentType.Intersects;

            }

            // If we get here, then we know all the points were actually contained, therefore result is Contains
            return ContainmentType.Contains;
        }

        public ContainmentType Contains(BoundingSphere sphere)
        {
            if (sphere.Center.x - Min.x > sphere.Radius
                && sphere.Center.y - Min.y > sphere.Radius
                && sphere.Center.z - Min.z > sphere.Radius
                && Max.x - sphere.Center.x > sphere.Radius
                && Max.y - sphere.Center.y > sphere.Radius
                && Max.z - sphere.Center.z > sphere.Radius)
                return ContainmentType.Contains;

            double dmin = 0;

            // Fixed bug in logic for sphere intersection

            if (sphere.Center.x < Min.x)
                dmin += (sphere.Center.x - Min.x) * (sphere.Center.x - Min.x);
            else if (Max.x < sphere.Center.x)
                dmin += (sphere.Center.x - Max.x) * (sphere.Center.x - Max.x);

            if (sphere.Center.y < Min.y)
                dmin += (sphere.Center.y - Min.y) * (sphere.Center.y - Min.y);
            else if (Max.y < sphere.Center.y )
                dmin += (sphere.Center.y - Max.y) * (sphere.Center.y - Max.y);

            if (sphere.Center.z < Min.z)
                dmin += (sphere.Center.z - Min.z) * (sphere.Center.z - Min.z);
            else if (Max.z < sphere.Center.z)
                dmin += (sphere.Center.z - Max.z) * (sphere.Center.z - Max.z);

            if (dmin <= sphere.Radius * sphere.Radius)
                return ContainmentType.Intersects;

            return ContainmentType.Disjoint;
        }

        public void Contains(ref BoundingSphere sphere, out ContainmentType result)
        {
            result = this.Contains(sphere);
        }

        public ContainmentType Contains(Vector3 point)
        {
            ContainmentType result;
            this.Contains(ref point, out result);
            return result;
        }

        public void Contains(ref Vector3 point, out ContainmentType result)
        {
            //first we get if point is out of box
            if (point.x < this.Min.x
                || point.x > this.Max.x
                || point.y < this.Min.y
                || point.y > this.Max.y
                || point.z < this.Min.z
                || point.z > this.Max.z)
            {
                result = ContainmentType.Disjoint;
            }//or if point is on box because coordonate of point is lesser or equal
            else if (point.x == this.Min.x
                || point.x == this.Max.x
                || point.y == this.Min.y
                || point.y == this.Max.y
                || point.z == this.Min.z
                || point.z == this.Max.z)
                result = ContainmentType.Intersects;
            else
                result = ContainmentType.Contains;


        }

        public static BoundingBox CreateFromPoints(IEnumerable<Vector3> points)
        {
            if (points == null)
                throw new ArgumentNullException();

            // TODO: Just check that Count > 0
            bool empty = true;
            Vector3 vector2 = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 vector1 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (Vector3 vector3 in points)
            {
                vector2 = Vector3.Min(vector2, vector3);
                vector1 = Vector3.Max(vector1, vector3);
                empty = false;
            }
            if (empty)
                throw new ArgumentException();

            return new BoundingBox(vector2, vector1);
        }

        public static BoundingBox CreateFromSphere(BoundingSphere sphere)
        {
            Vector3 vector1 = new Vector3(sphere.Radius, sphere.Radius, sphere.Radius);
            return new BoundingBox(sphere.Center - vector1, sphere.Center + vector1);
        }

        public static void CreateFromSphere(ref BoundingSphere sphere, out BoundingBox result)
        {
            result = BoundingBox.CreateFromSphere(sphere);
        }

        public static BoundingBox CreateMerged(BoundingBox original, BoundingBox additional)
        {
            return new BoundingBox(
                Vector3.Min(original.Min, additional.Min), Vector3.Max(original.Max, additional.Max));
        }

        public static void CreateMerged(ref BoundingBox original, ref BoundingBox additional, out BoundingBox result)
        {
            result = BoundingBox.CreateMerged(original, additional);
        }

        public bool Equals(BoundingBox other)
        {
            return (this.Min == other.Min) && (this.Max == other.Max);
        }

        public override bool Equals(object obj)
        {
            return (obj is BoundingBox) ? this.Equals((BoundingBox)obj) : false;
        }

        public Vector3[] GetCorners()
        {
            return new Vector3[] {
                new Vector3(this.Min.x, this.Max.y, this.Max.z), 
                new Vector3(this.Max.x, this.Max.y, this.Max.z),
                new Vector3(this.Max.x, this.Min.y, this.Max.z), 
                new Vector3(this.Min.x, this.Min.y, this.Max.z), 
                new Vector3(this.Min.x, this.Max.y, this.Min.z),
                new Vector3(this.Max.x, this.Max.y, this.Min.z),
                new Vector3(this.Max.x, this.Min.y, this.Min.z),
                new Vector3(this.Min.x, this.Min.y, this.Min.z)
            };
        }

        public void GetCorners(Vector3[] corners)
        {
            if (corners == null)
            {
                throw new ArgumentNullException("corners");
            }
            if (corners.Length < 8)
            {
                throw new ArgumentOutOfRangeException("corners", "Not Enought Corners");
            }
            corners[0].x = this.Min.x;
            corners[0].y = this.Max.y;
            corners[0].z = this.Max.z;
            corners[1].x = this.Max.x;
            corners[1].y = this.Max.y;
            corners[1].z = this.Max.z;
            corners[2].x = this.Max.x;
            corners[2].y = this.Min.y;
            corners[2].z = this.Max.z;
            corners[3].x = this.Min.x;
            corners[3].y = this.Min.y;
            corners[3].z = this.Max.z;
            corners[4].x = this.Min.x;
            corners[4].y = this.Max.y;
            corners[4].z = this.Min.z;
            corners[5].x = this.Max.x;
            corners[5].y = this.Max.y;
            corners[5].z = this.Min.z;
            corners[6].x = this.Max.x;
            corners[6].y = this.Min.y;
            corners[6].z = this.Min.z;
            corners[7].x = this.Min.x;
            corners[7].y = this.Min.y;
            corners[7].z = this.Min.z;
        }

        public override int GetHashCode()
        {
            return this.Min.GetHashCode() + this.Max.GetHashCode();
        }

        public bool Intersects(BoundingBox box)
        {
            bool result;
            Intersects(ref box, out result);
            return result;
        }

        public void Intersects(ref BoundingBox box, out bool result)
        {
            if ((this.Max.x >= box.Min.x) && (this.Min.x <= box.Max.x))
            {
                if ((this.Max.y < box.Min.y) || (this.Min.y > box.Max.y))
                {
                    result = false;
                    return;
                }

                result = (this.Max.z >= box.Min.z) && (this.Min.z <= box.Max.z);
                return;
            }

            result = false;
            return;
        }

        public bool Intersects(BoundingFrustum frustum)
        {
            return frustum.Intersects(this);
        }

        public bool Intersects(BoundingSphere sphere)
        {
            if (sphere.Center.x - Min.x > sphere.Radius
                && sphere.Center.y - Min.y > sphere.Radius
                && sphere.Center.z - Min.z > sphere.Radius
                && Max.x - sphere.Center.x > sphere.Radius
                && Max.y - sphere.Center.y > sphere.Radius
                && Max.z - sphere.Center.z > sphere.Radius)
                return true;

            double dmin = 0;

            if (sphere.Center.x - Min.x <= sphere.Radius)
                dmin += (sphere.Center.x - Min.x) * (sphere.Center.x - Min.x);
            else if (Max.x - sphere.Center.x <= sphere.Radius)
                dmin += (sphere.Center.x - Max.x) * (sphere.Center.x - Max.x);

            if (sphere.Center.y - Min.y <= sphere.Radius)
                dmin += (sphere.Center.y - Min.y) * (sphere.Center.y - Min.y);
            else if (Max.y - sphere.Center.y <= sphere.Radius)
                dmin += (sphere.Center.y - Max.y) * (sphere.Center.y - Max.y);

            if (sphere.Center.z - Min.z <= sphere.Radius)
                dmin += (sphere.Center.z - Min.z) * (sphere.Center.z - Min.z);
            else if (Max.z - sphere.Center.z <= sphere.Radius)
                dmin += (sphere.Center.z - Max.z) * (sphere.Center.z - Max.z);

            if (dmin <= sphere.Radius * sphere.Radius)
                return true;

            return false;
        }

        public void Intersects(ref BoundingSphere sphere, out bool result)
        {
            result = Intersects(sphere);
        }

        public PlaneIntersectionType Intersects(Plane plane)
        {
            //check all corner side of plane
            Vector3[] corners = this.GetCorners();
            float lastdistance = Vector3.Dot(plane.normal, corners[0]) + plane.distance;

            for (int i = 1; i < corners.Length; i++)
            {
                float distance = Vector3.Dot(plane.normal, corners[i]) + plane.distance;
                if ((distance <= 0.0f && lastdistance > 0.0f) || (distance >= 0.0f && lastdistance < 0.0f))
                    return PlaneIntersectionType.Intersecting;
                lastdistance = distance;
            }

            if (lastdistance > 0.0f)
                return PlaneIntersectionType.Front;

            return PlaneIntersectionType.Back;

        }

        public void Intersects(ref Plane plane, out PlaneIntersectionType result)
        {
            result = Intersects(plane);
        }

        public Nullable<float> Intersects(Ray ray)
        {
            return ray.Intersects(this);
        }

        public void Intersects(ref Ray ray, out Nullable<float> result)
        {
            result = Intersects(ray);
        }

        public static bool operator ==(BoundingBox a, BoundingBox b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(BoundingBox a, BoundingBox b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return string.Format("{{Min:{0} Max:{1}}}", this.Min.ToString(), this.Max.ToString());
        }

        #endregion Public Methods
    }
}
