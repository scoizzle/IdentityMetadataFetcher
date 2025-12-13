using IdentityMetadataFetcher.Iis.Configuration;
using IdentityMetadataFetcher.Models;
using NUnit.Framework;
using System;
using System.Linq;

namespace IdentityMetadataFetcher.Iis.Tests.Configuration
{
    [TestFixture]
    public class IssuerElementCollectionTests
    {
        private IssuerElementCollection _collection;

        [SetUp]
        public void Setup()
        {
            _collection = new IssuerElementCollection();
        }

        [Test]
        public void IsEmpty_WhenCreated()
        {
            Assert.That(_collection.Count, Is.EqualTo(0));
        }

        [Test]
        public void CanAddElement()
        {
            var element = new IssuerElement { Id = "issuer-1", Endpoint = "https://example.com/metadata", MetadataType = "Saml" };
            _collection.Add(element);

            Assert.That(_collection.Count, Is.EqualTo(1));
        }

        [Test]
        public void CanRemoveElement()
        {
            var element = new IssuerElement { Id = "issuer-1", Endpoint = "https://example.com/metadata", MetadataType = "Saml" };
            _collection.Add(element);
            _collection.Remove(element);

            Assert.That(_collection.Count, Is.EqualTo(0));
        }

        [Test]
        public void CanClearAllElements()
        {
            _collection.Add(new IssuerElement { Id = "issuer-1", MetadataType = "Saml" });
            _collection.Add(new IssuerElement { Id = "issuer-2", MetadataType = "Saml" });
            
            _collection.Clear();

            Assert.That(_collection.Count, Is.EqualTo(0));
        }

        [Test]
        public void CanAccessElementByIndex()
        {
            var element = new IssuerElement { Id = "issuer-1", Endpoint = "https://example.com/metadata", MetadataType = "Saml" };
            _collection.Add(element);

            var retrieved = _collection[0];
            Assert.That(retrieved.Id, Is.EqualTo("issuer-1"));
        }

        [Test]
        public void CanAccessElementById()
        {
            var element = new IssuerElement { Id = "issuer-1", Endpoint = "https://example.com/metadata", MetadataType = "Saml" };
            _collection.Add(element);

            var retrieved = _collection["issuer-1"];
            Assert.That(retrieved.Id, Is.EqualTo("issuer-1"));
        }

        [Test]
        public void ToIssuerEndpoints_ReturnsAllElements()
        {
            _collection.Add(new IssuerElement { Id = "issuer-1", Endpoint = "https://example1.com/metadata", MetadataType = "Saml" });
            _collection.Add(new IssuerElement { Id = "issuer-2", Endpoint = "https://example2.com/metadata", MetadataType = "WsFed" });

            var endpoints = _collection.ToIssuerEndpoints().ToList();

            Assert.That(endpoints.Count, Is.EqualTo(2));
            Assert.That(endpoints[0].Id, Is.EqualTo("issuer-1"));
            Assert.That(endpoints[1].Id, Is.EqualTo("issuer-2"));
        }

        [Test]
        public void ToIssuerEndpoints_ReturnsEmptyWhenNoElements()
        {
            var endpoints = _collection.ToIssuerEndpoints().ToList();

            Assert.That(endpoints.Count, Is.EqualTo(0));
        }

        [Test]
        public void ToIssuerEndpoints_ConvertsMetadataTypesCorrectly()
        {
            _collection.Add(new IssuerElement { Id = "issuer-1", Endpoint = "https://example1.com/metadata", MetadataType = "Saml" });
            _collection.Add(new IssuerElement { Id = "issuer-2", Endpoint = "https://example2.com/metadata", MetadataType = "WsFed" });

            var endpoints = _collection.ToIssuerEndpoints().ToList();

            Assert.That(endpoints[0].MetadataType, Is.EqualTo(MetadataType.SAML));
            Assert.That(endpoints[1].MetadataType, Is.EqualTo(MetadataType.WSFED));
        }

        [Test]
        public void Contains_ReturnsTrueForExistingElement()
        {
            var element = new IssuerElement { Id = "issuer-1", MetadataType = "Saml" };
            _collection.Add(element);

            Assert.That(_collection.Contains(element), Is.True);
        }

        [Test]
        public void Contains_ReturnsFalseForNonExistingElement()
        {
            var element = new IssuerElement { Id = "issuer-1", MetadataType = "Saml" };

            Assert.That(_collection.Contains(element), Is.False);
        }

        [Test]
        public void Multiple_Elements_CanCoexist()
        {
            for (int i = 1; i <= 5; i++)
            {
                _collection.Add(new IssuerElement 
                { 
                    Id = $"issuer-{i}", 
                    Endpoint = $"https://example{i}.com/metadata", 
                    MetadataType = "Saml" 
                });
            }

            Assert.That(_collection.Count, Is.EqualTo(5));
            var endpoints = _collection.ToIssuerEndpoints().ToList();
            Assert.That(endpoints.Count, Is.EqualTo(5));
        }
    }
}
