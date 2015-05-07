using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Cube_Loader.src
{
    using System.IO;
    using System.Net;
    using System.Threading;
    using RestSharp;
    using UnityEngine;

    class CacheWebRequest
    {
        // How many files we want to keep on disk
        private static int _maxCacheSize = 300;
        // The path to the cache location on disk
        private static string _temporaryCachePath;
        
        // MRU index for cache items. The key points to the node in the list that can be used to delete it or refresh it (move it to the end)
        private static readonly Dictionary<string, LinkedListNode<string>> _cacheFileIndex = new Dictionary<string, LinkedListNode<string>>();
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
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                originalPath = originalPath.Replace(invalidChar, '_');
            }

            return Path.Combine(TemporaryCachePath, originalPath);
        }

        private static void EvictCacheEntry()
        {
            Debug.LogWarning("Evicting");
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

        public static void GetBytes(string url, Action<byte[]> onBytesDownloaded)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                string cachePath = GetCacheFilePath(url);
                byte[] bytes;
                if (_cacheFileIndex.ContainsKey(cachePath))
                {
                    bytes = File.ReadAllBytes(cachePath);
                    onBytesDownloaded(bytes);
                    ThreadPool.QueueUserWorkItem(notUsed =>
                    {
                        InsertOrUpdateCacheEntry(cachePath);
                    });
                }
                else
                {
                    var client = new RestClient(url);
                    var request = new RestRequest(Method.GET);
                    client.ExecuteAsync(request, (r, h) =>
                    {
                        if (r.RawBytes != null)
                        {
                            bytes = r.RawBytes;
                            ThreadPool.QueueUserWorkItem(s =>
                            {
                                SaveResponseToFileCache(cachePath, bytes);
                            });
                            onBytesDownloaded(bytes);
                        }
                    });
                }
            });
        }
    }
}
