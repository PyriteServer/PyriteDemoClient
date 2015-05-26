using System.Globalization;

namespace Pyrite
{
    using System;
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Security.Policy;

    public class CubeTracker
    {
        private bool _active = false;
        private string _DictKey;

        private LoadCubeRequest _loadCubeRequest;
        public int l;
        public int x;
        public int y;
        public int z;

        public GameObject gameObject { get; set; }
        public GameObject trackObject { get; set; }
        public PyriteQuery pyriteQuery { get; set; }

        public string DictKey
        {
            get { return _DictKey; }
            set
            {
                _DictKey = value;
                var sKey = value.Split(',');
                l = int.Parse(sKey[0]);
                x = int.Parse(sKey[1]);
                y = int.Parse(sKey[2]);
                z = int.Parse(sKey[3]);
            }
        }        

        public bool Active
        {
            get { return _active; }
            set
            {
                _active = value;
                if (gameObject != null)
                {
                    // TODO: Activate/Deactivate Cube Using Caching
                    //if (!_active)
                    //{
                    //    ClearMesh();
                    //}
                    gameObject.SetActive(_active);                    
                }
            }
        }

        public void ClearMesh()
        {
            if (gameObject == null)
                return;

            Active = false;
            gameObject.GetComponent<MeshFilter>().mesh.Clear();
            gameObject.GetComponent<Renderer>().sharedMaterial = null;            
        }

        public CubeTracker(string key)
        {
            this.DictKey = key;            
        }
    }
}