using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Services;
using IdentityMetadataFetcher.Iis.Tests.Mocks; // Use IIS test mocks
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

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
                new IssuerEndpoint("issuer1", "https://issuer1.example.com/metadata", "Issuer 1", MetadataType.SAML),
                new IssuerEndpoint("issuer2", "https://issuer2.example.com/metadata", "Issuer 2", MetadataType.WSFED)
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
            // Pre-populate cache with entity id for issuer1
            var entity = new EntityDescriptor { EntityId = new EntityId("https://issuer1.example.com") };
            _cache.AddOrUpdateMetadata("issuer1", entity, "<xml />");

            var ex = new System.IdentityModel.Tokens.SecurityTokenValidationException(
                "Issuer 'https://issuer1.example.com' signature key not found");

            var result = await _recoveryService.TryRecoverFromAuthenticationFailureAsync(ex, _endpoints);
            Assert.IsTrue(result);
        }

        [Test]
        public async Task TryRecoverFromAuthenticationFailureAsync_PollThrottled_ReturnsFalse()
        {
            var ex = new System.IdentityModel.Tokens.SecurityTokenValidationException(
                "ID4037: Key not found");

            // First recovers
            var first = await _recoveryService.TryRecoverFromAuthenticationFailureAsync(ex, _endpoints);
            Assert.IsTrue(first);

            // Immediate second attempt should be throttled by polling service
            var second = await _recoveryService.TryRecoverFromAuthenticationFailureAsync(ex, _endpoints);
            Assert.IsFalse(second);
        }
    }
}
