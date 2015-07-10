#if !UNITY_WSA || UNITY_EDITOR
namespace Pyrite
{
    using System;
    using System.Net;

    [System.ComponentModel.DesignerCategory("Code")]
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