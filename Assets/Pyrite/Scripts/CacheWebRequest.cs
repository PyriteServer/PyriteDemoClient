namespace Pyrite
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using UnityEngine;

    internal class CacheWebRequest
    {
        public enum CacheWebResponseStatus
        {
            Success = 0,
            Error = 1,
            Cancelled = 2
        }

        public struct CacheWebResponse<T>
        {
            public bool IsCacheHit { get; set; }
            public T Content { get; set; }
            public CacheWebResponseStatus Status { get; set; }
            public string ErrorMessage { get; set; }
        }

        // How many files we want to keep on disk
        private const int DefaultMaxCacheSize = 3000;
        private static int _maxCacheSize = 3000;
        // The path to the cache location on disk
        private static string _temporaryCachePath;

        private static bool _hydrated;

        private static string _proxyUrl;

        private static readonly char[] InvalidFileCharacters = Path.GetInvalidFileNameChars();

        // MRU index for cache items. The key points to the node in the list that can be used to delete it or refresh it (move it to the end)
        private static readonly Dictionary<string, LinkedListNode<string>> _cacheFileIndex =
            new Dictionary<string, LinkedListNode<string>>();

        // List of cache items. When eviction is needed the First item is deleted
        // New items should be added to the back
        private static readonly LinkedList<string> _cacheFileList = new LinkedList<string>();

        static CacheWebRequest()
        {
            BetterThreadPool.InitInstance();
            InitializeCache(DefaultMaxCacheSize);
        }

        public static void InitializeCache(int maxCacheSize, String proxyUrl = null)
        {
            _maxCacheSize = maxCacheSize;
            lock (_cacheFileList)
            {
                if (!_hydrated)
                {
                    _hydrated = true;
                    if (!Directory.Exists(TemporaryCachePath))
                    {
                        Directory.CreateDirectory(TemporaryCachePath);
                    }

                    foreach (var file in Directory.GetFiles(TemporaryCachePath))
                    {
                        InsertOrUpdateCacheEntry(file);
                    }
                }

                _proxyUrl = proxyUrl;
            }
        }

        private static char PathSeparator
        {
            get
            {
#if !UNITY_WSA
                return Path.DirectorySeparatorChar;
#else
            return '\\';
#endif
            }
        }

        private static string TemporaryCachePath
        {
            get
            {
                if (string.IsNullOrEmpty(_temporaryCachePath))
                {
                    _temporaryCachePath = Application.temporaryCachePath + PathSeparator + "PyriteCache" + PathSeparator;
#if UNITY_WSA
                    _temporaryCachePath = _temporaryCachePath.Replace("/", "\\");
#endif
                }
                return _temporaryCachePath;
            }
        }

        public static string GetCacheFilePath(string originalPath)
        {
            var sb = new StringBuilder(originalPath);
            foreach (var invalidChar in InvalidFileCharacters)
            {
                sb.Replace(invalidChar, '_');
            }
            return TemporaryCachePath + sb;
        }

        public static bool IsItemInCache(string cacheKey)
        {
            return _cacheFileIndex.ContainsKey(cacheKey);
        }

        public static void AddToCache(string cacheKey, byte[] content)
        {
            BetterThreadPool.QueueUserWorkItem(s => { SaveResponseToFileCache(cacheKey, content); });
        }

        private static void EvictCacheEntry()
        {
            lock (_cacheFileList)
            {
                var nodeToRemove = _cacheFileList.First;
                _cacheFileIndex.Remove(nodeToRemove.Value);
                _cacheFileList.RemoveFirst();
                File.Delete(nodeToRemove.Value);
            }
        }

        private static void InsertOrUpdateCacheEntry(string cacheKey)
        {
            lock (_cacheFileList)
            {
                if (_cacheFileIndex.Count > _maxCacheSize)
                {
                    EvictCacheEntry();
                }
                LinkedListNode<string> cacheNode;
                if (_cacheFileIndex.TryGetValue(cacheKey, out cacheNode))
                {
                    _cacheFileList.Remove(cacheNode);
                }
                else
                {
                    cacheNode = new LinkedListNode<string>(cacheKey);
                    _cacheFileIndex.Add(cacheKey, cacheNode);
                }
                _cacheFileList.AddLast(cacheNode);
            }
        }

        private static void SaveResponseToFileCache(string cacheFilePath, byte[] response)
        {
            File.WriteAllBytes(cacheFilePath, response);
            InsertOrUpdateCacheEntry(cacheFilePath);
        }

        public static void GetBytes(string url, Action<CacheWebResponse<byte[]>> onBytesDownloaded,
            Func<string, bool> isRequestCancelled)
        {
            BetterThreadPool.QueueUserWorkItem(state =>
            {
                var response = new CacheWebResponse<byte[]>();
                if (!isRequestCancelled(url))
                {
                    response.Status = CacheWebResponseStatus.Cancelled;
                }
                else
                {
                    var cachePath = GetCacheFilePath(url);
                    if (_cacheFileIndex.ContainsKey(cachePath))
                    {
                        try
                        {
                            response.Content = File.ReadAllBytes(cachePath);
                            response.IsCacheHit = true;
                            response.Status = CacheWebResponseStatus.Success;
                            BetterThreadPool.QueueUserWorkItem(notUsed => { InsertOrUpdateCacheEntry(cachePath); });
                        }
                        catch (IOException ioException)
                        {
                            response.Status = CacheWebResponseStatus.Error;
                            response.ErrorMessage = ioException.Message;
                        }
                    }
                    else
                    {
#if !UNITY_WSA // We do not have WebClient in Windows Store Apps
                        var client = new TimeoutWebClient();
                        // set a proxy if one was used
                        if (!string.IsNullOrEmpty(_proxyUrl))
                        {
                            client.Proxy = new WebProxy(_proxyUrl);
                        }
                        try
                        {
                            response.Content = client.DownloadData(url);
                            response.Status = CacheWebResponseStatus.Success;
                            BetterThreadPool.QueueUserWorkItem(
                                s => { SaveResponseToFileCache(cachePath, response.Content); });
                        }
                        catch (WebException wex)
                        {
                            response.Status = CacheWebResponseStatus.Error;
                            response.ErrorMessage = wex.ToString();
                        }
#else
                        Debug.LogError("This should not execute.");
#endif
                    }
                }
                onBytesDownloaded(response);
            });
        }
    }
}