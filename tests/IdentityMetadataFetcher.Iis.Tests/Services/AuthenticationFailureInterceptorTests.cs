using System;
using System.IdentityModel.Tokens;
using NUnit.Framework;
using IdentityMetadataFetcher.Iis.Services;

namespace IdentityMetadataFetcher.Iis.Tests.Services
{
    [TestFixture]
    public class AuthenticationFailureInterceptorTests
    {
        private AuthenticationFailureInterceptor _interceptor;

        [SetUp]
        public void Setup()
        {
            _interceptor = new AuthenticationFailureInterceptor();
        }

        [Test]
        public void IsCertificateTrustFailure_WithNull_ReturnsFalse()
        {
            var result = _interceptor.IsCertificateTrustFailure(null);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithSecurityTokenValidationException_ID4037_ReturnsTrue()
        {
            var exception = new SecurityTokenValidationException("ID4037: The key needed to verify the signature could not be resolved");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithSecurityTokenValidationException_ID4175_ReturnsTrue()
        {
            var exception = new SecurityTokenValidationException("ID4175: The issuer of the security token was not recognized");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithSecurityTokenValidationException_ID4022_ReturnsTrue()
        {
            var exception = new SecurityTokenValidationException("ID4022: The key needed to decrypt the token could not be resolved");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithSignatureMessage_ReturnsTrue()
        {
            var exception = new SecurityTokenException("Signature verification failed");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithCertificateMessage_ReturnsTrue()
        {
            var exception = new SecurityTokenException("Invalid certificate");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithX509Message_ReturnsTrue()
        {
            var exception = new SecurityTokenException("X509 validation failed");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithUnrelatedSecurityTokenException_ReturnsFalse()
        {
            var exception = new SecurityTokenException("Token expired");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            // This should return true because we check for generic "issuer" in the message
            // but if the message doesn't contain any of our keywords, it would return false
            Assert.IsFalse(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithNonSecurityTokenException_ReturnsFalse()
        {
            var exception = new InvalidOperationException("Some other error");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithInnerException_ChecksInnerException()
        {
            var innerException = new SecurityTokenValidationException("ID4037: Signature key not found");
            var outerException = new Exception("Outer exception", innerException);
            
            var result = _interceptor.IsCertificateTrustFailure(outerException);
            Assert.IsTrue(result);
        }

        [Test]
        public void ExtractIssuerFromException_WithNull_ReturnsNull()
        {
            var result = _interceptor.ExtractIssuerFromException(null);
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractIssuerFromException_WithHttpsIssuerUrl_ExtractsIssuer()
        {
            var exception = new SecurityTokenException("Error from issuer 'https://sts.example.com'");
            var result = _interceptor.ExtractIssuerFromException(exception);
            Assert.AreEqual("https://sts.example.com", result);
        }

        [Test]
        public void ExtractIssuerFromException_WithUrnIssuer_ExtractsIssuer()
        {
            var exception = new SecurityTokenException("Error from issuer \"urn:example:issuer:123\"");
            var result = _interceptor.ExtractIssuerFromException(exception);
            Assert.AreEqual("urn:example:issuer:123", result);
        }

        [Test]
        public void ExtractIssuerFromException_WithInnerException_ExtractsFromInner()
        {
            var innerException = new SecurityTokenException("Issuer https://login.example.com failed");
            var outerException = new Exception("Outer", innerException);
            
            var result = _interceptor.ExtractIssuerFromException(outerException);
            Assert.AreEqual("https://login.example.com", result);
        }

        [Test]
        public void ExtractIssuerFromException_WithNoIssuerPattern_ReturnsNull()
        {
            var exception = new SecurityTokenException("Generic error without issuer");
            var result = _interceptor.ExtractIssuerFromException(exception);
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractIssuerFromException_WithNonSecurityTokenException_ReturnsNull()
        {
            var exception = new InvalidOperationException("Some error");
            var result = _interceptor.ExtractIssuerFromException(exception);
            Assert.IsNull(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithKeyNeededMessage_ReturnsTrue()
        {
            var exception = new SecurityTokenException("The key needed for validation was not found");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsCertificateTrustFailure_WithIssuerMessage_ReturnsTrue()
        {
            var exception = new SecurityTokenException("The issuer was not recognized");
            var result = _interceptor.IsCertificateTrustFailure(exception);
            Assert.IsTrue(result);
        }

        [Test]
        public void ExtractIssuerFromException_CaseInsensitive_ExtractsIssuer()
        {
            var exception = new SecurityTokenException("Error FROM ISSUER 'https://sts.example.com'");
            var result = _interceptor.ExtractIssuerFromException(exception);
            Assert.AreEqual("https://sts.example.com", result);
        }

        [Test]
        public void ExtractIssuerFromException_WithQuotes_ExtractsIssuer()
        {
            var exception = new SecurityTokenException("Error from issuer \"https://sts.example.com\"");
            var result = _interceptor.ExtractIssuerFromException(exception);
            Assert.AreEqual("https://sts.example.com", result);
        }

        [Test]
        public void ExtractIssuerFromException_WithoutQuotes_ExtractsIssuer()
        {
            var exception = new SecurityTokenException("Error from issuer https://sts.example.com in the request");
            var result = _interceptor.ExtractIssuerFromException(exception);
            Assert.AreEqual("https://sts.example.com", result);
        }
    }
}
