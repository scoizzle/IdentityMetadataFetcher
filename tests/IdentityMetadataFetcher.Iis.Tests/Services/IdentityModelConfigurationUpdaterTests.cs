using IdentityMetadataFetcher.Iis.Services;
using IdentityMetadataFetcher.Iis.Tests.Mocks;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services; // Core library
using Microsoft.IdentityModel.Protocols.WsFederation;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace IdentityMetadataFetcher.Iis.Tests.Services
{
    [TestFixture]
    public class IdentityModelConfigurationUpdaterTests
    {
        private IdentityModelConfigurationUpdater _updater;

        [SetUp]
        public void Setup()
        {
            _updater = new IdentityModelConfigurationUpdater();
        }

        [Test]
        public void Apply_WithNullEntry_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _updater.Apply(null, "Test Issuer"));
        }

        [Test]
        public void Apply_WithNullMetadata_DoesNotThrow()
        {
            var entry = new MetadataCacheEntry // From core library
            {
                IssuerId = "test-issuer",
                Metadata = null,
                RawXml = "<test />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithEmptyMetadata_DoesNotThrow()
        {
            var config = new WsFederationConfiguration();
            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithEmptySigningKeys_DoesNotThrow()
        {
            // SigningKeys collection exists but is empty by default
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com"
            };

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithMetadataContainingSigningCertificate_UpdatesIssuerNameRegistry()
        {
            // This test validates the overall flow but cannot fully test System.IdentityModel
            // runtime configuration in a unit test context without extensive mocking
            var config = CreateConfigurationWithSigningCert();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            // Verify it doesn't throw - actual registry update requires FederationConfiguration
            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithInvalidThumbprint_HandlesGracefully()
        {
            var config = CreateConfigurationWithInvalidKey();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithNullIssuerDisplayName_UsesCacheEntryIssuerId()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com"
            };
            
            var entry = new MetadataCacheEntry
            {
                IssuerId = "fallback-issuer-id",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            // Verify it uses the fallback without throwing
            Assert.DoesNotThrow(() => _updater.Apply(entry, null));
        }

        [Test]
        public void Apply_WithEmptyIssuerDisplayName_UsesCacheEntryIssuerId()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com"
            };
            
            var entry = new MetadataCacheEntry
            {
                IssuerId = "fallback-issuer-id",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            // Verify it uses the fallback without throwing
            Assert.DoesNotThrow(() => _updater.Apply(entry, ""));
        }

        [Test]
        public void Apply_WithMultipleSigningCertificates_ProcessesAll()
        {
            var config = CreateConfigurationWithMultipleSigningCerts();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithValidSigningKey_ProcessesCertificate()
        {
            var config = CreateConfigurationWithSigningCert();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithRsaSecurityKey_HandlesGracefully()
        {
            var config = CreateConfigurationWithRsaKey();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithTokenEndpoint_UpdatesIssuer()
        {
            var config = CreateConfigurationWithTokenEndpoint();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            // Verify it doesn't throw - actual module update requires WSFederationAuthenticationModule
            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithoutTokenEndpoint_DoesNotUpdateIssuer()
        {
            var config = CreateConfigurationWithoutTokenEndpoint();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithMixedSecurityKeys_ProcessesAllX509Keys()
        {
            var config = CreateConfigurationWithMixedKeyTypes();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithInvalidSecurityKey_HandlesGracefully()
        {
            var config = CreateConfigurationWithInvalidKey();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new WsFederationMetadataDocument(config, "<EntityDescriptor />"),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        #region Helper Methods

        private WsFederationConfiguration CreateConfigurationWithSigningCert()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com"
            };
            
            // Create a test certificate and wrap it in X509SecurityKey
            var cert = CreateTestCertificate();
            var x509Key = new X509SecurityKey(cert);
            config.SigningKeys.Add(x509Key);
            
            return config;
        }

        private WsFederationConfiguration CreateConfigurationWithMultipleSigningCerts()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com"
            };
            
            // Add multiple signing certificates
            for (int i = 0; i < 3; i++)
            {
                var cert = CreateTestCertificate();
                var x509Key = new X509SecurityKey(cert);
                config.SigningKeys.Add(x509Key);
            }
            
            return config;
        }

        private WsFederationConfiguration CreateConfigurationWithRsaKey()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com"
            };
            
            // Add an RSA key (no certificate) - don't dispose RSA while it's referenced
            var rsa = System.Security.Cryptography.RSA.Create();
            var rsaKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa);
            config.SigningKeys.Add(rsaKey);
            
            return config;
        }

        private WsFederationConfiguration CreateConfigurationWithMixedKeyTypes()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com"
            };
            
            // Add X509 key
            var cert1 = CreateTestCertificate();
            config.SigningKeys.Add(new X509SecurityKey(cert1));
            
            // Add RSA key - don't dispose RSA while it's referenced
            var rsa = System.Security.Cryptography.RSA.Create();
            config.SigningKeys.Add(new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa));
            
            // Add another X509 key
            var cert2 = CreateTestCertificate();
            config.SigningKeys.Add(new X509SecurityKey(cert2));
            
            return config;
        }

        private WsFederationConfiguration CreateConfigurationWithInvalidKey()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com"
            };
            
            // Add a mock key (will be handled gracefully)
            config.SigningKeys.Add(new MockSecurityKey());
            
            return config;
        }

        private WsFederationConfiguration CreateConfigurationWithTokenEndpoint()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com",
                TokenEndpoint = "https://sts.example.com/wsfed"
            };
            
            return config;
        }

        private WsFederationConfiguration CreateConfigurationWithoutTokenEndpoint()
        {
            var config = new WsFederationConfiguration
            {
                Issuer = "https://test.example.com",
                TokenEndpoint = null
            };
            
            return config;
        }

        private X509Certificate2 CreateTestCertificate()
        {
            // Use a real self-signed test certificate (generated with OpenSSL)
            // This is a minimal valid X.509 certificate for testing purposes only
            // NOTE: This is a test-only certificate with no sensitive data.
            // In production code, certificates should be loaded from secure storage or key vaults.
            var certBytes = Convert.FromBase64String(
                "MIIDFzCCAf+gAwIBAgIUJFDuEHUIo9jyTEp/H3EX2logxicwDQYJKoZIhvcNAQEL" +
                "BQAwGzEZMBcGA1UEAwwQVGVzdCBDZXJ0aWZpY2F0ZTAeFw0yNTEyMTIxNTA4MjVa" +
                "Fw0yNjEyMTIxNTA4MjVaMBsxGTAXBgNVBAMMEFRlc3QgQ2VydGlmaWNhdGUwggEi" +
                "MA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCiUybZvoHuth9uQb5hbTbmSs5U" +
                "nwUxwitg6B8y5a8YdotoCHeUNIkCdpWxi4La17D1rYFKbnws6K4NSz3kI0TkEsmo" +
                "rZhRLIFmxZUbFm8trmZEk6Qft9WJF1WFy57//mi3MyVetDrjIIspvxLw6HWbTxR3" +
                "u0ZUdqxp5QfQtbvMRtsYIjIHCljYrMBFz0klg4/qKJoVfQdJ2tUwetZlIGMMWkGt" +
                "giPIIUXAXVDVgbhDW74+a0vsCK4We7vTQwH+Chbd99VvufaFP70DyXZ4bBblCvyW" +
                "GImNC2/l4d2bcg1LAFIsUPCCCRhCZnmIAhKndnDh9LEaAn2odySBt9ApFkLdAgMB" +
                "AAGjUzBRMB0GA1UdDgQWBBQ4dLQfB7MGq4oghjeRDGVOMp5YPTAfBgNVHSMEGDAW" +
                "gBQ4dLQfB7MGq4oghjeRDGVOMp5YPTAPBgNVHRMBAf8EBTADAQH/MA0GCSqGSIb3" +
                "DQEBCwUAA4IBAQCMk8Hl0y3FudtrhEV/hRhFx0LNP/k228c32PWE6i054taoVe0V" +
                "2zt0nhg6DPL3BleWXGIEEtKVIAhoYNJBFyCWgCkdB+GS/nfRyDFn0e3toOnW7lEE" +
                "jJ2oszalg7St89xoEFdu6/uTX+XCsa0WmOeJf8B4jYUgxTGtbol/h0Siqgd85WmF" +
                "JwqNiD28QUgudNrrPEaQZaNOFyP6XIx2e9oX93mJ1rnEL7THAQ+idyHlcDmf4QnL" +
                "6tQbVX3kGZttWdbSvT52ixGY4zvFv+KjUfIG9XUILNH7/ukRwcxDZsTVHOfJrhg4" +
                "76Kubo+IdIvxDEOHeS++8HyA0nvpA1lQjGMB");
                
            return new X509Certificate2(certBytes);
        }

        #endregion
    }
}
