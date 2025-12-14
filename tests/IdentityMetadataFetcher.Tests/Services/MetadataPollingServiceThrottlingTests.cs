using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using IdentityMetadataFetcher.Tests.Mocks;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityMetadataFetcher.Tests.Services
{
    [TestFixture]
    public class MetadataPollingServiceThrottlingTests
    {
        private MetadataCache _cache;
        private MockMetadataFetcher _mockFetcher;
        private List<IssuerEndpoint> _endpoints;

        [SetUp]
        public void Setup()
        {
            _cache = new MetadataCache();
            _mockFetcher = new MockMetadataFetcher();
            
            _endpoints = new List<IssuerEndpoint>
            {
                new IssuerEndpoint("issuer1", "https://issuer1.example.com/metadata", "Issuer 1"),
                new IssuerEndpoint("issuer2", "https://issuer2.example.com/metadata", "Issuer 2")
            };
        }

        [Test]
        public void Constructor_WithMinimumPollInterval_CreatesService()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            Assert.That(service, Is.Not.Null);
            service.Dispose();
        }

        [Test]
        public void ShouldPollIssuer_WithNoThrottling_AlwaysReturnsTrue()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 0);
            
            Assert.That(service.ShouldPollIssuer("issuer1"), Is.True);
            Assert.That(service.ShouldPollIssuer("issuer1"), Is.True);
            Assert.That(service.ShouldPollIssuer("issuer1"), Is.True);
            
            service.Dispose();
        }

        [Test]
        public void ShouldPollIssuer_NeverPolled_ReturnsTrue()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            var result = service.ShouldPollIssuer("issuer1");
            
            Assert.That(result, Is.True);
            service.Dispose();
        }

        [Test]
        public async Task ShouldPollIssuer_RecentlyPolled_ReturnsFalse()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            // Poll issuer1
            await service.PollIssuerNowAsync("issuer1");
            
            // Immediately check if should poll again
            var result = service.ShouldPollIssuer("issuer1");
            
            Assert.That(result, Is.False);
            service.Dispose();
        }

        [Test]
        public async Task ShouldPollIssuer_AfterInterval_ReturnsTrue()
        {
            // Use 0 minute interval for testing
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 0);
            
            // Poll issuer1
            await service.PollIssuerNowAsync("issuer1");
            
            // Wait a tiny bit
            await Task.Delay(10);
            
            // Check if should poll again (with 0 minute interval, should be true)
            var result = service.ShouldPollIssuer("issuer1");
            
            Assert.That(result, Is.True);
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_FirstTime_ReturnsTrue()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            var result = await service.PollIssuerNowAsync("issuer1");
            
            Assert.That(result, Is.True);
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_WithinThrottleWindow_ReturnsFalse()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            // First poll
            var result1 = await service.PollIssuerNowAsync("issuer1");
            Assert.That(result1, Is.True);
            
            // Second poll immediately
            var result2 = await service.PollIssuerNowAsync("issuer1");
            Assert.That(result2, Is.False); // Should be throttled
            
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_DifferentIssuers_BothSucceed()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            // Poll issuer1
            var result1 = await service.PollIssuerNowAsync("issuer1");
            Assert.That(result1, Is.True);
            
            // Poll issuer2 (different issuer, should succeed)
            var result2 = await service.PollIssuerNowAsync("issuer2");
            Assert.That(result2, Is.True);
            
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_WithNullIssuerId_ThrowsArgumentNullException()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await service.PollIssuerNowAsync(null));
            
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_WithEmptyIssuerId_ThrowsArgumentNullException()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await service.PollIssuerNowAsync(""));
            
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_WithUnknownIssuerId_ReturnsFalse()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            var result = await service.PollIssuerNowAsync("unknown-issuer");
            
            Assert.That(result, Is.False);
            service.Dispose();
        }

        [Test]
        public async Task GetLastPollTimestamp_NeverPolled_ReturnsNull()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            var timestamp = service.GetLastPollTimestamp("issuer1");
            
            Assert.That(timestamp, Is.Null);
            service.Dispose();
        }

        [Test]
        public async Task GetLastPollTimestamp_AfterPoll_ReturnsTimestamp()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            var before = DateTime.UtcNow;
            await service.PollIssuerNowAsync("issuer1");
            var after = DateTime.UtcNow;
            
            var timestamp = service.GetLastPollTimestamp("issuer1");
            
            Assert.That(timestamp, Is.Not.Null);
            Assert.That(timestamp.Value, Is.GreaterThanOrEqualTo(before));
            Assert.That(timestamp.Value, Is.LessThanOrEqualTo(after));
            
            service.Dispose();
        }

        [Test]
        public async Task GetLastGlobalPollTimestamp_NeverPolled_ReturnsNull()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            var timestamp = service.GetLastGlobalPollTimestamp();
            
            Assert.That(timestamp, Is.Null);
            service.Dispose();
        }

        [Test]
        public async Task GetLastGlobalPollTimestamp_AfterPollNow_ReturnsTimestamp()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            var before = DateTime.UtcNow;
            await service.PollNowAsync();
            var after = DateTime.UtcNow;
            
            var timestamp = service.GetLastGlobalPollTimestamp();
            
            Assert.That(timestamp, Is.Not.Null);
            Assert.That(timestamp.Value, Is.GreaterThanOrEqualTo(before));
            Assert.That(timestamp.Value, Is.LessThanOrEqualTo(after));
            
            service.Dispose();
        }

        [Test]
        public async Task PollNowAsync_UpdatesAllIssuerTimestamps()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            await service.PollNowAsync();
            
            var timestamp1 = service.GetLastPollTimestamp("issuer1");
            var timestamp2 = service.GetLastPollTimestamp("issuer2");
            
            Assert.That(timestamp1, Is.Not.Null);
            Assert.That(timestamp2, Is.Not.Null);
            
            service.Dispose();
        }

        [Test]
        public async Task Throttling_IsPerIssuer_NotGlobal()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            // Poll issuer1
            await service.PollIssuerNowAsync("issuer1");
            
            // issuer1 should be throttled
            Assert.That(service.ShouldPollIssuer("issuer1"), Is.False);
            
            // issuer2 should NOT be throttled
            Assert.That(service.ShouldPollIssuer("issuer2"), Is.True);
            
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_UpdatesCache()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            // Poll issuer1
            await service.PollIssuerNowAsync("issuer1");
            
            // Check cache
            var cached = _cache.GetCacheEntry("issuer1");
            Assert.That(cached, Is.Not.Null);
            Assert.That(cached.Metadata, Is.Not.Null);
            
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_WithFailure_StillUpdatesTimestamp()
        {
            _mockFetcher.SetFailure("issuer1", "Network error");
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 5);
            
            // Poll issuer1 (will fail)
            await service.PollIssuerNowAsync("issuer1");
            
            // Timestamp should still be updated (to prevent retrying immediately)
            var timestamp = service.GetLastPollTimestamp("issuer1");
            Assert.That(timestamp, Is.Not.Null);
            
            service.Dispose();
        }

        [Test]
        public async Task ShouldPollIssuer_ThreadSafe_ConcurrentAccess()
        {
            var service = new MetadataPollingService(_mockFetcher, _cache, _endpoints, 60, 0);
            
            // Poll issuer1
            await service.PollIssuerNowAsync("issuer1");
            
            // Check ShouldPoll from multiple threads
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => service.ShouldPollIssuer("issuer1")))
                .ToArray();
            
            Assert.DoesNotThrow(() => Task.WaitAll(tasks));
            
            service.Dispose();
        }

        [Test]
        public async Task PollIssuerNowAsync_WithMissingEndpoint_ReturnsFalse()
        {
            var cache = new MetadataCache();
            var fetcher = new MockMetadataFetcher();
            var endpoints = new List<IssuerEndpoint>
            {
                new IssuerEndpoint("issuer1", "https://issuer1.example.com/metadata", "Issuer 1"),
            };
            var service = new MetadataPollingService(fetcher, cache, endpoints, 60, 5);

            var result = await service.PollIssuerNowAsync("non-existent");
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task PollNowAsync_ConcurrentCalls_TriggersSinglePoll()
        {
            var cache = new MetadataCache();
            var fetcher = new MockMetadataFetcher();
            var endpoints = new List<IssuerEndpoint>
            {
                new IssuerEndpoint("issuer1", "https://issuer1.example.com/metadata", "Issuer 1"),
                new IssuerEndpoint("issuer2", "https://issuer2.example.com/metadata", "Issuer 2")
            };
            var service = new MetadataPollingService(fetcher, cache, endpoints, 60, 5);

            var started = 0;
            service.PollingStarted += (s, e) => started++;

            // Launch concurrent polls
            var t1 = service.PollNowAsync();
            var t2 = service.PollNowAsync();
            await Task.WhenAll(t1, t2);

            Assert.That(started, Is.EqualTo(1));
        }
    }
}
