namespace Pyrite.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
#if UNITY_STANDALONE_WIN || UNITY_WEBGL
    using ICSharpCode.SharpZipLib.GZip;
#endif
    using UnityEngine;

    public static class WwwExtensions
    {
        private const string ContentEncodingHeaderName = "CONTENT-ENCODING";
        private const string GzipContentEncodingValue = "gzip";

        public static string GetDecompressedText(this WWW www)
        {
#if UNITY_STANDALONE_WIN || UNITY_WEBGL
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
#if UNITY_STANDALONE_WIN || UNITY_WEBGL
            string contentEncoding;
            if (www.responseHeaders == null ||
                !www.responseHeaders.TryGetValue(ContentEncodingHeaderName, out contentEncoding) ||
                !contentEncoding.Equals(GzipContentEncodingValue, StringComparison.OrdinalIgnoreCase))
            {
                return www.bytes;
            }

            var buffer = new byte[4096];
            using (var stream = new MemoryStream(www.bytes))
            using (var gzip = new GZipInputStream(stream))
            using (var outMs = new MemoryStream(www.bytes.Length))
            {
                var bytesRead = 0;
                while ((bytesRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outMs.Write(buffer, 0, bytesRead);
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
            var headers = new Dictionary<string, string>();
            if (requestCompression)
            {
#if UNITY_WEBGL
                headers.Add("Accept-Encoding", "gzip, deflate");
            #endif
            }

            return new WWW(path, null, headers);
        }

        /// <summary>
        /// Create and return a WWW instance
        /// 
        /// The WWW instance is configured to perform a GET operation against the specified path
        /// 
        /// </summary>
        /// <param name="path">The path to GET</param>
        /// <returns></returns>
        public static WWW CreateWWW(string path)
        {
            return CreateWWW(path, false);
        }

        public static bool Failed(this WWW www)
        {
            return !www.Succeeded();
        }

        public static bool Succeeded(this WWW www)
        {
            return string.IsNullOrEmpty(www.error);
        }
    }
}