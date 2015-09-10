// // //------------------------------------------------------------------------------------------------- 
// // // <copyright file="CubeBounds.cs" company="Microsoft Corporation">
// // // Copyright (c) Microsoft Corporation. All rights reserved.
// // // </copyright>
// // //-------------------------------------------------------------------------------------------------

namespace Pyrite.Model
{
    using System;
    using Client.Contracts;
    using Client.Model;
    using Microsoft.Xna.Framework;
    using UnityEngine;

    public class CubeBounds : IBounds<CubeBounds>
    {
        public BoundingBox BoundingBox { get; set; }
        public Microsoft.Xna.Framework.BoundingSphere BoundingSphere { get; set; }

        public Intersection<CubeBounds> Intersects(Ray ray)
        {
            if (BoundingBox.Max != BoundingBox.Min)
            {
                if (BoundingBox.Intersects(ray) != null)
                    return new Intersection<CubeBounds>(this);
            }
            else if (BoundingSphere.Radius != 0f)
            {
                if (BoundingSphere.Intersects(ray) != null)
                    return new Intersection<CubeBounds>(this);
            }

            return null;
        }

        public Intersection<CubeBounds> Intersects(CubeBounds obj)
        {
            Intersection<CubeBounds> ir;

            if (obj.BoundingBox.Min != obj.BoundingBox.Max)
            {
                ir = Intersects(obj.BoundingBox);
            }
            else if (obj.BoundingSphere.Radius != 0f)
            {
                ir = Intersects(obj.BoundingSphere);
            }
            else
                return null;

            if (ir != null)
            {
                ir.Object = this;
                ir.OtherObject = obj;
            }

            return ir;
        }

        public Intersection<CubeBounds> Intersects(Microsoft.Xna.Framework.BoundingSphere intersectionSphere)
        {
            if (BoundingBox.Max != BoundingBox.Min)
            {
                if (BoundingBox.Contains(intersectionSphere) != ContainmentType.Disjoint)
                    return new Intersection<CubeBounds>(this);
            }
            else if (BoundingSphere.Radius != 0f)
            {
                if (BoundingSphere.Contains(intersectionSphere) != ContainmentType.Disjoint)
                    return new Intersection<CubeBounds>(this);
            }

            return null;
        }

        public Intersection<CubeBounds> Intersects(BoundingBox intersectionBox)
        {
            if (BoundingBox.Max != BoundingBox.Min)
            {
                var ct = BoundingBox.Contains(intersectionBox);
                if (ct != ContainmentType.Disjoint)
                    return new Intersection<CubeBounds>(this);
            }
            else if (BoundingSphere.Radius != 0f)
            {
                if (BoundingSphere.Contains(intersectionBox) != ContainmentType.Disjoint)
                    return new Intersection<CubeBounds>(this);
            }

            return null;
        }

        public Intersection<CubeBounds> Intersects(BoundingFrustum frustum)
        {
            if (BoundingBox.Max != BoundingBox.Min)
            {
                var ct = BoundingBox.Contains(frustum);
                if (ct != ContainmentType.Disjoint)
                    return new Intersection<CubeBounds>(this);
            }
            else if (BoundingSphere.Radius != 0f)
            {
                if (BoundingSphere.Contains(frustum) != ContainmentType.Disjoint)
                    return new Intersection<CubeBounds>(this);
            }

            return null;
        }

        public override string ToString()
        {
            return String.Format("{0} BoundingBox:{1}", GetType().Name, BoundingBox);
        }
    }
}