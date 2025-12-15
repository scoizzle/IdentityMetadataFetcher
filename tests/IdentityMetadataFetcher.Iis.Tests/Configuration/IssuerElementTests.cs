using IdentityMetadataFetcher.Iis.Configuration;
using IdentityMetadataFetcher.Models;
using NUnit.Framework;
using System;
using System.Configuration;

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
            Assert.That(_element.Id, Is.EqualTo("test-issuer"));
        }

        [Test]
        public void Endpoint_CanBeSet()
        {
            var url = "https://example.com/metadata";
            _element.Endpoint = url;
            Assert.That(_element.Endpoint, Is.EqualTo(url));
        }

        [Test]
        public void Name_CanBeSet()
        {
            _element.Name = "Test Issuer";
            Assert.That(_element.Name, Is.EqualTo("Test Issuer"));
        }

        [Test]
        public void TimeoutSeconds_CanBeSet()
        {
            _element.TimeoutSeconds = 15;
            Assert.That(_element.TimeoutSeconds, Is.EqualTo(15));
        }

        [Test]
        public void ToIssuerEndpoint_ConvertsCorrectly()
        {
            _element.Id = "issuer-1";
            _element.Endpoint = "https://example.com/metadata";
            _element.Name = "Example";
            _element.TimeoutSeconds = 20;

            var endpoint = _element.ToIssuerEndpoint();

            Assert.That(endpoint, Is.Not.Null);
            Assert.That(endpoint.Id, Is.EqualTo("issuer-1"));
            Assert.That(endpoint.Endpoint, Is.EqualTo("https://example.com/metadata"));
            Assert.That(endpoint.Name, Is.EqualTo("Example"));
            Assert.That(endpoint.Timeout, Is.EqualTo(20000)); // TimeoutSeconds converted to milliseconds
        }

        [Test]
        public void ToIssuerEndpoint_ThrowsOnMissingId()
        {
            _element.Endpoint = "https://example.com/metadata";
            _element.Name = "Example";

            Assert.Throws<ConfigurationErrorsException>(() => _element.ToIssuerEndpoint());
        }

        [Test]
        public void ToIssuerEndpoint_ThrowsOnEmptyId()
        {
            _element.Id = "";
            _element.Endpoint = "https://example.com/metadata";
            _element.Name = "Example";

            Assert.Throws<ConfigurationErrorsException>(() => _element.ToIssuerEndpoint());
        }

        [Test]
        public void ToIssuerEndpoint_TimeoutSecondsIsOptional()
        {
            _element.Id = "issuer-1";
            _element.Endpoint = "https://example.com/metadata";
            _element.Name = "Example";
            _element.TimeoutSeconds = 0; // Not set

            var endpoint = _element.ToIssuerEndpoint();

            Assert.That(endpoint.Timeout, Is.Null);
        }
    }
}
