using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using NUnit.Framework;
using System.Threading.Tasks;

namespace IdentityMetadataFetcher.Tests
{
    [TestFixture]
    public class OidcMetadataFetcherTests
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
        [Category("Integration")]
        [Explicit("Requires internet connection")]
        public void FetchMetadata_WithGoogleOidcEndpoint_ReturnsSuccessResult()
        {
            // Arrange
            var endpoint = new IssuerEndpoint
            {
                Id = "google-oidc",
                Endpoint = "https://accounts.google.com/.well-known/openid-configuration",
                Name = "Google OIDC"
            };

            // Act
            var result = _fetcher.FetchMetadata(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata, Is.InstanceOf<OpenIdConnectMetadataDocument>());
            var oidcDoc = result.Metadata as OpenIdConnectMetadataDocument;
            Assert.That(oidcDoc.Issuer, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RawMetadata, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RawMetadata.TrimStart(), Does.StartWith("{"));
        }

        [Test]
        [Category("Integration")]
        [Explicit("Requires internet connection")]
        public async Task FetchMetadataAsync_WithGoogleOidcEndpoint_ReturnsSuccessResult()
        {
            // Arrange
            var endpoint = new IssuerEndpoint
            {
                Id = "google-oidc",
                Endpoint = "https://accounts.google.com/.well-known/openid-configuration",
                Name = "Google OIDC"
            };

            // Act
            var result = await _fetcher.FetchMetadataAsync(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata, Is.InstanceOf<OpenIdConnectMetadataDocument>());
            var oidcDoc = result.Metadata as OpenIdConnectMetadataDocument;
            Assert.That(oidcDoc.Issuer, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RawMetadata, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        [Category("Integration")]
        [Explicit("Requires internet connection")]
        public void FetchMetadata_WithAzureAdOidcEndpoint_ReturnsSuccessResult()
        {
            // Arrange
            var endpoint = new IssuerEndpoint
            {
                Id = "azure-ad-oidc",
                Endpoint = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
                Name = "Azure AD OIDC"
            };

            // Act
            var result = _fetcher.FetchMetadata(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata, Is.InstanceOf<OpenIdConnectMetadataDocument>());
            var oidcDoc = result.Metadata as OpenIdConnectMetadataDocument;
            Assert.That(oidcDoc.Configuration, Is.Not.Null);
            Assert.That(oidcDoc.Configuration.AuthorizationEndpoint, Is.Not.Null.And.Not.Empty);
            Assert.That(oidcDoc.Configuration.TokenEndpoint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        [Category("Integration")]
        [Explicit("Requires internet connection")]
        public void FetchMetadata_OidcEndpoint_ExtractsSigningKeys()
        {
            // Arrange
            var endpoint = new IssuerEndpoint
            {
                Id = "google-oidc",
                Endpoint = "https://accounts.google.com/.well-known/openid-configuration",
                Name = "Google OIDC"
            };

            // Act
            var result = _fetcher.FetchMetadata(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata, Is.InstanceOf<OpenIdConnectMetadataDocument>());
            var oidcDoc = result.Metadata as OpenIdConnectMetadataDocument;
            Assert.That(oidcDoc.Configuration.SigningKeys, Is.Not.Null);
            Assert.That(oidcDoc.Configuration.SigningKeys.Count, Is.GreaterThan(0));
        }

        [Test]
        [Category("Integration")]
        [Explicit("Requires internet connection")]
        public void FetchMetadata_OidcEndpoint_ExtractsEndpoints()
        {
            // Arrange
            var endpoint = new IssuerEndpoint
            {
                Id = "google-oidc",
                Endpoint = "https://accounts.google.com/.well-known/openid-configuration",
                Name = "Google OIDC"
            };

            // Act
            var result = _fetcher.FetchMetadata(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata, Is.InstanceOf<OpenIdConnectMetadataDocument>());
            var oidcDoc = result.Metadata as OpenIdConnectMetadataDocument;
            Assert.That(oidcDoc.Endpoints, Is.Not.Null);
            Assert.That(oidcDoc.Endpoints.ContainsKey("AuthorizationEndpoint"), Is.True);
            Assert.That(oidcDoc.Endpoints.ContainsKey("TokenEndpoint"), Is.True);
            Assert.That(oidcDoc.Endpoints.ContainsKey("JwksUri"), Is.True);
        }

        [Test]
        [Category("Integration")]
        [Explicit("Requires internet connection")]
        public void FetchMetadata_WsFedEndpoint_StillWorksCorrectly()
        {
            // Arrange - Test backward compatibility with WS-Fed endpoints
            var endpoint = new IssuerEndpoint
            {
                Id = "azure-ad-wsfed",
                Endpoint = "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
                Name = "Azure AD WS-Fed"
            };

            // Act
            var result = _fetcher.FetchMetadata(endpoint);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata, Is.InstanceOf<WsFederationMetadataDocument>());
            Assert.That(result.RawMetadata, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RawMetadata.TrimStart(), Does.StartWith("<"));
        }
    }
}
