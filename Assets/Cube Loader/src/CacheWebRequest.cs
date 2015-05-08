namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using RestSharp;
    using UnityEngine;

    internal class CacheWebRequest
    {
        public struct CacheWebResponse<T>
        {
            public bool IsCacheHit { get; set; }
            public T Content { get; set; }
            public bool IsError { get; set; }
            public string ErrorMessage { get; set; }
        }

        // How many files we want to keep on disk
        private static readonly int _maxCacheSize = 3000;
        // The path to the cache location on disk
        private static string _temporaryCachePath;

        private static readonly char[] InvalidFileCharacters = Path.GetInvalidFileNameChars();

        // MRU index for cache items. The key points to the node in the list that can be used to delete it or refresh it (move it to the end)
        private static readonly Dictionary<string, LinkedListNode<string>> _cacheFileIndex =
            new Dictionary<string, LinkedListNode<string>>();

        // List of cache items. When eviction is needed the First item is deleted
        // New items should be added to the back
        private static readonly LinkedList<string> _cacheFileList = new LinkedList<string>();


        static CacheWebRequest()
        {
            RehydrateCache();
            int workerThreads, completionPortThreads;
            ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
            Debug.Log("Max Worker: " + workerThreads + ", Cmpl: " + completionPortThreads);
            ThreadPool.GetMinThreads(out workerThreads, out completionPortThreads);
            Debug.Log("Min Worker: " + workerThreads + ", Cmpl: " + completionPortThreads);
        }

        private static void RehydrateCache()
        {
            foreach (var file in Directory.GetFiles(TemporaryCachePath))
            {
                InsertOrUpdateCacheEntry(file);
            }
        }

        private static string TemporaryCachePath
        {
            get
            {
                if (string.IsNullOrEmpty(_temporaryCachePath))
                {
                    _temporaryCachePath = Application.temporaryCachePath;
                }
                return _temporaryCachePath;
            }
        }

        private static string GetCacheFilePath(string originalPath)
        {
            var sb = new StringBuilder(originalPath);
            foreach (var invalidChar in InvalidFileCharacters)
            {
                sb.Replace(invalidChar, '_');
                // originalPath = originalPath.Replace(invalidChar, '_');
            }

            return TemporaryCachePath + Path.DirectorySeparatorChar + sb;
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

        public static void GetBytes(string url, Action<CacheWebResponse<byte[]>> onBytesDownloaded)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var cachePath = GetCacheFilePath(url);
                if (_cacheFileIndex.ContainsKey(cachePath))
                {
                    var response = new CacheWebResponse<byte[]>();
                    try
                    {
                        response.Content = File.ReadAllBytes(cachePath);
                        response.IsCacheHit = true;
                        response.IsError = false;
                        ThreadPool.QueueUserWorkItem(notUsed => { InsertOrUpdateCacheEntry(cachePath); });
                    }
                    catch (IOException ioException)
                    {
                        response.IsError = true;
                        response.ErrorMessage = ioException.Message;
                    }

                    onBytesDownloaded(response);
                }
                else
                {
                    var client = new RestClient(url);
                    var request = new RestRequest(Method.GET);
                    client.ExecuteAsync(request, (r, h) =>
                    {
                        var response = new CacheWebResponse<byte[]>();
                        response.IsCacheHit = false;
                        if (r.RawBytes != null)
                        {
                            response.Content = r.RawBytes;
                            response.IsError = false;
                            ThreadPool.QueueUserWorkItem(s => { SaveResponseToFileCache(cachePath, r.RawBytes); });
                            onBytesDownloaded(response);
                        }
                        else
                        {
                            response.IsError = true;
                            if (!string.IsNullOrEmpty(r.ErrorMessage))
                            {
                                response.ErrorMessage = r.ErrorMessage;
                            }
                            else
                            {
                                response.ErrorMessage = "Content was null. StatusCode: " + r.StatusCode;
                            }
                            onBytesDownloaded(response);
                        }
                    });
                }
            });
        }
    }
}