using System.Globalization;

namespace Pyrite
{
    using System;
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Security.Policy;

    public class CubeTracker : MonoBehaviour
    {
        private bool _active = false;
        private string _DictKey;

        private LoadCubeRequest _loadCubeRequest;
        public int l;
        public int x;
        public int y;
        public int z;

        public GameObject gameObject { get; set; }
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
                    // TODO: Activate/Deactivate Cube
                    //gameObject.SetActive(_active);                    
                    if (value)
                    {
                        gameObject.GetComponent<MeshRenderer>().material.color = Color.yellow;
                        var loadRequest = new LoadCubeRequest(x, y, z, l, pyriteQuery, null);
                        // TODO: FIX THIS
                        //yield return StartCoroutine(EnqueueLoadCubeRequest(loadRequest));
                    }
                    else
                    {
                        gameObject.GetComponent<MeshRenderer>().material.color = Color.red;
                    }                    
                    
                    
                }
            }
        }

        public CubeTracker(string key, GameObject obj)
        {
            this.DictKey = key;
            this.gameObject = obj;
        }
    }
}