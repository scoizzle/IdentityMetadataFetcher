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
            Assert.That(_service, Is.Not.Null);
        }

        [Test]
        public async Task PollNowAsync_UpdatesCache()
        {
            await _service.PollNowAsync();

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.That(allEntries.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task PollNowAsync_RaisesPollingStartedEvent()
        {
            var eventRaised = false;
            _service.PollingStarted += (sender, e) => eventRaised = true;

            await _service.PollNowAsync();

            Assert.That(eventRaised, Is.True);
        }

        [Test]
        public async Task PollNowAsync_RaisesPollingCompletedEvent()
        {
            var eventRaised = false;
            _service.PollingCompleted += (sender, e) => eventRaised = true;

            await _service.PollNowAsync();

            Assert.That(eventRaised, Is.True);
        }

        [Test]
        public async Task PollNowAsync_RaisesMetadataUpdatedEvent()
        {
            var eventsRaised = new List<string>();
            _service.MetadataUpdated += (sender, e) => eventsRaised.Add(e.IssuerId);

            await _service.PollNowAsync();

            Assert.That(eventsRaised.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task PollNowAsync_RaisesPollingErrorEventOnFailure()
        {
            _fetcher.SetFailure("issuer-1", "Network error");

            var errorEventRaised = false;
            _service.PollingError += (sender, e) => errorEventRaised = true;

            await _service.PollNowAsync();

            Assert.That(errorEventRaised, Is.True);
        }

        [Test]
        public async Task PollNowAsync_ContinuesOnErrorForOtherEndpoints()
        {
            _fetcher.SetFailure("issuer-1", "Network error");

            await _service.PollNowAsync();

            // issuer-2 should still be cached despite issuer-1 failure
            Assert.That(_cache.HasMetadata("issuer-2"), Is.True);
        }

        [Test]
        public async Task PollNowAsync_PollingCompletedEventIncludesSummary()
        {
            PollingEventArgs eventArgs = null;
            _service.PollingCompleted += (sender, e) => eventArgs = e;

            await _service.PollNowAsync();

            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.SuccessCount, Is.GreaterThan(0));
        }

        [Test]
        public void Start_StartsPolling()
        {
            _service.Start();

            // Small delay to allow timer to fire
            System.Threading.Thread.Sleep(100);

            var allEntries = _cache.GetAllEntries().ToList();
            Assert.That(allEntries.Count, Is.GreaterThan(0));

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

            Assert.That(countAfterStop, Is.EqualTo(countAfterWait));
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
            Assert.That(pollCount, Is.EqualTo(1));
        }

        [Test]
        public async Task PollNowAsync_SupportsMultipleEndpoints()
        {
            await _service.PollNowAsync();

            Assert.That(_cache.HasMetadata("issuer-1"), Is.True);
            Assert.That(_cache.HasMetadata("issuer-2"), Is.True);
        }

        [Test]
        public async Task PollNowAsync_StoresRawMetadata()
        {
            await _service.PollNowAsync();

            var rawXml = _cache.GetRawMetadata("issuer-1");
            Assert.That(rawXml, Is.Not.Empty);
        }

        [Test]
        public async Task PollingCompletedEvent_ProvidesTotalAndSuccessCount()
        {
            PollingEventArgs eventArgs = null;
            _service.PollingCompleted += (sender, e) => eventArgs = e;

            await _service.PollNowAsync();

            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.TotalCount, Is.EqualTo(_endpoints.Count));
            Assert.That(eventArgs.SuccessCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task PollingErrorEvent_IncludesErrorDetails()
        {
            _fetcher.SetFailure("issuer-1", "Timeout exception");

            PollingErrorEventArgs errorArgs = null;
            _service.PollingError += (sender, e) => errorArgs = e;

            await _service.PollNowAsync();

            Assert.That(errorArgs, Is.Not.Null);
            Assert.That(errorArgs.IssuerId, Is.EqualTo("issuer-1"));
            Assert.That(errorArgs.ErrorMessage, Is.Not.Empty);
        }

        [Test]
        public async Task MetadataUpdatedEvent_IncludesTimestamp()
        {
            MetadataUpdatedEventArgs eventArgs = null;
            _service.MetadataUpdated += (sender, e) => eventArgs = e;

            var before = DateTime.UtcNow;
            await _service.PollNowAsync();
            var after = DateTime.UtcNow;

            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.UpdatedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(eventArgs.UpdatedAt, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public void Start_TriggersSingleInitialPoll_AndIsIdempotent()
        {
            var cache = new MetadataCache();
            var fetcher = new MockMetadataFetcher();
            var endpoints = new List<IssuerEndpoint>
            {
                new IssuerEndpoint { Id = "issuer-1", Endpoint = "https://example1.com/metadata", Name = "Example 1", MetadataType = MetadataType.SAML },
            };
            var service = new MetadataPollingService(fetcher, cache, endpoints, pollingIntervalMinutes: 60);

            var startedCount = 0;
            service.PollingStarted += (s, e) => startedCount++;

            service.Start();
            // Allow the initial poll to run
            System.Threading.Thread.Sleep(50);

            // Subsequent Start() calls should be ignored
            service.Start();
            service.Start();
            System.Threading.Thread.Sleep(50);

            Assert.That(startedCount, Is.EqualTo(1), "Start should trigger only a single initial poll and be idempotent.");

            service.Stop();
        }
    }
}
