using IdentityMetadataFetcher.Models;
using NUnit.Framework;

namespace IdentityMetadataFetcher.Tests
{
    [TestFixture]
    public class IssuerEndpointTests
    {
        [Test]
        public void Constructor_Default_CreatesEmptyEndpoint()
        {
            var endpoint = new IssuerEndpoint();
            Assert.That(endpoint.Id, Is.Null);
            Assert.That(endpoint.Endpoint, Is.Null);
            Assert.That(endpoint.Name, Is.Null);
            Assert.That(endpoint.Timeout, Is.Null);
        }

        [Test]
        public void Constructor_WithParameters_SetsAllProperties()
        {
            var endpoint = new IssuerEndpoint("test-id", "http://example.com/metadata", "Test Issuer", MetadataType.SAML);
            
            Assert.That(endpoint.Id, Is.EqualTo("test-id"));
            Assert.That(endpoint.Endpoint, Is.EqualTo("http://example.com/metadata"));
            Assert.That(endpoint.Name, Is.EqualTo("Test Issuer"));
            Assert.That(endpoint.MetadataType, Is.EqualTo(MetadataType.SAML));
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

            Assert.That(endpoint.Id, Is.EqualTo("new-id"));
            Assert.That(endpoint.Endpoint, Is.EqualTo("http://issuer.example.com/metadata"));
            Assert.That(endpoint.Name, Is.EqualTo("New Issuer"));
            Assert.That(endpoint.MetadataType, Is.EqualTo(MetadataType.WSFED));
            Assert.That(endpoint.Timeout, Is.EqualTo(15000));
        }

        [Test]
        public void MetadataType_SAML_IsValidValue()
        {
            var endpoint = new IssuerEndpoint { MetadataType = MetadataType.SAML };
            Assert.That(endpoint.MetadataType, Is.EqualTo(MetadataType.SAML));
        }

        [Test]
        public void MetadataType_WSFED_IsValidValue()
        {
            var endpoint = new IssuerEndpoint { MetadataType = MetadataType.WSFED };
            Assert.That(endpoint.MetadataType, Is.EqualTo(MetadataType.WSFED));
        }

        [Test]
        public void Timeout_CanBeNullOrInteger()
        {
            var endpoint1 = new IssuerEndpoint();
            Assert.That(endpoint1.Timeout, Is.Null);

            var endpoint2 = new IssuerEndpoint();
            endpoint2.Timeout = 30000;
            Assert.That(endpoint2.Timeout, Is.EqualTo(30000));
        }
    }
}
