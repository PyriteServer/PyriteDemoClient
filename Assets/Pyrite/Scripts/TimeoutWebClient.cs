using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Pyrite
{
    public class TimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = 10000;
            return w;
        }
    }
}
