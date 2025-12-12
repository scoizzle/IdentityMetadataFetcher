using System;
using System.Configuration;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Configuration;

namespace IdentityMetadataFetcher.Iis.Tests.Configuration
{
    [TestFixture]
    public class MetadataPollingConfigurationSectionTests
    {
        private MetadataPollingConfigurationSection _section;

        [SetUp]
        public void Setup()
        {
            _section = new MetadataPollingConfigurationSection();
        }

        [Test]
        public void Enabled_DefaultValueIsTrue()
        {
            Assert.IsTrue(_section.Enabled);
        }

        [Test]
        public void PollingIntervalMinutes_DefaultValueIs60()
        {
            Assert.AreEqual(60, _section.PollingIntervalMinutes);
        }

        [Test]
        public void HttpTimeoutSeconds_DefaultValueIs30()
        {
            Assert.AreEqual(30, _section.HttpTimeoutSeconds);
        }

        [Test]
        public void ValidateServerCertificate_DefaultValueIsTrue()
        {
            Assert.IsTrue(_section.ValidateServerCertificate);
        }

        [Test]
        public void MaxRetries_DefaultValueIs2()
        {
            Assert.AreEqual(2, _section.MaxRetries);
        }

        [Test]
        public void PollingIntervalMinutes_CanSetValidValue()
        {
            _section.PollingIntervalMinutes = 120;
            Assert.AreEqual(120, _section.PollingIntervalMinutes);
        }

        [Test]
        public void PollingIntervalMinutes_MinimumValueIs1()
        {
            _section.PollingIntervalMinutes = 1;
            Assert.AreEqual(1, _section.PollingIntervalMinutes);
        }

        [Test]
        public void PollingIntervalMinutes_MaximumValueIs10080()
        {
            _section.PollingIntervalMinutes = 10080;
            Assert.AreEqual(10080, _section.PollingIntervalMinutes);
        }

        [Test]
        public void HttpTimeoutSeconds_MinimumValueIs5()
        {
            _section.HttpTimeoutSeconds = 5;
            Assert.AreEqual(5, _section.HttpTimeoutSeconds);
        }

        [Test]
        public void HttpTimeoutSeconds_MaximumValueIs300()
        {
            _section.HttpTimeoutSeconds = 300;
            Assert.AreEqual(300, _section.HttpTimeoutSeconds);
        }

        [Test]
        public void MaxRetries_MinimumValueIs0()
        {
            _section.MaxRetries = 0;
            Assert.AreEqual(0, _section.MaxRetries);
        }

        [Test]
        public void MaxRetries_MaximumValueIs5()
        {
            _section.MaxRetries = 5;
            Assert.AreEqual(5, _section.MaxRetries);
        }

        [Test]
        public void Issuers_ReturnsCollectionOfIssuers()
        {
            var issuers = _section.Issuers;
            Assert.IsNotNull(issuers);
            Assert.IsInstanceOf<IssuerElementCollection>(issuers);
        }

        [Test]
        public void ValidateServerCertificate_CanBeSetToFalse()
        {
            _section.ValidateServerCertificate = false;
            Assert.IsFalse(_section.ValidateServerCertificate);
        }

        [Test]
        public void AutoApplyIdentityModel_DefaultValueIsFalse()
        {
            Assert.IsFalse(_section.AutoApplyIdentityModel);
        }

        [Test]
        public void AutoApplyIdentityModel_CanBeSetToTrue()
        {
            _section.AutoApplyIdentityModel = true;
            Assert.IsTrue(_section.AutoApplyIdentityModel);
        }
    }
}
