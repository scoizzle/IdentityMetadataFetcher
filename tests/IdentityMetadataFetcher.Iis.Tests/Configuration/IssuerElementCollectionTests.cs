using System;
using System.Linq;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Configuration;
using IdentityMetadataFetcher.Models;

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
            Assert.AreEqual(0, _collection.Count);
        }

        [Test]
        public void CanAddElement()
        {
            var element = new IssuerElement { Id = "issuer-1", Endpoint = "https://example.com/metadata", MetadataType = "Saml" };
            _collection.Add(element);

            Assert.AreEqual(1, _collection.Count);
        }

        [Test]
        public void CanRemoveElement()
        {
            var element = new IssuerElement { Id = "issuer-1", Endpoint = "https://example.com/metadata", MetadataType = "Saml" };
            _collection.Add(element);
            _collection.Remove(element);

            Assert.AreEqual(0, _collection.Count);
        }

        [Test]
        public void CanClearAllElements()
        {
            _collection.Add(new IssuerElement { Id = "issuer-1", MetadataType = "Saml" });
            _collection.Add(new IssuerElement { Id = "issuer-2", MetadataType = "Saml" });
            
            _collection.Clear();

            Assert.AreEqual(0, _collection.Count);
        }

        [Test]
        public void CanAccessElementByIndex()
        {
            var element = new IssuerElement { Id = "issuer-1", Endpoint = "https://example.com/metadata", MetadataType = "Saml" };
            _collection.Add(element);

            var retrieved = _collection[0];
            Assert.AreEqual("issuer-1", retrieved.Id);
        }

        [Test]
        public void CanAccessElementById()
        {
            var element = new IssuerElement { Id = "issuer-1", Endpoint = "https://example.com/metadata", MetadataType = "Saml" };
            _collection.Add(element);

            var retrieved = _collection["issuer-1"];
            Assert.AreEqual("issuer-1", retrieved.Id);
        }

        [Test]
        public void ToIssuerEndpoints_ReturnsAllElements()
        {
            _collection.Add(new IssuerElement { Id = "issuer-1", Endpoint = "https://example1.com/metadata", MetadataType = "Saml" });
            _collection.Add(new IssuerElement { Id = "issuer-2", Endpoint = "https://example2.com/metadata", MetadataType = "WsFed" });

            var endpoints = _collection.ToIssuerEndpoints().ToList();

            Assert.AreEqual(2, endpoints.Count);
            Assert.AreEqual("issuer-1", endpoints[0].Id);
            Assert.AreEqual("issuer-2", endpoints[1].Id);
        }

        [Test]
        public void ToIssuerEndpoints_ReturnsEmptyWhenNoElements()
        {
            var endpoints = _collection.ToIssuerEndpoints().ToList();

            Assert.AreEqual(0, endpoints.Count);
        }

        [Test]
        public void ToIssuerEndpoints_ConvertsMetadataTypesCorrectly()
        {
            _collection.Add(new IssuerElement { Id = "issuer-1", Endpoint = "https://example1.com/metadata", MetadataType = "Saml" });
            _collection.Add(new IssuerElement { Id = "issuer-2", Endpoint = "https://example2.com/metadata", MetadataType = "WsFed" });

            var endpoints = _collection.ToIssuerEndpoints().ToList();

            Assert.AreEqual(MetadataType.SAML, endpoints[0].MetadataType);
            Assert.AreEqual(MetadataType.WSFED, endpoints[1].MetadataType);
        }

        [Test]
        public void Contains_ReturnsTrueForExistingElement()
        {
            var element = new IssuerElement { Id = "issuer-1", MetadataType = "Saml" };
            _collection.Add(element);

            Assert.IsTrue(_collection.Contains(element));
        }

        [Test]
        public void Contains_ReturnsFalseForNonExistingElement()
        {
            var element = new IssuerElement { Id = "issuer-1", MetadataType = "Saml" };

            Assert.IsFalse(_collection.Contains(element));
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

            Assert.AreEqual(5, _collection.Count);
            var endpoints = _collection.ToIssuerEndpoints().ToList();
            Assert.AreEqual(5, endpoints.Count);
        }
    }
}
