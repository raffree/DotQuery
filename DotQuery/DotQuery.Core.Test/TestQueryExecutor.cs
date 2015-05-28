﻿using System;
using DotQuery.Core.Async;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotQuery.Core.Test.Stub;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DotQuery.Core.Test
{
    using System.Runtime.Caching;

    using DotQuery.Extensions;

    [TestClass]
    public class TestQueryExecutor
    {
        private TimeSpan m_delayTime;
        private AsyncQueryExecutor<AddQuery, int> m_exec;

        private static TimeSpan TimeCost(Func<Task> a)
        {
            Stopwatch sw = Stopwatch.StartNew();
            a().Wait();
            return sw.Elapsed;
        }

        private static bool TryCatchException<TException>(Action a) where TException : Exception
        {
            try
            {
                a();
            }
            catch (AggregateException e)
            {
                if (e.InnerException is TException)
                {
                    return true;
                }
            }
            catch (TException)
            {
                return true;
            }
            return false;
        }

        [TestInitialize]
        public void Init()
        {
            m_delayTime = TimeSpan.FromMilliseconds(200);
            m_exec = new AddAsyncQueryExecutor(m_delayTime);
        }

        [TestMethod]
        public void TestCacheHit()
        {
            var q1 = new AddQuery { Left = 1, Right = 2 };
            var q2 = new AddQuery { Left = 1, Right = 2 };

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q1)); }) 
                >= 
                m_delayTime);

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q2)); })
                <=
                TimeSpan.FromMilliseconds(10));  //well, a cache hit
        }

        [TestMethod]
        public void TestQueryOptions()
        {
            var q1 = new AddQuery { Left = 1, Right = 2 };
            var q2 = new AddQuery { Left = 1, Right = 2 , QueryOptions = QueryOptions.None};

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q1)); })
                >=
                m_delayTime);

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q2)); })
                >=
                m_delayTime);  //well, should not hit
        }

        [TestMethod]
        public void TestQueryOptions2()
        {
            var q1 = new AddQuery { Left = 1, Right = 2 };
            var q2 = new AddQuery { Left = 1, Right = 2, QueryOptions = QueryOptions.SaveToCache };

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q1)); })
                >=
                m_delayTime);

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q2)); })
                >=
                m_delayTime);  //well, should not hit
        }

        [TestMethod]
        public void TestQueryOptions3()
        {
            var q1 = new AddQuery { Left = 1, Right = 2, QueryOptions = QueryOptions.SaveToCache };
            var q2 = new AddQuery { Left = 1, Right = 2 };

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q1)); })
                >=
                m_delayTime, "Should take longer");

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q2)); })
                <=
                TimeSpan.FromMilliseconds(10), "Should hit cache");  //well, a cache hit
        }

        [TestMethod]
        public void TestQueryOptions4()
        {
            var q1 = new AddQuery { Left = int.MaxValue, Right = int.MaxValue };
            var q2 = new AddQuery { Left = int.MaxValue, Right = int.MaxValue };

            Assert.IsTrue(TryCatchException<OverflowException>(() => TimeCost(async () => { await m_exec.QueryAsync(q1); })));
            Assert.IsTrue(TryCatchException<OverflowException>(() => TimeCost(async () => { await m_exec.QueryAsync(q2); })));

            Assert.AreEqual(2, ((AddAsyncQueryExecutor)m_exec).RealCalcCount);
        }

        [TestMethod]
        public void TestQueryOptions5()
        {
            var q1 = new AddQuery { Left = int.MaxValue, Right = int.MaxValue };

            //use cached failed task
            var q2 = new AddQuery { Left = int.MaxValue, Right = int.MaxValue, QueryOptions = (QueryOptions) (QueryOptions.Default - QueryOptions.ReQueryWhenErrorCached) };

            Assert.IsTrue(TryCatchException<OverflowException>(() => TimeCost(async () => { await m_exec.QueryAsync(q1); })));
            Assert.IsTrue(TryCatchException<OverflowException>(() => TimeCost(async () => { await m_exec.QueryAsync(q2); })));

            Assert.AreEqual(1, ((AddAsyncQueryExecutor)m_exec).RealCalcCount);
        }

        [TestMethod]
        public void TestMemoryCacheQueryCache()
        {
            m_exec = new AddAsyncQueryExecutor(new MemoryCacheBasedQueryCache<AddQuery, AsyncLazy<int>>(new DefaultKeySerializer<AddQuery>(), TimeSpan.FromMinutes(1)), m_delayTime);
            var q1 = new AddQuery { Left = 1, Right = 2 };

            Assert.IsTrue(
                TimeCost(async () => { Assert.AreEqual(3, await m_exec.QueryAsync(q1)); })
                >=
                m_delayTime, "Should take longer");
        }

        [TestMethod]
        public void TestObjectCacheBehavior()
        {
            CacheItemPolicy policy = new CacheItemPolicy() {SlidingExpiration = TimeSpan.FromMinutes(1)};
            Assert.AreEqual(null, MemoryCache.Default.AddOrGetExisting(new CacheItem("test", "one"), policy).Value);
            Assert.AreEqual("test", MemoryCache.Default.AddOrGetExisting(new CacheItem("test", "two"), policy).Key);
            Assert.AreEqual("one", MemoryCache.Default.AddOrGetExisting(new CacheItem("test", "two"), policy).Value);
            Assert.AreEqual("one", MemoryCache.Default.AddOrGetExisting("test", "three", policy));

        }
    }
}
