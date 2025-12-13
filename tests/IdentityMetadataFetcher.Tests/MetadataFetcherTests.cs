using IdentityMetadataFetcher.Exceptions;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityMetadataFetcher.Tests
{
    [TestFixture]
    public class MetadataFetcherTests
    {
        private MetadataFetcher _fetcher;
        private MetadataFetchOptions _options;

        [SetUp]
        public void Setup()
        {
            _options = new MetadataFetchOptions
            {
                DefaultTimeoutMs = 30000,
                ContinueOnError = true,
                ValidateServerCertificate = false,
                MaxRetries = 1
            };
            _fetcher = new MetadataFetcher(_options);
        }

        [Test]
        public void Constructor_WithoutOptions_CreatesDefaultOptions()
        {
            var fetcher = new MetadataFetcher();
            Assert.That(fetcher, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithOptions_UsesProvidedOptions()
        {
            var options = new MetadataFetchOptions { DefaultTimeoutMs = 15000 };
            var fetcher = new MetadataFetcher(options);
            Assert.That(fetcher, Is.Not.Null);
        }

        [Test]
        public void FetchMetadata_WithNullEndpoint_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _fetcher.FetchMetadata(null));
        }

        [Test]
        public void FetchMetadata_WithEmptyEndpointUrl_ThrowsArgumentException()
        {
            var endpoint = new IssuerEndpoint { Id = "test", Endpoint = "", MetadataType = MetadataType.SAML };
            Assert.Throws<ArgumentException>(() => _fetcher.FetchMetadata(endpoint));
        }

        [Test]
        public void FetchMetadata_WithInvalidUrl_ReturnsFailureResult()
        {
            var endpoint = new IssuerEndpoint
            {
                Id = "test-invalid",
                Endpoint = "http://invalid-endpoint-that-does-not-exist-12345.example.com/metadata",
                Name = "Invalid Endpoint",
                MetadataType = MetadataType.SAML
            };

            var result = _fetcher.FetchMetadata(endpoint);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Exception, Is.Not.Null);
            Assert.That(result.ErrorMessage, Is.Not.Null);
            Assert.That(result.Endpoint, Is.EqualTo(endpoint));
        }

        [Test]
        public async Task FetchMetadataAsync_WithNullEndpoint_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () => await _fetcher.FetchMetadataAsync(null));
        }

        [Test]
        public async Task FetchMetadataAsync_WithEmptyEndpointUrl_ThrowsArgumentException()
        {
            var endpoint = new IssuerEndpoint { Id = "test", Endpoint = "", MetadataType = MetadataType.SAML };
            Assert.ThrowsAsync<ArgumentException>(async () => await _fetcher.FetchMetadataAsync(endpoint));
        }

        [Test]
        public async Task FetchMetadataAsync_WithInvalidUrl_ReturnsFailureResult()
        {
            var endpoint = new IssuerEndpoint
            {
                Id = "test-invalid",
                Endpoint = "http://invalid-endpoint-that-does-not-exist-12345.example.com/metadata",
                Name = "Invalid Endpoint",
                MetadataType = MetadataType.SAML
            };

            var result = await _fetcher.FetchMetadataAsync(endpoint);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Exception, Is.Not.Null);
            Assert.That(result.ErrorMessage, Is.Not.Null);
            Assert.That(result.Endpoint, Is.EqualTo(endpoint));
        }

        [Test]
        public void FetchMetadataFromMultipleEndpoints_WithNullList_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _fetcher.FetchMetadataFromMultipleEndpoints(null));
        }

        [Test]
        public void FetchMetadataFromMultipleEndpoints_WithMultipleInvalidEndpoints_ReturnsResultsForAll()
        {
            var endpoints = new[]
            {
                new IssuerEndpoint { Id = "ep1", Endpoint = "http://invalid1.example.com/metadata", Name = "Invalid 1", MetadataType = MetadataType.SAML },
                new IssuerEndpoint { Id = "ep2", Endpoint = "http://invalid2.example.com/metadata", Name = "Invalid 2", MetadataType = MetadataType.WSFED }
            };

            var results = _fetcher.FetchMetadataFromMultipleEndpoints(endpoints).ToList();

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].IsSuccess, Is.False);
            Assert.That(results[1].IsSuccess, Is.False);
        }

        [Test]
        public async Task FetchMetadataFromMultipleEndpointsAsync_WithNullList_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () => await _fetcher.FetchMetadataFromMultipleEndpointsAsync(null));
        }

        [Test]
        public async Task FetchMetadataFromMultipleEndpointsAsync_WithMultipleInvalidEndpoints_ReturnsResultsForAll()
        {
            var endpoints = new[]
            {
                new IssuerEndpoint { Id = "ep1", Endpoint = "http://invalid1.example.com/metadata", Name = "Invalid 1", MetadataType = MetadataType.SAML },
                new IssuerEndpoint { Id = "ep2", Endpoint = "http://invalid2.example.com/metadata", Name = "Invalid 2", MetadataType = MetadataType.WSFED }
            };

            var results = (await _fetcher.FetchMetadataFromMultipleEndpointsAsync(endpoints)).ToList();

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].IsSuccess, Is.False);
            Assert.That(results[1].IsSuccess, Is.False);
        }

        [Test]
        public void FetchMetadataFromMultipleEndpoints_WithEmptyList_ReturnsEmptyCollection()
        {
            var results = _fetcher.FetchMetadataFromMultipleEndpoints(new List<IssuerEndpoint>()).ToList();
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task FetchMetadataFromMultipleEndpointsAsync_WithEmptyList_ReturnsEmptyCollection()
        {
            var results = (await _fetcher.FetchMetadataFromMultipleEndpointsAsync(new List<IssuerEndpoint>())).ToList();
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public void MetadataFetchResult_DefaultValues()
        {
            var result = new MetadataFetchResult();
            Assert.That(result.FetchedAt, Is.LessThanOrEqualTo(DateTime.UtcNow));
            Assert.That(result.FetchedAt, Is.GreaterThanOrEqualTo(DateTime.UtcNow.AddSeconds(-1)));
        }

        [Test]
        public void IssuerEndpoint_ConstructorWithParameters()
        {
            var endpoint = new IssuerEndpoint("id1", "http://example.com/metadata", "Example", MetadataType.SAML);
            Assert.That(endpoint.Id, Is.EqualTo("id1"));
            Assert.That(endpoint.Endpoint, Is.EqualTo("http://example.com/metadata"));
            Assert.That(endpoint.Name, Is.EqualTo("Example"));
            Assert.That(endpoint.MetadataType, Is.EqualTo(MetadataType.SAML));
        }

        [Test]
        public void MetadataFetchOptions_DefaultValues()
        {
            var options = new MetadataFetchOptions();
            Assert.That(options.DefaultTimeoutMs, Is.EqualTo(30000));
            Assert.That(options.ContinueOnError, Is.True);
            Assert.That(options.ValidateServerCertificate, Is.True);
            Assert.That(options.MaxRetries, Is.EqualTo(0));
            Assert.That(options.CacheMetadata, Is.False);
            Assert.That(options.CacheDurationMinutes, Is.EqualTo(60));
        }
    }
}
