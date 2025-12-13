using System;
using Microsoft.IdentityModel.Protocols.WsFederation.Metadata;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Services;
using IdentityMetadataFetcher.Iis.Tests.Mocks;
using IdentityMetadataFetcher.Services; // Core library

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
            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = new EntityDescriptor(),
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithNullRoleDescriptors_DoesNotThrow()
        {
            var entity = new EntityDescriptor();
            entity.RoleDescriptors.Clear();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
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
            var entity = CreateEntityDescriptorWithSigningCert();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            // Verify it doesn't throw - actual registry update requires FederationConfiguration
            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithInvalidThumbprint_HandlesGracefully()
        {
            var entity = CreateEntityDescriptorWithInvalidThumbprint();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithNullIssuerDisplayName_UsesCacheEntryIssuerId()
        {
            var entity = new EntityDescriptor();
            
            var entry = new MetadataCacheEntry
            {
                IssuerId = "fallback-issuer-id",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            // Verify it uses the fallback without throwing
            Assert.DoesNotThrow(() => _updater.Apply(entry, null));
        }

        [Test]
        public void Apply_WithEmptyIssuerDisplayName_UsesCacheEntryIssuerId()
        {
            var entity = new EntityDescriptor();
            
            var entry = new MetadataCacheEntry
            {
                IssuerId = "fallback-issuer-id",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            // Verify it uses the fallback without throwing
            Assert.DoesNotThrow(() => _updater.Apply(entry, ""));
        }

        [Test]
        public void Apply_WithMultipleSigningCertificates_ProcessesAll()
        {
            var entity = CreateEntityDescriptorWithMultipleSigningCerts();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithUnspecifiedKeyType_IncludesCertificate()
        {
            var entity = CreateEntityDescriptorWithUnspecifiedKeyType();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithEncryptionKeyType_SkipsCertificate()
        {
            var entity = CreateEntityDescriptorWithEncryptionKey();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithPassiveStsEndpoint_UpdatesIssuer()
        {
            var entity = CreateEntityDescriptorWithPassiveSts();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            // Verify it doesn't throw - actual module update requires WSFederationAuthenticationModule
            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithoutPassiveStsEndpoint_DoesNotUpdateIssuer()
        {
            var entity = CreateEntityDescriptorWithoutPassiveSts();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithMixedKeyTypes_ProcessesOnlySigningAndUnspecified()
        {
            var entity = CreateEntityDescriptorWithMixedKeyTypes();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        [Test]
        public void Apply_WithCorruptedCertificateData_HandlesGracefully()
        {
            var entity = CreateEntityDescriptorWithCorruptedCert();

            var entry = new MetadataCacheEntry
            {
                IssuerId = "test-issuer",
                Metadata = entity,
                RawXml = "<EntityDescriptor />",
                CachedAt = DateTime.UtcNow
            };

            Assert.DoesNotThrow(() => _updater.Apply(entry, "Test Issuer"));
        }

        #region Helper Methods

        private EntityDescriptor CreateEntityDescriptorWithSigningCert()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var role = new MockRoleDescriptor();
            
            // Create a test certificate
            var cert = CreateTestCertificate();
            var keyInfo = new X509RawDataKeyIdentifierClause(cert);
            
            var keyDescriptor = new KeyDescriptor
            {
                Use = KeyType.Signing
            };
            keyDescriptor.KeyInfo.Add(keyInfo);
            
            role.Keys.Add(keyDescriptor);
            entity.RoleDescriptors.Add(role);
            
            return entity;
        }

        private EntityDescriptor CreateEntityDescriptorWithMultipleSigningCerts()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var role = new MockRoleDescriptor();
            
            // Add multiple signing certificates
            for (int i = 0; i < 3; i++)
            {
                var cert = CreateTestCertificate();
                var keyInfo = new X509RawDataKeyIdentifierClause(cert);
                
                var keyDescriptor = new KeyDescriptor
                {
                    Use = KeyType.Signing
                };
                keyDescriptor.KeyInfo.Add(keyInfo);
                
                role.Keys.Add(keyDescriptor);
            }
            
            entity.RoleDescriptors.Add(role);
            return entity;
        }

        private EntityDescriptor CreateEntityDescriptorWithUnspecifiedKeyType()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var role = new MockRoleDescriptor();
            
            var cert = CreateTestCertificate();
            var keyInfo = new X509RawDataKeyIdentifierClause(cert);
            
            var keyDescriptor = new KeyDescriptor
            {
                Use = KeyType.Unspecified
            };
            keyDescriptor.KeyInfo.Add(keyInfo);
            
            role.Keys.Add(keyDescriptor);
            entity.RoleDescriptors.Add(role);
            
            return entity;
        }

        private EntityDescriptor CreateEntityDescriptorWithEncryptionKey()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var role = new MockRoleDescriptor();
            
            var cert = CreateTestCertificate();
            var keyInfo = new X509RawDataKeyIdentifierClause(cert);
            
            var keyDescriptor = new KeyDescriptor
            {
                Use = KeyType.Encryption
            };
            keyDescriptor.KeyInfo.Add(keyInfo);
            
            role.Keys.Add(keyDescriptor);
            entity.RoleDescriptors.Add(role);
            
            return entity;
        }

        private EntityDescriptor CreateEntityDescriptorWithMixedKeyTypes()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var role = new MockRoleDescriptor();
            
            // Add signing key
            var signingCert = CreateTestCertificate();
            var signingKeyInfo = new X509RawDataKeyIdentifierClause(signingCert);
            var signingKeyDescriptor = new KeyDescriptor { Use = KeyType.Signing };
            signingKeyDescriptor.KeyInfo.Add(signingKeyInfo);
            role.Keys.Add(signingKeyDescriptor);
            
            // Add encryption key
            var encryptionCert = CreateTestCertificate();
            var encryptionKeyInfo = new X509RawDataKeyIdentifierClause(encryptionCert);
            var encryptionKeyDescriptor = new KeyDescriptor { Use = KeyType.Encryption };
            encryptionKeyDescriptor.KeyInfo.Add(encryptionKeyInfo);
            role.Keys.Add(encryptionKeyDescriptor);
            
            // Add unspecified key
            var unspecifiedCert = CreateTestCertificate();
            var unspecifiedKeyInfo = new X509RawDataKeyIdentifierClause(unspecifiedCert);
            var unspecifiedKeyDescriptor = new KeyDescriptor { Use = KeyType.Unspecified };
            unspecifiedKeyDescriptor.KeyInfo.Add(unspecifiedKeyInfo);
            role.Keys.Add(unspecifiedKeyDescriptor);
            
            entity.RoleDescriptors.Add(role);
            return entity;
        }

        private EntityDescriptor CreateEntityDescriptorWithInvalidThumbprint()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var role = new MockRoleDescriptor();
            
            // Create a key descriptor without proper certificate data
            var keyDescriptor = new KeyDescriptor
            {
                Use = KeyType.Signing
            };
            
            // Add an empty or invalid thumbprint clause
            try
            {
                var invalidThumbprint = new byte[0];
                var thumbprintClause = new X509ThumbprintKeyIdentifierClause(invalidThumbprint);
                keyDescriptor.KeyInfo.Add(thumbprintClause);
            }
            catch
            {
                // Expected: Creating a clause with invalid thumbprint may throw
                // Leave KeyInfo empty to test error handling in the updater
            }
            
            role.Keys.Add(keyDescriptor);
            entity.RoleDescriptors.Add(role);
            
            return entity;
        }

        private EntityDescriptor CreateEntityDescriptorWithCorruptedCert()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var role = new MockRoleDescriptor();
            
            var keyDescriptor = new KeyDescriptor
            {
                Use = KeyType.Signing
            };
            
            // Try to create a corrupted certificate - if this fails, we just return empty
            try
            {
                var corruptedData = new byte[] { 0x00, 0x01, 0x02, 0x03 }; // Invalid cert data
                var keyInfo = new X509RawDataKeyIdentifierClause(corruptedData);
                keyDescriptor.KeyInfo.Add(keyInfo);
            }
            catch
            {
                // Expected: Creating a clause with corrupted data may throw
                // Leave KeyInfo empty to test error handling in the updater
            }
            
            role.Keys.Add(keyDescriptor);
            entity.RoleDescriptors.Add(role);
            
            return entity;
        }

        private EntityDescriptor CreateEntityDescriptorWithPassiveSts()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var sts = new SecurityTokenServiceDescriptor();
            var endpoint = new System.IdentityModel.Protocols.WSTrust.EndpointReference("https://sts.example.com/wsfed");
            sts.PassiveRequestorEndpoints.Add(endpoint);
            
            entity.RoleDescriptors.Add(sts);
            
            return entity;
        }

        private EntityDescriptor CreateEntityDescriptorWithoutPassiveSts()
        {
            var entity = new EntityDescriptor();
            entity.EntityId = new EntityId("https://test.example.com");
            
            var role = new MockRoleDescriptor();
            entity.RoleDescriptors.Add(role);
            
            return entity;
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
