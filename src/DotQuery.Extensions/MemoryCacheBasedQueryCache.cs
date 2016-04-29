﻿using System;
#if NET451 || DNX451
using System.Runtime.Caching;
#endif
using DotQuery.Core.Caches;

namespace DotQuery.Extensions
{
#if NET451 || DNX451
    public class MemoryCacheBasedQueryCache<TKey, TValue> : IQueryCache<TKey, TValue>
    {
        private readonly IKeySerializer<TKey> m_keySerializer;
        private readonly TimeSpan m_expirationSpan;
        private MemoryCache m_objectCache = MemoryCache.Default;

        public MemoryCacheBasedQueryCache(IKeySerializer<TKey> keySerializer, TimeSpan expirationSpan)
        {
            this.m_keySerializer = keySerializer;
            this.m_expirationSpan = expirationSpan;
        }

        public void Trim()
        {
            m_objectCache.Trim(75);
        }

        public void Clear()
        {
            m_objectCache.Trim(100);
        }

        public TValue GetOrAdd(TKey key, TValue lazyTask)
        {
            object existingValue = m_objectCache.AddOrGetExisting(new CacheItem(m_keySerializer.SerializeToString(key), lazyTask), new CacheItemPolicy() { SlidingExpiration = m_expirationSpan }).Value;
            return (TValue)(existingValue ?? lazyTask);
        }

        public bool TryGet(TKey key, out TValue value)
        {
            object cached = m_objectCache.Get(m_keySerializer.SerializeToString(key));

            if (cached == null)
            {
                value = default(TValue);
                return false;
            }
            else
            {
                value = (TValue)cached;
                return true;
            }
        }

        public void Set(TKey key, TValue value)
        {
            string keyAsString = m_keySerializer.SerializeToString(key);
            m_objectCache.Set(new CacheItem(keyAsString, value), new CacheItemPolicy() { SlidingExpiration = m_expirationSpan });
            m_objectCache[keyAsString] = value;
        }

    }
#endif
}
