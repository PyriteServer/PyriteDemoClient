namespace Pyrite
{
    using System.Collections;
    using UnityEngine;

    public static class Connection
    {
        public static ConnectionState State { get; private set; }

        public static IEnumerator CheckConnection()
        {
            State = ConnectionState.Connecting;
            var internetChecker = new WWW("http://api.pyrite3d.org/sets");
            yield return internetChecker;
            State = string.IsNullOrEmpty(internetChecker.error)
                ? ConnectionState.Connected
                : ConnectionState.NotConnected;
        }
    }
}