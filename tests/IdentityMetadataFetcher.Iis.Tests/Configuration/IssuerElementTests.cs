using System;
using System.Configuration;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Configuration;
using IdentityMetadataFetcher.Models;

namespace IdentityMetadataFetcher.Iis.Tests.Configuration
{
    [TestFixture]
    public class IssuerElementTests
    {
        private IssuerElement _element;

        [SetUp]
        public void Setup()
        {
            _element = new IssuerElement();
        }

        [Test]
        public void Id_CanBeSet()
        {
            _element.Id = "test-issuer";
            Assert.AreEqual("test-issuer", _element.Id);
        }

        [Test]
        public void Endpoint_CanBeSet()
        {
            var url = "https://example.com/metadata";
            _element.Endpoint = url;
            Assert.AreEqual(url, _element.Endpoint);
        }

        [Test]
        public void Name_CanBeSet()
        {
            _element.Name = "Test Issuer";
            Assert.AreEqual("Test Issuer", _element.Name);
        }

        [Test]
        public void MetadataType_CanBeSet()
        {
            _element.MetadataType = "Saml";
            Assert.AreEqual("Saml", _element.MetadataType);
        }

        [Test]
        public void TimeoutSeconds_CanBeSet()
        {
            _element.TimeoutSeconds = 15;
            Assert.AreEqual(15, _element.TimeoutSeconds);
        }

        [Test]
        public void ToIssuerEndpoint_ConvertsSamlMetadataType()
        {
            _element.Id = "issuer-1";
            _element.Endpoint = "https://example.com/metadata";
            _element.Name = "Example";
            _element.MetadataType = "Saml";
            _element.TimeoutSeconds = 20;

            var endpoint = _element.ToIssuerEndpoint();

            Assert.IsNotNull(endpoint);
            Assert.AreEqual("issuer-1", endpoint.Id);
            Assert.AreEqual("https://example.com/metadata", endpoint.Endpoint);
            Assert.AreEqual("Example", endpoint.Name);
            Assert.AreEqual(MetadataType.Saml, endpoint.MetadataType);
            Assert.AreEqual(20000, endpoint.Timeout); // TimeoutSeconds converted to milliseconds
        }

        [Test]
        public void ToIssuerEndpoint_ConvertsWsFedMetadataType()
        {
            _element.Id = "issuer-1";
            _element.Endpoint = "https://example.com/metadata";
            _element.MetadataType = "WsFed";

            var endpoint = _element.ToIssuerEndpoint();

            Assert.AreEqual(MetadataType.WsFed, endpoint.MetadataType);
        }

        [Test]
        public void ToIssuerEndpoint_ThrowsOnMissingId()
        {
            _element.Endpoint = "https://example.com/metadata";
            _element.MetadataType = "Saml";

            Assert.Throws<ConfigurationErrorsException>(() => _element.ToIssuerEndpoint());
        }

        [Test]
        public void ToIssuerEndpoint_ThrowsOnInvalidMetadataType()
        {
            _element.Id = "issuer-1";
            _element.Endpoint = "https://example.com/metadata";
            _element.MetadataType = "InvalidType";

            Assert.Throws<ConfigurationErrorsException>(() => _element.ToIssuerEndpoint());
        }

        [Test]
        public void ToIssuerEndpoint_ThrowsOnEmptyId()
        {
            _element.Id = "";
            _element.Endpoint = "https://example.com/metadata";
            _element.MetadataType = "Saml";

            Assert.Throws<ConfigurationErrorsException>(() => _element.ToIssuerEndpoint());
        }

        [Test]
        public void ToIssuerEndpoint_TimeoutSecondsIsOptional()
        {
            _element.Id = "issuer-1";
            _element.Endpoint = "https://example.com/metadata";
            _element.MetadataType = "Saml";
            _element.TimeoutSeconds = 0; // Not set

            var endpoint = _element.ToIssuerEndpoint();

            Assert.IsNull(endpoint.Timeout);
        }

        [Test]
        public void ToIssuerEndpoint_MetadataTypeCaseInsensitive()
        {
            _element.Id = "issuer-1";
            _element.Endpoint = "https://example.com/metadata";
            _element.MetadataType = "saml"; // lowercase

            var endpoint = _element.ToIssuerEndpoint();

            Assert.AreEqual(MetadataType.Saml, endpoint.MetadataType);
        }
    }
}
