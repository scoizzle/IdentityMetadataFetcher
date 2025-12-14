using IdentityMetadataFetcher.Iis.Services;
using IdentityMetadataFetcher.Iis.Tests.Mocks; // Use IIS test mocks
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using Microsoft.IdentityModel.Protocols.WsFederation;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityMetadataFetcher.Iis.Tests.Services
{
    [TestFixture]
    public class AuthenticationFailureRecoveryServiceAdditionalTests
    {
        private MetadataCache _cache;
        private IdentityModelConfigurationUpdater _updater;
        private MockMetadataFetcher _fetcher;
        private MetadataPollingService _pollingService;
        private AuthenticationFailureRecoveryService _recoveryService;
        private List<IssuerEndpoint> _endpoints;

        [SetUp]
        public void Setup()
        {
            _cache = new MetadataCache();
            _updater = new IdentityModelConfigurationUpdater();
            _fetcher = new MockMetadataFetcher();
            _endpoints = new List<IssuerEndpoint>
            {
                new IssuerEndpoint("issuer1", "https://issuer1.example.com/metadata", "Issuer 1"),
                new IssuerEndpoint("issuer2", "https://issuer2.example.com/metadata", "Issuer 2")
            };
            _pollingService = new MetadataPollingService(_fetcher, _cache, _endpoints, 60, 5);
            _recoveryService = new AuthenticationFailureRecoveryService(_pollingService, _cache, _updater);
        }

        [TearDown]
        public void Teardown()
        {
            _pollingService.Dispose();
        }

        [Test]
        public async Task TryRecoverFromAuthenticationFailureAsync_WithIssuerMatch_PrefersMatchingEndpoint()
        {
            // Pre-populate cache with issuer for issuer1 - must match what MockMetadata will provide
            var config = new WsFederationConfiguration { Issuer = "https://example.com/entity" };
            _cache.AddOrUpdateMetadata("issuer1", new WsFederationMetadataDocument(config, "<xml />"), "<xml />");

            var ex = new SecurityTokenValidationException(
                "Issuer 'https://example.com/entity' signature key not found");

            var result = await _recoveryService.TryRecoverFromAuthenticationFailureAsync(ex, _endpoints);
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task TryRecoverFromAuthenticationFailureAsync_PollThrottled_ReturnsFalse()
        {
            // Pre-populate cache with issuer matching MockMetadata
            var config = new WsFederationConfiguration { Issuer = "https://example.com/entity" };
            _cache.AddOrUpdateMetadata("issuer1", new WsFederationMetadataDocument(config, "<xml />"), "<xml />");
            _cache.AddOrUpdateMetadata("issuer2", new WsFederationMetadataDocument(config, "<xml />"), "<xml />");

            var ex = new SecurityTokenValidationException(
                "Issuer 'https://example.com/entity' signature key not found");

            // First recovers
            var first = await _recoveryService.TryRecoverFromAuthenticationFailureAsync(ex, _endpoints);
            Assert.That(first, Is.True);

            // Immediate second attempt should be throttled by polling service
            var second = await _recoveryService.TryRecoverFromAuthenticationFailureAsync(ex, _endpoints);
            Assert.That(second, Is.False);
        }
    }
}
