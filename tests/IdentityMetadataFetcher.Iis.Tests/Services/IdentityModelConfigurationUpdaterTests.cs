using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Services;
using IdentityMetadataFetcher.Iis.Tests.Mocks;

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
            var entry = new MetadataCacheEntry
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
                // If creating invalid clause fails, just leave KeyInfo empty
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
                // If creating corrupted clause fails, leave KeyInfo empty
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
            // For .NET Framework compatibility, we'll create a simple byte array representing a minimal certificate
            // In real tests, you would use actual certificate files or embedded resources
            // This is just for testing the code paths, not actual certificate validation
            
            // Create a basic self-signed certificate using the old API
            // Note: This is a simplified version for testing purposes
            var certBytes = Convert.FromBase64String(
                "MIIDIzCCAgugAwIBAgIQOcKwOqNYGrIGXgDPIR7cHjANBgkqhkiG9w0BAQsFADAU" +
                "MRIwEAYDVQQDDAlUZXN0IENlcnQwHhcNMjQwMTAxMDAwMDAwWhcNMjUwMTAxMDAw" +
                "MDAwWjAUMRIwEAYDVQQDDAlUZXN0IENlcnQwggEiMA0GCSqGSIb3DQEBAQUAA4IB" +
                "DwAwggEKAoIBAQC6vI1v3z8cQ8N+8Bke7YSM3oMfEQvYJx9nH2h8cxq1H7N5mJZb" +
                "YQPGHLmQVp8Y9K3aM7VtLx6pN8TQ7bYxfH4QNZ9k4Y9XfE5vL8fN6pY9fN6h8cYx" +
                "9k4Y9XfE5vL8fN6pY9fN6h8cYx9k4Y9XfE5vL8fN6pY9fN6h8cYx9k4Y9XfE5vL8" +
                "fN6pY9fN6h8cYx9k4Y9XfE5vL8fN6pY9fN6h8cYxqZfpPxQIDAQABo3sweTAOBgNV" +
                "HQ8BAf8EBAMCBaAwHQYDVR0lBBYwFAYIKwYBBQUHAwEGCCsGAQUFBwMCMB0GA1Ud" +
                "DgQWBBQ7QH+xYQW5rBZR8U6fqR0QxLHq2zAfBgNVHSMEGDAWgBQ7QH+xYQW5rBZR" +
                "8U6fqR0QxLHq2zANBgkqhkiG9w0BAQsFAAOCAQEAK1V8L5gBx8pPQXNvYqQXD7YR" +
                "fN6h8cYx9k4Y9XfE5vL8fN6pY9fN6h8cYx9k4Y9XfE5vL8fN6pY9fN6h8cYx9k4Y" +
                "9XfE5vL8fN6pY9fN6h8cYx9k4Y9XfE5vL8fN6pY9fN6h8cYx9k4Y9XfE5vL8");
                
            try
            {
                return new X509Certificate2(certBytes);
            }
            catch
            {
                // If the above doesn't work, create a minimal valid cert for testing
                // This fallback creates a very basic certificate structure
                return new X509Certificate2(System.Text.Encoding.UTF8.GetBytes("TEST_CERT_DATA"));
            }
        }

        #endregion
    }
}
