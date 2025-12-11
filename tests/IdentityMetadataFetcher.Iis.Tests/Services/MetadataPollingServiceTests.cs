using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Services;
using IdentityMetadataFetcher.Iis.Tests.Mocks;
using IdentityMetadataFetcher.Models;

namespace IdentityMetadataFetcher.Iis.Tests.Services
{
    [TestFixture]
    public class MetadataPollingServiceTests
    {
        private MetadataPollingService _service;
        private MetadataCache _cache;
        private MockMetadataFetcher _fetcher;
        private List<IssuerEndpoint> _endpoints;

        [SetUp]
        public void Setup()
        {
            _cache = new MetadataCache();
            _fetcher = new MockMetadataFetcher();
            
            _endpoints = new List<IssuerEndpoint>
            {
                new IssuerEndpoint { Id = "issuer-1", Endpoint = "https://example1.com/metadata", Name = "Example 1", MetadataType = MetadataType.SAML },
                new IssuerEndpoint { Id = "issuer-2", Endpoint = "https://example2.com/metadata", Name = "Example 2", MetadataType = MetadataType.WSFED }
            };

            _service = new MetadataPollingService(_fetcher, _cache, _endpoints, pollingIntervalMinutes: 60);
        }

        [TearDown]
        public void Teardown()
        {
            _service?.Stop();
        }

        [Test]
        public void CanBeCreated()
        {
            Assert.IsNotNull(_service);
        }

        [Test]
        public async Task PollNowAsync_UpdatesCache()
        {
            await _service.PollNowAsync();

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.GreaterOrEqual(allEntries.Count, 1);
        }

        [Test]
        public async Task PollNowAsync_RaisesPollingStartedEvent()
        {
            var eventRaised = false;
            _service.PollingStarted += (sender, e) => eventRaised = true;

            await _service.PollNowAsync();

            Assert.IsTrue(eventRaised);
        }

        [Test]
        public async Task PollNowAsync_RaisesPollingCompletedEvent()
        {
            var eventRaised = false;
            _service.PollingCompleted += (sender, e) => eventRaised = true;

            await _service.PollNowAsync();

            Assert.IsTrue(eventRaised);
        }

        [Test]
        public async Task PollNowAsync_RaisesMetadataUpdatedEvent()
        {
            var eventsRaised = new List<string>();
            _service.MetadataUpdated += (sender, e) => eventsRaised.Add(e.IssuerId);

            await _service.PollNowAsync();

            Assert.Greater(eventsRaised.Count, 0);
        }

        [Test]
        public async Task PollNowAsync_RaisesPollingErrorEventOnFailure()
        {
            _fetcher.SetFailure("issuer-1", "Network error");

            var errorEventRaised = false;
            _service.PollingError += (sender, e) => errorEventRaised = true;

            await _service.PollNowAsync();

            Assert.IsTrue(errorEventRaised);
        }

        [Test]
        public async Task PollNowAsync_ContinuesOnErrorForOtherEndpoints()
        {
            _fetcher.SetFailure("issuer-1", "Network error");

            await _service.PollNowAsync();

            // issuer-2 should still be cached despite issuer-1 failure
            Assert.IsTrue(_cache.HasMetadata("issuer-2"));
        }

        [Test]
        public async Task PollNowAsync_PollingCompletedEventIncludesSummary()
        {
            PollingEventArgs eventArgs = null;
            _service.PollingCompleted += (sender, e) => eventArgs = e;

            await _service.PollNowAsync();

            Assert.IsNotNull(eventArgs);
            Assert.Greater(eventArgs.SuccessCount, 0);
        }

        [Test]
        public void Start_StartsPolling()
        {
            _service.Start();

            // Small delay to allow timer to fire
            System.Threading.Thread.Sleep(100);

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.Greater(allEntries.Count, 0);

            _service.Stop();
        }

        [Test]
        public void Stop_StopsPolling()
        {
            _service.Start();
            System.Threading.Thread.Sleep(100);

            _service.Stop();
            var countAfterStop = _cache.GetAllEntries().Count();

            System.Threading.Thread.Sleep(100);
            var countAfterWait = _cache.GetAllEntries().Count();

            Assert.AreEqual(countAfterStop, countAfterWait);
        }

        [Test]
        public async Task PollNowAsync_PreventsConcurrentPolling()
        {
            var pollCount = 0;
            _service.PollingStarted += (sender, e) => pollCount++;

            // Try to poll concurrently
            var task1 = _service.PollNowAsync();
            var task2 = _service.PollNowAsync();

            await Task.WhenAll(task1, task2);

            // Should only increment once due to concurrent poll prevention
            Assert.AreEqual(1, pollCount);
        }

        [Test]
        public async Task PollNowAsync_SupportsMultipleEndpoints()
        {
            await _service.PollNowAsync();

            Assert.IsTrue(_cache.HasMetadata("issuer-1"));
            Assert.IsTrue(_cache.HasMetadata("issuer-2"));
        }

        [Test]
        public async Task PollNowAsync_StoresRawMetadata()
        {
            await _service.PollNowAsync();

            var rawXml = _cache.GetRawMetadata("issuer-1");
            Assert.IsNotEmpty(rawXml);
        }

        [Test]
        public async Task PollingCompletedEvent_ProvidesTotalAndSuccessCount()
        {
            PollingEventArgs eventArgs = null;
            _service.PollingCompleted += (sender, e) => eventArgs = e;

            await _service.PollNowAsync();

            Assert.IsNotNull(eventArgs);
            Assert.AreEqual(_endpoints.Count, eventArgs.TotalCount);
            Assert.Greater(eventArgs.SuccessCount, 0);
        }

        [Test]
        public async Task PollingErrorEvent_IncludesErrorDetails()
        {
            _fetcher.SetFailure("issuer-1", "Timeout exception");

            PollingErrorEventArgs errorArgs = null;
            _service.PollingError += (sender, e) => errorArgs = e;

            await _service.PollNowAsync();

            Assert.IsNotNull(errorArgs);
            Assert.AreEqual("issuer-1", errorArgs.IssuerId);
            Assert.IsNotEmpty(errorArgs.ErrorMessage);
        }

        [Test]
        public async Task MetadataUpdatedEvent_IncludesTimestamp()
        {
            MetadataUpdatedEventArgs eventArgs = null;
            _service.MetadataUpdated += (sender, e) => eventArgs = e;

            var before = DateTime.UtcNow;
            await _service.PollNowAsync();
            var after = DateTime.UtcNow;

            Assert.IsNotNull(eventArgs);
            Assert.GreaterOrEqual(eventArgs.UpdatedAt, before);
            Assert.LessOrEqual(eventArgs.UpdatedAt, after);
        }
    }
}
