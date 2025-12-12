using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using IdentityMetadataFetcher.Tests.Mocks;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

namespace IdentityMetadataFetcher.Tests.Services
{
    [TestFixture]
    public class MetadataCacheTests
    {
        private MetadataCache _cache;

        [SetUp]
        public void Setup()
        {
            _cache = new MetadataCache();
        }

        [Test]
        public void IsEmpty_WhenCreated()
        {
            var allEntries = _cache.GetAllEntries().ToList();
            Assert.AreEqual(0, allEntries.Count);
        }

        [Test]
        public void CanAddMetadata()
        {
            var metadata = new MockMetadata();
            _cache.AddOrUpdateMetadata("issuer-1", metadata, "<metadata />");

            Assert.IsTrue(_cache.HasMetadata("issuer-1"));
        }

        [Test]
        public void CanRetrieveMetadata()
        {
            var metadata = new MockMetadata();
            _cache.AddOrUpdateMetadata("issuer-1", metadata, "<metadata />");

            var retrieved = _cache.GetMetadata("issuer-1");
            Assert.IsNotNull(retrieved);
        }

        [Test]
        public void CanRetrieveRawMetadata()
        {
            var metadata = new MockMetadata();
            var rawXml = "<metadata>Test</metadata>";
            _cache.AddOrUpdateMetadata("issuer-1", metadata, rawXml);

            var retrieved = _cache.GetRawMetadata("issuer-1");
            Assert.AreEqual(rawXml, retrieved);
        }

        [Test]
        public void HasMetadata_ReturnsFalseForMissingKey()
        {
            Assert.IsFalse(_cache.HasMetadata("missing-issuer"));
        }

        [Test]
        public void GetMetadata_ReturnsNullForMissingKey()
        {
            var retrieved = _cache.GetMetadata("missing-issuer");
            Assert.IsNull(retrieved);
        }

        [Test]
        public void CanUpdateExistingMetadata()
        {
            var metadata1 = new MockMetadata();
            var metadata2 = new MockMetadata();
            
            _cache.AddOrUpdateMetadata("issuer-1", metadata1, "<metadata1 />");
            _cache.AddOrUpdateMetadata("issuer-1", metadata2, "<metadata2 />");

            var raw = _cache.GetRawMetadata("issuer-1");
            Assert.AreEqual("<metadata2 />", raw);
        }

        [Test]
        public void CachedAt_IsSet()
        {
            var before = DateTime.UtcNow;
            var metadata = new MockMetadata();
            _cache.AddOrUpdateMetadata("issuer-1", metadata, "<metadata />");
            var after = DateTime.UtcNow;

            var entry = _cache.GetCacheEntry("issuer-1");
            
            Assert.IsNotNull(entry);
            Assert.GreaterOrEqual(entry.CachedAt, before);
            Assert.LessOrEqual(entry.CachedAt, after);
        }

        [Test]
        public void CanStoreMultipleIssuers()
        {
            for (int i = 1; i <= 5; i++)
            {
                _cache.AddOrUpdateMetadata($"issuer-{i}", new MockMetadata(), $"<metadata{i} />");
            }

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.AreEqual(5, allEntries.Count);
        }

        [Test]
        public void CanClear()
        {
            _cache.AddOrUpdateMetadata("issuer-1", new MockMetadata(), "<metadata />");
            _cache.Clear();

            Assert.IsFalse(_cache.HasMetadata("issuer-1"));
        }

        [Test]
        public void IsThreadSafe_ConcurrentWrites()
        {
            var tasks = new List<Task>();

            // 10 threads each adding 10 entries
            for (int t = 0; t < 10; t++)
            {
                int threadId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        _cache.AddOrUpdateMetadata($"issuer-{threadId}-{i}", new MockMetadata(), $"<metadata />");
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.AreEqual(100, allEntries.Count);
        }

        [Test]
        public void IsThreadSafe_ConcurrentReads()
        {
            // Add initial data
            _cache.AddOrUpdateMetadata("issuer-1", new MockMetadata(), "<metadata />");

            var tasks = new List<Task>();

            // 10 threads each reading 100 times
            for (int t = 0; t < 10; t++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var metadata = _cache.GetMetadata("issuer-1");
                        Assert.IsNotNull(metadata);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Verify data integrity
            Assert.IsTrue(_cache.HasMetadata("issuer-1"));
        }

        [Test]
        public void IsThreadSafe_MixedReadWrite()
        {
            var tasks = new List<Task>();

            // Writers
            for (int w = 0; w < 3; w++)
            {
                int writerId = w;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        _cache.AddOrUpdateMetadata($"issuer-w{writerId}-{i}", new MockMetadata(), $"<metadata />");
                    }
                }));
            }

            // Readers
            for (int r = 0; r < 3; r++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < 20; i++)
                    {
                        var entries = _cache.GetAllEntries().ToList();
                        System.Threading.Thread.Sleep(1); // Brief delay
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.AreEqual(30, allEntries.Count);
        }

        [Test]
        public void GetCacheEntry_ReturnsCompleteEntry()
        {
            var metadata = new MockMetadata();
            var rawXml = "<metadata />";
            _cache.AddOrUpdateMetadata("issuer-1", metadata, rawXml);

            var entry = _cache.GetCacheEntry("issuer-1");

            Assert.IsNotNull(entry);
            Assert.AreEqual("issuer-1", entry.IssuerId);
            Assert.AreEqual(metadata, entry.Metadata);
            Assert.AreEqual(rawXml, entry.RawXml);
        }

        [Test]
        public void GetAllEntries_ReturnsSnapshot()
        {
            _cache.AddOrUpdateMetadata("issuer-1", new MockMetadata(), "<metadata1 />");
            _cache.AddOrUpdateMetadata("issuer-2", new MockMetadata(), "<metadata2 />");

            var entries = _cache.GetAllEntries().ToList();

            Assert.AreEqual(2, entries.Count);
            Assert.IsTrue(entries.Any(e => e.IssuerId == "issuer-1"));
            Assert.IsTrue(entries.Any(e => e.IssuerId == "issuer-2"));
        }
    }
}
