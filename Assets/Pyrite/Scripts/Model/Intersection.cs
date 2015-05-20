using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Pyrite.Client.Model
{
    public class Intersection<TObject>
    {
        private readonly bool hasHit = false;

        //public Intersection()
        //{
        //    this.Position = Vector3.zero;
        //    this.Normal = Vector3.zero;
        //    this.Ray = new Ray();
        //    this.Distance = float.MaxValue;
        //    this.Object = default(TObject);
        //}

        public Intersection(Vector3 hitPos, Vector3 hitNormal, Ray ray, double distance)
        {
            this.Position = hitPos;
            this.Normal = hitNormal;
            this.Ray = ray;
            this.Distance = distance;
            this.hasHit = true;
        }

        public Intersection(TObject hitObject = default(TObject))
        {
            this.hasHit = hitObject != null;
            this.Object = hitObject;
            this.Position = Vector3.zero;
            this.Normal = Vector3.zero;
            this.Ray = new Ray();
            this.Distance = 0.0f;
        }

        public double Distance { get; private set; }

        public bool HasHit
        {
            get { return this.hasHit; }
        }

        public Vector3 Normal { get; private set; }
        public TObject Object { get; set; }
        public TObject OtherObject { get; set; }
        public Vector3 Position { get; private set; }
        public Ray Ray { get; private set; }

        public override bool Equals(object otherRecord)
        {
            Intersection<TObject> o = (Intersection<TObject>)otherRecord;
            return o.Equals(this);
        }
    }
}
