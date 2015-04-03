namespace Assets.Cube_Loader.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using ICSharpCode.SharpZipLib.GZip;
    using UnityEngine;
    using RestSharp;


    public static class WWWExtensions
    {
        private const string ContentEncodingHeaderName = "CONTENT-ENCODING";
        private const string GzipContentEncodingValue = "gzip";

        public static string GetDecompressedText(this WWW www)
        {
        #if UNITY_STANDALONE_WIN
            string contentEncoding;
            if (www.responseHeaders == null ||
                !www.responseHeaders.TryGetValue(ContentEncodingHeaderName, out contentEncoding) ||
                !contentEncoding.Equals(GzipContentEncodingValue, StringComparison.OrdinalIgnoreCase))
            {
                return www.text;
            }

            using (var stream = new MemoryStream(www.bytes))
            using (var gzip = new GZipInputStream(stream))
            using (var sr = new StreamReader(gzip))
            {
                return sr.ReadToEnd();
            }

        #else
            return www.text;
        #endif
        }

        public static byte[] GetDecompressedBytes(this WWW www)
        {
        #if UNITY_STANDALONE_WIN
            string contentEncoding;
            if (www.responseHeaders == null ||
                !www.responseHeaders.TryGetValue(ContentEncodingHeaderName, out contentEncoding) ||
                !contentEncoding.Equals(GzipContentEncodingValue, StringComparison.OrdinalIgnoreCase))
            {
                return www.bytes;
            }

            byte[] buffer = new byte[4096];
            using (var stream = new MemoryStream(www.bytes))
            using (var gzip = new GZipInputStream(stream))
            using (var outMs = new MemoryStream(www.bytes.Length))
            {
                int bytesRead = 0;
                while ((bytesRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outMs.Write(buffer,0, bytesRead);
                }
                return outMs.ToArray();
            }

        #else
            return www.bytes;
        #endif
        }

        /// <summary>
        /// Create and return a WWW instance
        /// 
        /// The WWW instance is configured to perform a GET operation against the specified path
        /// </summary>
        /// <param name="path">Path to GET</param>
        /// <param name="requestCompression">Indicates whether or not the "Accept-Encoding: gzip, deflate" header is set</param>
        /// <returns></returns>
        public static WWW CreateWWW(string path, bool requestCompression)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            if (requestCompression)
            {
                headers.Add("Accept-Encoding", "gzip, deflate");
            }

            return new WWW(path, null, headers);
        }

        /// <summary>
        /// Create and return a WWW instance
        /// 
        /// The WWW instance is configured to perform a GET operation against the specified path and
        /// also sets the request header indicating that compressed results will be accepted
        /// 
        /// </summary>
        /// <param name="path">The path to GET</param>
        /// <returns></returns>
        public static WWW CreateWWW(string path)
        {
            return CreateWWW(path, true);
        }
    }
}
