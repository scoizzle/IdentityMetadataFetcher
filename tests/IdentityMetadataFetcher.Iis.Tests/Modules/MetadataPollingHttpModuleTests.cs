using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Configuration;
using IdentityMetadataFetcher.Iis.Modules;
using IdentityMetadataFetcher.Iis.Tests.Mocks;

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
            Assert.IsNotNull(_module);
            Assert.IsInstanceOf<IHttpModule>(_module);
        }

        [Test]
        public void MetadataCache_IsAccessible()
        {
            // Note: This would require proper Web.config setup in a real test
            // For unit testing, we verify the property exists
            var type = typeof(MetadataPollingHttpModule);
            var cacheProperty = type.GetProperty("MetadataCache");
            
            Assert.IsNotNull(cacheProperty);
        }

        [Test]
        public void PollingService_IsAccessible()
        {
            // Note: This would require proper Web.config setup in a real test
            // For unit testing, we verify the property exists
            var type = typeof(MetadataPollingHttpModule);
            var serviceProperty = type.GetProperty("PollingService");
            
            Assert.IsNotNull(serviceProperty);
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
            Assert.IsTrue(typeof(IHttpModule).IsAssignableFrom(typeof(MetadataPollingHttpModule)));
        }
    }
}
