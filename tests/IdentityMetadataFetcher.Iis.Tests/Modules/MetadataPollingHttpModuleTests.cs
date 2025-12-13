using IdentityMetadataFetcher.Iis.Configuration;
using IdentityMetadataFetcher.Iis.Modules;
using IdentityMetadataFetcher.Iis.Tests.Mocks;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace IdentityMetadataFetcher.Iis.Tests.Modules
{
    [TestFixture]
    public class MetadataPollingHttpModuleTests
    {
        private MetadataPollingHttpModule _module;

        [SetUp]
        public void Setup()
        {
            _module = new MetadataPollingHttpModule();
        }

        [Test]
        public void CanBeInstantiated()
        {
            Assert.That(_module, Is.Not.Null);
            Assert.That(_module, Is.InstanceOf<IHttpModule>());
        }

        [Test]
        public void MetadataCache_IsAccessible()
        {
            // Note: This would require proper Web.config setup in a real test
            // For unit testing, we verify the property exists
            var type = typeof(MetadataPollingHttpModule);
            var cacheProperty = type.GetProperty("MetadataCache");
            
            Assert.That(cacheProperty, Is.Not.Null);
        }

        [Test]
        public void PollingService_IsAccessible()
        {
            // Note: This would require proper Web.config setup in a real test
            // For unit testing, we verify the property exists
            var type = typeof(MetadataPollingHttpModule);
            var serviceProperty = type.GetProperty("PollingService");
            
            Assert.That(serviceProperty, Is.Not.Null);
        }

        [Test]
        public void Dispose_MethodExists()
        {
            // Verify that Dispose can be called without error
            Assert.DoesNotThrow(() => _module.Dispose());
        }

        [Test]
        public void IHttpModule_ImplementsInterface()
        {
            Assert.That(typeof(IHttpModule).IsAssignableFrom(typeof(MetadataPollingHttpModule)), Is.True);
        }
    }
}
