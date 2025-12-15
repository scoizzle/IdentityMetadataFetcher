using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityMetadataFetcher.Tests.Integration
{
    /// <summary>
    /// Integration tests that fetch real metadata from public endpoints.
    /// These tests require network access and may fail if endpoints are unavailable.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    [Explicit("Requires network access to public endpoints")]
    public class RealMetadataFetchTests
    {
        [Test]
        public async Task FetchMetadata_AzureAD_WsFed_Success()
        {
            // Arrange
            var fetcher = new MetadataFetcher(new MetadataFetchOptions
            {
                DefaultTimeoutMs = 30000,
                ValidateServerCertificate = true,
                MaxRetries = 2
            });

            var endpoint = new IssuerEndpoint(
                "azure-ad-common",
                "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
                "Azure Active Directory (Common)"
            );

            // Act
            var result = await fetcher.FetchMetadataAsync(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True, $"Fetch failed: {result.ErrorMessage}");
            Assert.That(result.Metadata, Is.Not.Null);

            var wsFedDoc = result.Metadata;

            // Azure AD should have multiple signing certificates (they rotate keys)
            Assert.That(wsFedDoc.SigningCertificates.Count, Is.GreaterThan(0), 
                "Azure AD should have at least one signing certificate");

            Console.WriteLine($"Azure AD Metadata:");
            Console.WriteLine($"  Issuer: {wsFedDoc.Issuer}");
            Console.WriteLine($"  Signing Certificates: {wsFedDoc.SigningCertificates.Count}");
            Console.WriteLine($"  Endpoints: {wsFedDoc.Endpoints.Count}");

            // Should have key endpoints
            Assert.That(wsFedDoc.Endpoints.ContainsKey("PassiveSts") || 
                       wsFedDoc.Endpoints.ContainsKey("TokenEndpoint"), 
                Is.True, "Should have WS-Fed passive endpoint");

            // Verify certificates are valid X509 certificates
            foreach (var cert in wsFedDoc.SigningCertificates)
            {
                Assert.That(cert.Thumbprint, Is.Not.Null.And.Not.Empty);
                Assert.That(cert.NotAfter, Is.GreaterThan(DateTime.UtcNow), 
                    $"Certificate {cert.Thumbprint} has expired");
                
                Console.WriteLine($"  Certificate: {cert.Subject}");
                Console.WriteLine($"    Thumbprint: {cert.Thumbprint}");
                Console.WriteLine($"    Valid Until: {cert.NotAfter:yyyy-MM-dd}");
            }
        }

        [Test]
        public async Task FetchMetadata_SAMLTestId_Success()
        {
            // Arrange
            var fetcher = new MetadataFetcher(new MetadataFetchOptions
            {
                DefaultTimeoutMs = 30000,
                ValidateServerCertificate = true,
                MaxRetries = 2
            });

            // WsFederationMetadataSerializer handles SAML metadata too
            var endpoint = new IssuerEndpoint(
                "samltest-id",
                "https://samltest.id/saml/idp",
                "SAMLtest.id Test IdP"
            );

            // Act
            var result = await fetcher.FetchMetadataAsync(endpoint);

            // Assert
            // Note: SAMLtest.id may be temporarily unavailable or have changed their endpoint structure
            if (!result.IsSuccess)
            {
                Assert.Inconclusive($"SAMLtest.id endpoint may be unavailable: {result.ErrorMessage}");
                return;
            }

            Assert.That(result.Metadata, Is.Not.Null);

            var metadataDoc = result.Metadata;

            // Verify basic metadata properties
            Assert.That(metadataDoc.Issuer, Is.Not.Null.And.Not.Empty, "Should have Issuer");
            Assert.That(metadataDoc.SigningCertificates.Count, Is.GreaterThan(0), 
                "Should have at least one signing certificate");

            Console.WriteLine($"SAMLtest.id Metadata:");
            Console.WriteLine($"  Issuer: {metadataDoc.Issuer}");
            Console.WriteLine($"  Signing Certificates: {metadataDoc.SigningCertificates.Count}");
            Console.WriteLine($"  Endpoints: {metadataDoc.Endpoints.Count}");

            // Verify certificates
            foreach (var cert in metadataDoc.SigningCertificates)
            {
                Assert.That(cert.Thumbprint, Is.Not.Null.And.Not.Empty);
                Assert.That(cert.NotAfter, Is.GreaterThan(DateTime.UtcNow), 
                    $"Certificate {cert.Thumbprint} has expired");
                
                Console.WriteLine($"  Certificate: {cert.Subject}");
                Console.WriteLine($"    Thumbprint: {cert.Thumbprint}");
                Console.WriteLine($"    Valid Until: {cert.NotAfter:yyyy-MM-dd}");
            }

            // Verify endpoints
            foreach (var ep in metadataDoc.Endpoints.Take(5))
            {
                Console.WriteLine($"  Endpoint: {ep.Key} = {ep.Value}");
            }
        }

        [Test]
        public void FetchMetadata_SAMLTestId_Synchronous_Success()
        {
            // Arrange
            var fetcher = new MetadataFetcher(new MetadataFetchOptions
            {
                DefaultTimeoutMs = 30000,
                ValidateServerCertificate = true
            });

            var endpoint = new IssuerEndpoint(
                "samltest-id",
                "https://samltest.id/saml/idp",
                "SAMLtest.id"
            );

            // Act
            var result = fetcher.FetchMetadata(endpoint);

            // Assert
            // Note: SAMLtest.id may be temporarily unavailable
            if (!result.IsSuccess)
            {
                Assert.Inconclusive($"SAMLtest.id endpoint may be unavailable: {result.ErrorMessage}");
                return;
            }

            Assert.That(result.Metadata, Is.InstanceOf<WsFederationMetadataDocument>());
            
            var metadataDoc = result.Metadata;
            Assert.That(metadataDoc.SigningCertificates.Count, Is.GreaterThan(0));
            Assert.That(metadataDoc.Issuer, Is.Not.Null.And.Not.Empty);
            Assert.That(metadataDoc.Endpoints.Count, Is.GreaterThan(0));

            Console.WriteLine($"Fetched SAMLtest.id metadata successfully");
            Console.WriteLine($"  Issuer: {metadataDoc.Issuer}");
            Console.WriteLine($"  Certificates: {metadataDoc.SigningCertificates.Count}");
            Console.WriteLine($"  Endpoints: {metadataDoc.Endpoints.Count}");
        }

        [Test]
        public async Task FetchMetadata_MultipleEndpoints_Success()
        {
            // Arrange
            var fetcher = new MetadataFetcher(new MetadataFetchOptions
            {
                DefaultTimeoutMs = 30000,
                ContinueOnError = true,
                ValidateServerCertificate = true,
                MaxRetries = 1
            });

            var endpoints = new[]
            {
                new IssuerEndpoint(
                    "azure-ad",
                    "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
                    "Azure AD"
                ),
                new IssuerEndpoint(
                    "samltest-id",
                    "https://samltest.id/saml/idp",
                    "SAMLtest.id"
                ),
                new IssuerEndpoint(
                    "azure-ad-us-gov",
                    "https://login.microsoftonline.us/common/federationmetadata/2007-06/federationmetadata.xml",
                    "Azure AD US Gov"
                )
            };

            // Act
            var results = await fetcher.FetchMetadataFromMultipleEndpointsAsync(endpoints);

            // Assert
            Assert.That(results, Is.Not.Null);
            var resultsList = results.ToList();
            Assert.That(resultsList.Count, Is.EqualTo(3));

            // At least Azure AD commercial should succeed (SAMLtest.id may be unavailable)
            var successCount = resultsList.Count(r => r.IsSuccess);
            Assert.That(successCount, Is.GreaterThanOrEqualTo(1), 
                "At least Azure AD commercial should succeed");

            foreach (var result in resultsList)
            {
                Console.WriteLine($"{result.Endpoint.Name}: {(result.IsSuccess ? "? Success" : "? Failed")}");
                if (!result.IsSuccess)
                {
                    Console.WriteLine($"  Error: {result.ErrorMessage}");
                }
                else
                {
                    var metadataDoc = result.Metadata;
                    Console.WriteLine($"  Certificates: {metadataDoc.SigningCertificates.Count}");
                    Console.WriteLine($"  Issuer: {metadataDoc.Issuer}");
                }
            }
        }

        [Test]
        public void FetchMetadata_AzureAD_Synchronous_Success()
        {
            // Arrange
            var fetcher = new MetadataFetcher(new MetadataFetchOptions
            {
                DefaultTimeoutMs = 30000,
                ValidateServerCertificate = true
            });

            var endpoint = new IssuerEndpoint(
                "azure-ad",
                "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
                "Azure AD"
            );

            // Act
            var result = fetcher.FetchMetadata(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True, $"Fetch failed: {result.ErrorMessage}");
            
            var wsFedDoc = result.Metadata as WsFederationMetadataDocument;
            Assert.That(wsFedDoc, Is.Not.Null);
            Assert.That(wsFedDoc.SigningCertificates.Count, Is.GreaterThan(0));
            Assert.That(wsFedDoc.Configuration, Is.Not.Null);
            Assert.That(wsFedDoc.Configuration.Issuer, Is.Not.Null.And.Not.Empty);

            Console.WriteLine($"Fetched Azure AD metadata successfully");
            Console.WriteLine($"  Issuer: {wsFedDoc.Configuration.Issuer}");
            Console.WriteLine($"  Token Endpoint: {wsFedDoc.Configuration.TokenEndpoint}");
            Console.WriteLine($"  Signing Keys: {wsFedDoc.Configuration.SigningKeys.Count}");
        }

        [Test]
        public async Task VerifyAzureADMetadata_ContainsExpectedProperties()
        {
            // Arrange
            var fetcher = new MetadataFetcher();
            var endpoint = new IssuerEndpoint(
                "azure-ad",
                "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
                "Azure AD"
            );

            // Act
            var result = await fetcher.FetchMetadataAsync(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            
            var wsFedDoc = result.Metadata as WsFederationMetadataDocument;
            Assert.That(wsFedDoc, Is.Not.Null);

            // Verify WS-Federation specific properties
            Assert.That(wsFedDoc.Configuration, Is.Not.Null);
            Assert.That(wsFedDoc.Configuration.Issuer, Does.Contain("windows.net")
                .Or.Contains("microsoftonline"));
            
            // Azure AD includes token endpoint
            Assert.That(wsFedDoc.Configuration.TokenEndpoint, Is.Not.Null.And.Not.Empty);
            Assert.That(wsFedDoc.Configuration.TokenEndpoint, Does.Contain("wsfed"));

            // Should have signing keys from WsFederationConfiguration
            Assert.That(wsFedDoc.Configuration.SigningKeys, Is.Not.Null);
            Assert.That(wsFedDoc.Configuration.SigningKeys.Count, Is.GreaterThan(0));

            // Verify the document exposes certificates through IMetadataDocument
            Assert.That(wsFedDoc.SigningCertificates.Count, 
                Is.EqualTo(wsFedDoc.Configuration.SigningKeys.Count));

            // Verify endpoints dictionary
            Assert.That(wsFedDoc.Endpoints, Is.Not.Null);
            Assert.That(wsFedDoc.Endpoints.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task VerifyMetadata_ContainsExpectedProperties()
        {
            // Arrange
            var fetcher = new MetadataFetcher();
            var endpoint = new IssuerEndpoint(
                "samltest-id",
                "https://samltest.id/saml/idp",
                "SAMLtest.id"
            );

            // Act
            var result = await fetcher.FetchMetadataAsync(endpoint);

            // Assert
            // Note: SAMLtest.id may be temporarily unavailable or have changed endpoints
            if (!result.IsSuccess)
            {
                Assert.Inconclusive($"SAMLtest.id endpoint may be unavailable: {result.ErrorMessage}");
                return;
            }
            
            var metadataDoc = result.Metadata;
            Assert.That(metadataDoc, Is.Not.Null);

            // Verify metadata properties
            Assert.That(metadataDoc.Issuer, Is.Not.Null.And.Not.Empty);
            
            // Should have at least one signing certificate
            Assert.That(metadataDoc.SigningCertificates.Count, Is.GreaterThan(0));
            
            // Verify certificate properties
            var cert = metadataDoc.SigningCertificates[0];
            Assert.That(cert.Thumbprint, Is.Not.Null.And.Not.Empty);
            Assert.That(cert.NotBefore, Is.LessThan(DateTime.UtcNow));
            Assert.That(cert.NotAfter, Is.GreaterThan(DateTime.UtcNow));

            // Verify has endpoints
            Assert.That(metadataDoc.Endpoints.Count, Is.GreaterThan(0));
        }
    }
}
