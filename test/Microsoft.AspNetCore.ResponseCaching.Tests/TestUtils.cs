﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.ResponseCaching.Tests
{
    internal class TestUtils
    {
        internal static RequestDelegate TestRequestDelegate = async (context) =>
        {
            var uniqueId = Guid.NewGuid().ToString();
            var headers = context.Response.GetTypedHeaders();
            headers.CacheControl = new CacheControlHeaderValue()
            {
                Public = true,
                MaxAge = TimeSpan.FromSeconds(10)
            };
            headers.Date = DateTimeOffset.UtcNow;
            headers.Headers["X-Value"] = uniqueId;
            await context.Response.WriteAsync(uniqueId);
        };

        internal static IResponseCacheKeyProvider CreateTestKeyProvider()
        {
            return CreateTestKeyProvider(new ResponseCacheOptions());
        }

        internal static IResponseCacheKeyProvider CreateTestKeyProvider(ResponseCacheOptions options)
        {
            return new ResponseCacheKeyProvider(new DefaultObjectPoolProvider(), Options.Create(options));
        }

        internal static IWebHostBuilder CreateBuilderWithResponseCache(
            Action<IApplicationBuilder> configureDelegate = null,
            ResponseCacheOptions options = null,
            RequestDelegate requestDelegate = null)
        {
            if (configureDelegate == null)
            {
                configureDelegate = app => { };
            }
            if (options == null)
            {
                options = new ResponseCacheOptions();
            }
            if (requestDelegate == null)
            {
                requestDelegate = TestRequestDelegate;
            }

            return new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddDistributedResponseCache();
                })
                .Configure(app =>
                {
                    configureDelegate(app);
                    app.UseResponseCache(options);
                    app.Run(requestDelegate);
                });
        }

        internal static ResponseCacheMiddleware CreateTestMiddleware(
            IResponseCacheStore store = null,
            ResponseCacheOptions options = null,
            IResponseCacheKeyProvider keyProvider = null,
            IResponseCachePolicyProvider policyProvider = null)
        {
            if (store == null)
            {
                store = new TestResponseCacheStore();
            }
            if (options == null)
            {
                options = new ResponseCacheOptions();
            }
            if (keyProvider == null)
            {
                keyProvider = new ResponseCacheKeyProvider(new DefaultObjectPoolProvider(), Options.Create(options));
            }
            if (policyProvider == null)
            {
                policyProvider = new TestResponseCachePolicyProvider();
            }

            return new ResponseCacheMiddleware(
                httpContext => TaskCache.CompletedTask,
                store,
                Options.Create(options),
                policyProvider,
                keyProvider);
        }

        internal static ResponseCacheContext CreateTestContext()
        {
            return new ResponseCacheContext(new DefaultHttpContext());
        }
    }

    internal class DummySendFileFeature : IHttpSendFileFeature
    {
        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellation)
        {
            return TaskCache.CompletedTask;
        }
    }

    internal class TestResponseCachePolicyProvider : IResponseCachePolicyProvider
    {
        public bool IsCachedEntryFresh(ResponseCacheContext context) => true;

        public bool IsRequestCacheable(ResponseCacheContext context) => true;

        public bool IsResponseCacheable(ResponseCacheContext context) => true;
    }

    internal class TestResponseCacheKeyProvider : IResponseCacheKeyProvider
    {
        private readonly string _baseKey;
        private readonly StringValues _varyKey;

        public TestResponseCacheKeyProvider(string lookupBaseKey = null, StringValues? lookupVaryKey = null)
        {
            _baseKey = lookupBaseKey;
            if (lookupVaryKey.HasValue)
            {
                _varyKey = lookupVaryKey.Value;
            }
        }

        public IEnumerable<string> CreateLookupVaryByKeys(ResponseCacheContext context)
        {
            foreach (var varyKey in _varyKey)
            {
                yield return _baseKey + varyKey;
            }
        }

        public string CreateBaseKey(ResponseCacheContext context)
        {
            return _baseKey;
        }

        public string CreateStorageVaryByKey(ResponseCacheContext context)
        {
            throw new NotImplementedException();
        }
    }

    internal class TestResponseCacheStore : IResponseCacheStore
    {
        private readonly IDictionary<string, object> _storage = new Dictionary<string, object>();
        public int GetCount { get; private set; }
        public int SetCount { get; private set; }

        public object Get(string key)
        {
            GetCount++;
            try
            {
                return _storage[key];
            }
            catch
            {
                return null;
            }
        }

        public void Remove(string key)
        {
        }

        public void Set(string key, object entry, TimeSpan validFor)
        {
            SetCount++;
            _storage[key] = entry;
        }
    }
}