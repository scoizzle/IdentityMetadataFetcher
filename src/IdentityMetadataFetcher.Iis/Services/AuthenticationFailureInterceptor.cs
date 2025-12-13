using Microsoft.IdentityModel.Tokens;
using System;
using System.Security.Cryptography.X509Certificates;

namespace IdentityMetadataFetcher.Iis.Services
{
    /// <summary>
    /// Analyzes authentication exceptions to determine if they are caused by 
    /// untrusted issuer certificates that might be resolved by metadata refresh.
    /// </summary>
    public class AuthenticationFailureInterceptor
    {
        /// <summary>
        /// Determines if an authentication exception is due to an untrusted certificate.
        /// </summary>
        /// <param name="exception">The authentication exception.</param>
        /// <returns>True if the failure is due to certificate trust issues; otherwise false.</returns>
        public bool IsCertificateTrustFailure(Exception exception)
        {
            if (exception == null)
                return false;

            // Check for SecurityTokenValidationException (WIF exceptions)
            if (exception is SecurityTokenValidationException)
            {
                var tokenException = exception as SecurityTokenValidationException;
                return IsCertificateRelated(tokenException);
            }

            // Check for SecurityTokenException base class
            if (exception is SecurityTokenException)
            {
                return IsCertificateRelated(exception);
            }

            // Check inner exceptions recursively
            if (exception.InnerException != null)
            {
                return IsCertificateTrustFailure(exception.InnerException);
            }

            return false;
        }

        /// <summary>
        /// Extracts the issuer identifier from an authentication exception.
        /// </summary>
        /// <param name="exception">The authentication exception.</param>
        /// <returns>The issuer identifier if found; otherwise null.</returns>
        public string ExtractIssuerFromException(Exception exception)
        {
            if (exception == null)
                return null;

            // Try to extract issuer from SecurityTokenException messages
            if (exception is SecurityTokenException)
            {
                // Common patterns in WIF exception messages:
                // "ID4037: The key needed to verify the signature could not be resolved from the following security key identifier..."
                // "ID4175: The issuer of the security token was not recognized by the IssuerNameRegistry..."
                // Parse exception message for issuer URIs
                var message = exception.Message;
                
                // Look for issuer URI patterns (https:// or urn:)
                var issuerMatch = System.Text.RegularExpressions.Regex.Match(
                    message, 
                    @"(?:issuer|from)\s+['""]?(https?://[^\s'""<>]+|urn:[^\s'""<>]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (issuerMatch.Success && issuerMatch.Groups.Count > 1)
                {
                    return issuerMatch.Groups[1].Value;
                }
            }

            // Check inner exception
            if (exception.InnerException != null)
            {
                return ExtractIssuerFromException(exception.InnerException);
            }

            return null;
        }

        /// <summary>
        /// Determines if a SecurityTokenException is certificate-related.
        /// </summary>
        private bool IsCertificateRelated(Exception exception)
        {
            var message = exception.Message;
            
            // Common WIF error IDs and messages related to certificate/signature issues:
            // ID4022: The key needed to decrypt the token could not be resolved from the token
            // ID4037: The key needed to verify the signature could not be resolved
            // ID4175: The issuer of the security token was not recognized
            // ID4257: The key wrap token provided is not a X509SecurityToken
            // ID4252: X509SecurityToken cannot be validated
            
            return message.Contains("ID4037") ||  // Signature verification failed
                   message.Contains("ID4022") ||  // Decryption key not found
                   message.Contains("ID4175") ||  // Issuer not recognized
                   message.Contains("ID4257") ||  // X509 token issue
                   message.Contains("ID4252") ||  // X509 validation failed
                   message.Contains("signature") ||
                   message.Contains("certificate") ||
                   message.Contains("X509") ||
                   message.Contains("key needed") ||
                   message.Contains("issuer");
        }
    }
}
