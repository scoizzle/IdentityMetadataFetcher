using IdentityMetadataFetcher.Iis.Configuration;
using NUnit.Framework;
using System;
using System.Configuration;

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
            Assert.That(_section.Enabled, Is.True);
        }

        [Test]
        public void PollingIntervalMinutes_DefaultValueIs60()
        {
            Assert.That(_section.PollingIntervalMinutes, Is.EqualTo(60));
        }

        [Test]
        public void HttpTimeoutSeconds_DefaultValueIs30()
        {
            Assert.That(_section.HttpTimeoutSeconds, Is.EqualTo(30));
        }

        [Test]
        public void ValidateServerCertificate_DefaultValueIsTrue()
        {
            Assert.That(_section.ValidateServerCertificate, Is.True);
        }

        [Test]
        public void MaxRetries_DefaultValueIs2()
        {
            Assert.That(_section.MaxRetries, Is.EqualTo(2));
        }

        [Test]
        public void PollingIntervalMinutes_CanSetValidValue()
        {
            _section.PollingIntervalMinutes = 120;
            Assert.That(_section.PollingIntervalMinutes, Is.EqualTo(120));
        }

        [Test]
        public void PollingIntervalMinutes_MinimumValueIs1()
        {
            _section.PollingIntervalMinutes = 1;
            Assert.That(_section.PollingIntervalMinutes, Is.EqualTo(1));
        }

        [Test]
        public void PollingIntervalMinutes_MaximumValueIs10080()
        {
            _section.PollingIntervalMinutes = 10080;
            Assert.That(_section.PollingIntervalMinutes, Is.EqualTo(10080));
        }

        [Test]
        public void HttpTimeoutSeconds_MinimumValueIs5()
        {
            _section.HttpTimeoutSeconds = 5;
            Assert.That(_section.HttpTimeoutSeconds, Is.EqualTo(5));
        }

        [Test]
        public void HttpTimeoutSeconds_MaximumValueIs300()
        {
            _section.HttpTimeoutSeconds = 300;
            Assert.That(_section.HttpTimeoutSeconds, Is.EqualTo(300));
        }

        [Test]
        public void MaxRetries_MinimumValueIs0()
        {
            _section.MaxRetries = 0;
            Assert.That(_section.MaxRetries, Is.EqualTo(0));
        }

        [Test]
        public void MaxRetries_MaximumValueIs5()
        {
            _section.MaxRetries = 5;
            Assert.That(_section.MaxRetries, Is.EqualTo(5));
        }

        [Test]
        public void Issuers_ReturnsCollectionOfIssuers()
        {
            var issuers = _section.Issuers;
            Assert.That(issuers, Is.Not.Null);
            Assert.That(issuers, Is.InstanceOf<IssuerElementCollection>());
        }

        [Test]
        public void ValidateServerCertificate_CanBeSetToFalse()
        {
            _section.ValidateServerCertificate = false;
            Assert.That(_section.ValidateServerCertificate, Is.False);
        }

        [Test]
        public void AutoApplyIdentityModel_DefaultValueIsFalse()
        {
            Assert.That(_section.AutoApplyIdentityModel, Is.False);
        }

        [Test]
        public void AutoApplyIdentityModel_CanBeSetToTrue()
        {
            _section.AutoApplyIdentityModel = true;
            Assert.That(_section.AutoApplyIdentityModel, Is.True);
        }
    }
}
