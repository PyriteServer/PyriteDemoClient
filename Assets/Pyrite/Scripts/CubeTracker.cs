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

        public string DictKey { get; set; }
        public GameObject gameObject { get; set; }
        public int TTL { get; set; }

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
                    gameObject.GetComponent<MeshRenderer>().material.color = value ? Color.yellow : Color.red;
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