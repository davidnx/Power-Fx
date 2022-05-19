﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx.Types;

namespace Microsoft.PowerFx.Connectors
{
    /// <summary>
    /// Interface for caching GET requests. 
    /// </summary>
    public interface ICachingHttpClient
    {
        // Clear all cache entries for the given namespace. 
        void Reset(string cacheScope);

        Task<FormulaValue> TryGetAsync(string cacheScope, string requestKey, Func<Task<FormulaValue>> getter);
    }

    /// <summary>
    /// Implementation that skips caching. 
    /// </summary>
    public class NonCachingClient : ICachingHttpClient
    {
        public static ICachingHttpClient Instance { get; } = new NonCachingClient();

        public void Reset(string cacheScope)
        {
        }

        public Task<FormulaValue> TryGetAsync(string cacheScope, string requestKey, Func<Task<FormulaValue>> getter)
        {
            return getter();
        }
    }        

    /// <summary>
    /// Simple cache for GETs. CAche is reset when a POST is made. 
    /// </summary>
    public class CachingHttpClient : ICachingHttpClient
    {
        // $$$ Empty on max size...

        // For GETs, map of URL to response
        private readonly Dictionary<string, Dictionary<string, FormulaValue>> _cache = new Dictionary<string, Dictionary<string, FormulaValue>>();

        public CachingHttpClient()
        {            
        }

        public void Reset(string cacheScope)
        {
            lock (_cache)
            {
                _cache.Remove(cacheScope);
            }
        }

        public async Task<FormulaValue> TryGetAsync(string cacheScope, string requestKey, Func<Task<FormulaValue>> getter)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(cacheScope, out var dict2))
                {
                    if (dict2.TryGetValue(requestKey, out var cachedResult))
                    {
                        return cachedResult;
                    }
                }
            }

            // Cache miss - get the result. 
            var result = await getter();

            lock (_cache)
            {
                if (!_cache.TryGetValue(cacheScope, out var dict2))
                {
                    dict2 = new Dictionary<string, FormulaValue>();
                    _cache[cacheScope] = dict2;
                }

                dict2[requestKey] = result;
            }

            return result;
        }
    }
}
