using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using IdentityMetadataFetcher.Tests.Mocks;
using Microsoft.IdentityModel.Protocols.WsFederation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

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

        private WsFederationMetadataDocument CreateTestMetadata()
        {
            var rawXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<EntityDescriptor xmlns=""urn:oasis:names:tc:SAML:2.0:metadata"" ID=""{Guid.NewGuid()}"">
    <SPSSODescriptor protocolSupportEnumeration=""urn:oasis:names:tc:SAML:2.0:protocol"">
        <SingleLogoutService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" Location=""https://example.com/logout"" />
        <AssertionConsumerService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" Location=""https://example.com/acs"" index=""0"" isDefault=""true"" />
    </SPSSODescriptor>
</EntityDescriptor>";

            using var reader = XmlReader.Create(new StringReader(rawXml));
            var serializer = new WsFederationMetadataSerializer();
            var configuration = serializer.ReadMetadata(reader);
            return new WsFederationMetadataDocument(configuration, rawXml);
        }

        [Test]
        public void IsEmpty_WhenCreated()
        {
            var allEntries = _cache.GetAllEntries().ToList();
            Assert.That(allEntries.Count, Is.EqualTo(0));
        }

        [Test]
        public void CanAddMetadata()
        {
            var metadata = CreateTestMetadata();
            _cache.AddOrUpdateMetadata("issuer-1", metadata, "<metadata />");

            Assert.That(_cache.HasMetadata("issuer-1"), Is.True);
        }

        [Test]
        public void CanRetrieveMetadata()
        {
            var metadata = CreateTestMetadata();
            _cache.AddOrUpdateMetadata("issuer-1", metadata, "<metadata />");

            var retrieved = _cache.GetMetadata("issuer-1");
            Assert.That(retrieved, Is.Not.Null);
        }

        [Test]
        public void CanRetrieveRawMetadata()
        {
            var metadata = CreateTestMetadata();
            var rawXml = "<metadata>Test</metadata>";
            _cache.AddOrUpdateMetadata("issuer-1", metadata, rawXml);

            var retrieved = _cache.GetRawMetadata("issuer-1");
            Assert.That(retrieved, Is.EqualTo(rawXml));
        }

        [Test]
        public void HasMetadata_ReturnsFalseForMissingKey()
        {
            Assert.That(_cache.HasMetadata("missing-issuer"), Is.False);
        }

        [Test]
        public void GetMetadata_ReturnsNullForMissingKey()
        {
            var retrieved = _cache.GetMetadata("missing-issuer");
            Assert.That(retrieved, Is.Null);
        }

        [Test]
        public void CanUpdateExistingMetadata()
        {
            var metadata1 = CreateTestMetadata();
            var metadata2 = CreateTestMetadata();
            
            _cache.AddOrUpdateMetadata("issuer-1", metadata1, "<metadata1 />");
            _cache.AddOrUpdateMetadata("issuer-1", metadata2, "<metadata2 />");

            var raw = _cache.GetRawMetadata("issuer-1");
            Assert.That(raw, Is.EqualTo("<metadata2 />"));
        }

        [Test]
        public void CachedAt_IsSet()
        {
            var before = DateTime.UtcNow;
            var metadata = CreateTestMetadata();
            _cache.AddOrUpdateMetadata("issuer-1", metadata, "<metadata />");
            var after = DateTime.UtcNow;

            var entry = _cache.GetCacheEntry("issuer-1");
            
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.CachedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(entry.CachedAt, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public void CanStoreMultipleIssuers()
        {
            for (int i = 1; i <= 5; i++)
            {
                _cache.AddOrUpdateMetadata($"issuer-{i}", CreateTestMetadata(), $"<metadata{i} />");
            }

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.That(allEntries.Count, Is.EqualTo(5));
        }

        [Test]
        public void CanClear()
        {
            _cache.AddOrUpdateMetadata("issuer-1", CreateTestMetadata(), "<metadata />");
            _cache.Clear();

            Assert.That(_cache.HasMetadata("issuer-1"), Is.False);
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
                        _cache.AddOrUpdateMetadata($"issuer-{threadId}-{i}", CreateTestMetadata(), $"<metadata />");
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.That(allEntries.Count, Is.EqualTo(100));
        }

        [Test]
        public void IsThreadSafe_ConcurrentReads()
        {
            // Add initial data
            _cache.AddOrUpdateMetadata("issuer-1", CreateTestMetadata(), "<metadata />");

            var tasks = new List<Task>();

            // 10 threads each reading 100 times
            for (int t = 0; t < 10; t++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var metadata = _cache.GetMetadata("issuer-1");
                        Assert.That(metadata, Is.Not.Null);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Verify data integrity
            Assert.That(_cache.HasMetadata("issuer-1"), Is.True);
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
                        _cache.AddOrUpdateMetadata($"issuer-w{writerId}-{i}", CreateTestMetadata(), $"<metadata />");
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
            Assert.That(allEntries.Count, Is.EqualTo(30));
        }

        [Test]
        public void GetCacheEntry_ReturnsCompleteEntry()
        {
            var metadata = CreateTestMetadata();
            var rawXml = "<metadata />";
            _cache.AddOrUpdateMetadata("issuer-1", metadata, rawXml);

            var entry = _cache.GetCacheEntry("issuer-1");

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.IssuerId, Is.EqualTo("issuer-1"));
            Assert.That(entry.Metadata, Is.EqualTo(metadata));
            Assert.That(entry.RawXml, Is.EqualTo(rawXml));
        }

        [Test]
        public void GetAllEntries_ReturnsSnapshot()
        {
            _cache.AddOrUpdateMetadata("issuer-1", CreateTestMetadata(), "<metadata1 />");
            _cache.AddOrUpdateMetadata("issuer-2", CreateTestMetadata(), "<metadata2 />");

            var entries = _cache.GetAllEntries().ToList();

            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries.Any(e => e.IssuerId == "issuer-1"), Is.True);
            Assert.That(entries.Any(e => e.IssuerId == "issuer-2"), Is.True);
        }
    }
}
