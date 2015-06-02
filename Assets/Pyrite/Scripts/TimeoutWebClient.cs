#if !UNITY_WSA
namespace Pyrite
{
    using System;
    using System.Net;

    public class TimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            var w = base.GetWebRequest(uri);
            w.Timeout = 10000;
            return w;
        }
    }
}
#endif