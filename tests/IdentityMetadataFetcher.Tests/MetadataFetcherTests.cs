using NUnit.Framework;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using IdentityMetadataFetcher.Exceptions;
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
            Assert.IsNotNull(fetcher);
        }

        [Test]
        public void Constructor_WithOptions_UsesProvidedOptions()
        {
            var options = new MetadataFetchOptions { DefaultTimeoutMs = 15000 };
            var fetcher = new MetadataFetcher(options);
            Assert.IsNotNull(fetcher);
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

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Exception);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.AreEqual(endpoint, result.Endpoint);
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

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Exception);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.AreEqual(endpoint, result.Endpoint);
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

            Assert.AreEqual(2, results.Count);
            Assert.IsFalse(results[0].IsSuccess);
            Assert.IsFalse(results[1].IsSuccess);
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

            Assert.AreEqual(2, results.Count);
            Assert.IsFalse(results[0].IsSuccess);
            Assert.IsFalse(results[1].IsSuccess);
        }

        [Test]
        public void FetchMetadataFromMultipleEndpoints_WithEmptyList_ReturnsEmptyCollection()
        {
            var results = _fetcher.FetchMetadataFromMultipleEndpoints(new List<IssuerEndpoint>()).ToList();
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public async Task FetchMetadataFromMultipleEndpointsAsync_WithEmptyList_ReturnsEmptyCollection()
        {
            var results = (await _fetcher.FetchMetadataFromMultipleEndpointsAsync(new List<IssuerEndpoint>())).ToList();
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void MetadataFetchResult_DefaultValues()
        {
            var result = new MetadataFetchResult();
            Assert.IsTrue(result.FetchedAt <= DateTime.UtcNow);
            Assert.IsTrue(result.FetchedAt >= DateTime.UtcNow.AddSeconds(-1));
        }

        [Test]
        public void IssuerEndpoint_ConstructorWithParameters()
        {
            var endpoint = new IssuerEndpoint("id1", "http://example.com/metadata", "Example", MetadataType.SAML);
            Assert.AreEqual("id1", endpoint.Id);
            Assert.AreEqual("http://example.com/metadata", endpoint.Endpoint);
            Assert.AreEqual("Example", endpoint.Name);
            Assert.AreEqual(MetadataType.SAML, endpoint.MetadataType);
        }

        [Test]
        public void MetadataFetchOptions_DefaultValues()
        {
            var options = new MetadataFetchOptions();
            Assert.AreEqual(30000, options.DefaultTimeoutMs);
            Assert.IsTrue(options.ContinueOnError);
            Assert.IsTrue(options.ValidateServerCertificate);
            Assert.AreEqual(0, options.MaxRetries);
            Assert.IsFalse(options.CacheMetadata);
            Assert.AreEqual(60, options.CacheDurationMinutes);
        }
    }
}
