using NUnit.Framework;
using IdentityMetadataFetcher.Models;

namespace IdentityMetadataFetcher.Tests
{
    [TestFixture]
    public class IssuerEndpointTests
    {
        [Test]
        public void Constructor_Default_CreatesEmptyEndpoint()
        {
            var endpoint = new IssuerEndpoint();
            Assert.IsNull(endpoint.Id);
            Assert.IsNull(endpoint.Endpoint);
            Assert.IsNull(endpoint.Name);
            Assert.IsNull(endpoint.Timeout);
        }

        [Test]
        public void Constructor_WithParameters_SetsAllProperties()
        {
            var endpoint = new IssuerEndpoint("test-id", "http://example.com/metadata", "Test Issuer", MetadataType.SAML);
            
            Assert.AreEqual("test-id", endpoint.Id);
            Assert.AreEqual("http://example.com/metadata", endpoint.Endpoint);
            Assert.AreEqual("Test Issuer", endpoint.Name);
            Assert.AreEqual(MetadataType.SAML, endpoint.MetadataType);
        }

        [Test]
        public void Properties_CanBeSet()
        {
            var endpoint = new IssuerEndpoint();
            
            endpoint.Id = "new-id";
            endpoint.Endpoint = "http://issuer.example.com/metadata";
            endpoint.Name = "New Issuer";
            endpoint.MetadataType = MetadataType.WSFED;
            endpoint.Timeout = 15000;

            Assert.AreEqual("new-id", endpoint.Id);
            Assert.AreEqual("http://issuer.example.com/metadata", endpoint.Endpoint);
            Assert.AreEqual("New Issuer", endpoint.Name);
            Assert.AreEqual(MetadataType.WSFED, endpoint.MetadataType);
            Assert.AreEqual(15000, endpoint.Timeout);
        }

        [Test]
        public void MetadataType_SAML_IsValidValue()
        {
            var endpoint = new IssuerEndpoint { MetadataType = MetadataType.SAML };
            Assert.AreEqual(MetadataType.SAML, endpoint.MetadataType);
        }

        [Test]
        public void MetadataType_WSFED_IsValidValue()
        {
            var endpoint = new IssuerEndpoint { MetadataType = MetadataType.WSFED };
            Assert.AreEqual(MetadataType.WSFED, endpoint.MetadataType);
        }

        [Test]
        public void Timeout_CanBeNullOrInteger()
        {
            var endpoint1 = new IssuerEndpoint();
            Assert.IsNull(endpoint1.Timeout);

            var endpoint2 = new IssuerEndpoint();
            endpoint2.Timeout = 30000;
            Assert.AreEqual(30000, endpoint2.Timeout);
        }
    }
}
