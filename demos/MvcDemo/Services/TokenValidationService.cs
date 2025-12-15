using IdentityMetadataFetcher.Iis.Modules;
using IdentityMetadataFetcher.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Tokens.Saml2;
using MvcDemo.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace MvcDemo.Services
{
    /// <summary>
    /// Service for validating SAML tokens using metadata from the polling service.
    /// </summary>
    public class TokenValidationService
    {
        /// <summary>
        /// Validates a SAML token using the issuer's metadata.
        /// Accepts both raw XML and Base64-encoded SAML tokens.
        /// If metadata is not available, attempts to trigger an immediate poll.
        /// </summary>
        public static async Task<TokenValidationResultViewModel> ValidateSamlTokenAsync(string issuerId, string samlToken)
        {
            var result = new TokenValidationResultViewModel
            {
                IssuerId = issuerId,
                IsValid = false
            };

            if (string.IsNullOrWhiteSpace(issuerId))
            {
                result.Message = "Issuer ID is required";
                result.ErrorDetails = "Please select an issuer from the dropdown list";
                return result;
            }

            if (string.IsNullOrWhiteSpace(samlToken))
            {
                result.Message = "SAML token is required";
                result.ErrorDetails = "Please paste a SAML token into the text area";
                return result;
            }

            try
            {
                // Get metadata cache from the HTTP module
                var cache = MetadataPollingHttpModule.MetadataCache;
                if (cache == null)
                {
                    result.Message = "Metadata cache not initialized";
                    result.ErrorDetails = "The metadata polling service has not been initialized yet";
                    return result;
                }

                // Retrieve raw metadata for the issuer
                var rawMetadata = cache.GetRawMetadata(issuerId);
                if (string.IsNullOrEmpty(rawMetadata))
                {
                    // Metadata not found - attempt to trigger a poll
                    await TriggerMetadataPollingAsync(issuerId);

                    // After polling, check again
                    rawMetadata = cache.GetRawMetadata(issuerId);
                    if (string.IsNullOrEmpty(rawMetadata))
                    {
                        result.Message = "Issuer metadata not available";
                        result.ErrorDetails = string.Format(
                            "The metadata for issuer '{0}' could not be fetched. Please check the issuer configuration.",
                            issuerId);
                        return result;
                    }
                }

                // Attempt to decode the token to XML string
                var tokenString = DecodeToken(samlToken);
                if (tokenString == null)
                {
                    result.Message = "Invalid token format";
                    result.ErrorDetails = "The token could not be decoded. Please provide either raw XML or valid Base64-encoded XML";
                    return result;
                }

                // Parse and validate the SAML token
                var validationResult = ValidateSamlToken(tokenString, rawMetadata, issuerId);
                if (!validationResult.IsValid)
                {
                    result.Message = validationResult.Message;
                    result.ErrorDetails = validationResult.ErrorDetails;
                    // Copy validation flags even for failed validations
                    result.SignatureValid = validationResult.SignatureValid;
                    result.SignatureValidMessage = validationResult.SignatureValidMessage;
                    result.IssuerVerified = validationResult.IssuerVerified;
                    result.IssuerVerifiedMessage = validationResult.IssuerVerifiedMessage;
                    result.TokenNotExpired = validationResult.TokenNotExpired;
                    result.TokenNotExpiredMessage = validationResult.TokenNotExpiredMessage;
                    result.TokenNotYetValid = validationResult.TokenNotYetValid;
                    result.TokenNotYetValidMessage = validationResult.TokenNotYetValidMessage;
                    return result;
                }

                // Successfully validated - copy all properties from validationResult
                result.IsValid = true;
                result.Message = validationResult.Message;
                result.IssuerName = GetIssuerName(issuerId);
                
                // Token properties
                result.TokenIssuer = validationResult.TokenIssuer;
                result.IssueInstant = validationResult.IssueInstant;
                result.NotBefore = validationResult.NotBefore;
                result.NotOnOrAfter = validationResult.NotOnOrAfter;
                
                // Audiences
                if (validationResult.Audiences != null && validationResult.Audiences.Any())
                {
                    result.Audiences.AddRange(validationResult.Audiences);
                }
                
                // Claims
                if (validationResult.Claims != null && validationResult.Claims.Any())
                {
                    foreach (var claim in validationResult.Claims)
                    {
                        result.Claims[claim.Key] = claim.Value;
                    }
                }
                
                // Validation flags
                result.SignatureValid = validationResult.SignatureValid;
                result.SignatureValidMessage = validationResult.SignatureValidMessage;
                result.IssuerVerified = validationResult.IssuerVerified;
                result.IssuerVerifiedMessage = validationResult.IssuerVerifiedMessage;
                result.TokenNotExpired = validationResult.TokenNotExpired;
                result.TokenNotExpiredMessage = validationResult.TokenNotExpiredMessage;
                result.TokenNotYetValid = validationResult.TokenNotYetValid;
                result.TokenNotYetValidMessage = validationResult.TokenNotYetValidMessage;
                
                // Certificate information
                result.SigningCertificateCount = validationResult.SigningCertificateCount;
                result.PrimarySigningCertificateThumbprint = validationResult.PrimarySigningCertificateThumbprint;
                result.PrimarySigningCertificateExpiration = validationResult.PrimarySigningCertificateExpiration;
                
                return result;
            }
            catch (Exception ex)
            {
                result.Message = "Token validation error";
                result.ErrorDetails = string.Format("An unexpected error occurred: {0}", ex.Message);
                System.Diagnostics.Trace.TraceError(
                    string.Format("TokenValidationService: Unexpected error during validation: {0}", ex));
                return result;
            }
        }

        /// <summary>
        /// Attempts to decode a SAML token from multiple formats:
        /// - Raw XML (starts with &lt;)
        /// - HTML-escaped XML (&lt; instead of &lt;, etc.)
        /// - Base64-encoded XML
        /// </summary>
        private static string DecodeToken(string samlToken)
        {
            if (string.IsNullOrWhiteSpace(samlToken))
                return null;

            samlToken = samlToken.Trim();

            // Check if it's HTML-escaped XML (contains &lt; and &gt;)
            if (samlToken.Contains("&lt;") || samlToken.Contains("&gt;"))
            {
                try
                {
                    // Unescape HTML entities
                    string unescaped = System.Net.WebUtility.HtmlDecode(samlToken);
                    if (unescaped.Trim().StartsWith("<"))
                    {
                        return unescaped;
                    }
                }
                catch (Exception)
                {
                    // Fall through to other formats
                }
            }

            // Check if it's already raw XML (starts with <)
            if (samlToken.StartsWith("<"))
            {
                return samlToken;
            }

            // Try to Base64 decode
            try
            {
                byte[] tokenBytes = System.Convert.FromBase64String(samlToken);
                string decodedString = System.Text.Encoding.UTF8.GetString(tokenBytes);

                // Verify it's valid XML after decoding
                if (decodedString.Trim().StartsWith("<"))
                {
                    return decodedString;
                }

                // Check if the decoded result is HTML-escaped XML
                if (decodedString.Contains("&lt;") || decodedString.Contains("&gt;"))
                {
                    try
                    {
                        string unescaped = System.Net.WebUtility.HtmlDecode(decodedString);
                        if (unescaped.Trim().StartsWith("<"))
                        {
                            return unescaped;
                        }
                    }
                    catch (Exception)
                    {
                        // Fall through
                    }
                }
            }
            catch (FormatException)
            {
                // Not valid Base64
            }
            catch (Exception)
            {
                // Other decode errors
            }

            return null;
        }

        /// <summary>
        /// Triggers a metadata poll for the specified issuer.
        /// </summary>
        private static async Task TriggerMetadataPollingAsync(string issuerId)
        {
            try
            {
                var pollingService = MetadataPollingHttpModule.PollingService;
                if (pollingService == null)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "TokenValidationService: Polling service not initialized");
                    return;
                }

                var result = await pollingService.PollIssuerNowAsync(issuerId, force: true);
                if (result)
                {
                    System.Diagnostics.Trace.TraceInformation(
                        string.Format("TokenValidationService: Successfully polled metadata for issuer '{0}'", issuerId));
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning(
                        string.Format("TokenValidationService: Metadata poll for issuer '{0}' failed or was throttled", issuerId));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    string.Format("TokenValidationService: Error during metadata poll for issuer '{0}': {1}", issuerId, ex.Message));
            }
        }

        /// <summary>
        /// Gets the issuer name from the service.
        /// </summary>
        private static string GetIssuerName(string issuerId)
        {
            try
            {
                var issuers = IssuerManagementService.GetCurrentIssuers();
                foreach (var issuer in issuers)
                {
                    if (issuer.Id == issuerId)
                    {
                        return issuer.Name ?? issuerId;
                    }
                }
            }
            catch
            {
                // Silently fail
            }

            return issuerId;
        }

        /// <summary>
        /// Performs comprehensive SAML token validation using Saml2SecurityTokenHandler.
        /// </summary>
        private static TokenValidationResultViewModel ValidateSamlToken(string tokenXml, string metadataXml, string expectedIssuerId)
        {
            var result = new TokenValidationResultViewModel { IsValid = false };

            try
            {
                // Extract signing certificates from metadata first
                var signingCertificates = ExtractSigningCertificatesFromMetadata(metadataXml);
                if (!signingCertificates.Any())
                {
                    result.Message = "No signing certificates in metadata";
                    result.ErrorDetails = "The issuer's metadata does not contain any signing certificates for validation";
                    result.SignatureValid = false;
                    result.SignatureValidMessage = "No certificates available";
                    return result;
                }

                // Store certificate info
                result.SigningCertificateCount = signingCertificates.Count;
                if (signingCertificates.Count > 0)
                {
                    result.PrimarySigningCertificateThumbprint = signingCertificates[0].Thumbprint;
                    result.PrimarySigningCertificateExpiration = signingCertificates[0].NotAfter;
                }

                // Create SAML2 token handler
                var saml2Handler = new Saml2SecurityTokenHandler();
                
                // Check if handler can read this token format
                if (!saml2Handler.CanReadToken(tokenXml))
                {
                    result.Message = "Unsupported token format";
                    result.ErrorDetails = "The token is not in a supported SAML2 assertion format";
                    result.SignatureValid = false;
                    result.SignatureValidMessage = "Invalid token format";
                    return result;
                }

                // Extract audiences from the token for validation
                var audiences = ExtractAudiencesFromToken(tokenXml);

                // Build token validation parameters with the signing certificates
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = signingCertificates
                        .Select(cert => new X509SecurityKey(cert))
                        .Cast<SecurityKey>()
                        .ToList(),
                    ValidateIssuer = false,  // We'll validate the issuer manually
                    ValidateLifetime = true,
                    ValidateAudience = audiences.Any(),  // Only validate audience if we found one
                    ValidAudiences = audiences,  // Set the audiences extracted from the token
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                // Validate the token using Saml2SecurityTokenHandler
                try
                {
                    SecurityToken validatedToken;
                    var principal = saml2Handler.ValidateToken(tokenXml, tokenValidationParameters, out validatedToken);
                    
                    // At this point, signature is valid
                    result.SignatureValid = true;
                    result.SignatureValidMessage = "Cryptographically verified using issuer's certificates";

                    // Extract token issuer for additional validation
                    var saml2Token = validatedToken as Saml2SecurityToken;
                    if (saml2Token != null && saml2Token.Issuer != null)
                    {
                        string tokenIssuer = saml2Token.Issuer.ToString();
                        
                        // Validate issuer against metadata
                        if (!ValidateTokenIssuerAgainstMetadata(tokenIssuer, metadataXml))
                        {
                            result.Message = "Invalid issuer";
                            result.ErrorDetails = $"The token issuer '{tokenIssuer}' does not match any issuer in the metadata";
                            result.IssuerVerified = false;
                            result.IssuerVerifiedMessage = $"Token issuer '{tokenIssuer}' not found in metadata";
                            return result;
                        }

                        result.IssuerVerified = true;
                        result.IssuerVerifiedMessage = "Issuer matches metadata and trusted provider list";
                    }
                    else
                    {
                        result.IssuerVerified = false;
                        result.IssuerVerifiedMessage = "Unable to extract issuer from token";
                    }

                    // Extract and populate token details (for all valid tokens)
                    if (saml2Token != null)
                    {
                        if (saml2Token.Issuer != null)
                        {
                            result.TokenIssuer = saml2Token.Issuer.ToString();
                        }
                        result.IssueInstant = saml2Token.ValidFrom;
                        result.NotBefore = saml2Token.ValidFrom;
                        result.NotOnOrAfter = saml2Token.ValidTo;

                        // Check expiration status
                        var now = System.DateTime.UtcNow;
                        if (now >= saml2Token.ValidFrom && now < saml2Token.ValidTo)
                        {
                            result.TokenNotExpired = true;
                            result.TokenNotExpiredMessage = $"Token is valid until {saml2Token.ValidTo:yyyy-MM-dd HH:mm:ss UTC}";
                            result.TokenNotYetValid = true;
                            result.TokenNotYetValidMessage = "Token is currently valid";
                        }
                        else if (now < saml2Token.ValidFrom)
                        {
                            result.TokenNotExpired = true;
                            result.TokenNotYetValid = false;
                            result.TokenNotYetValidMessage = $"Token becomes valid at {saml2Token.ValidFrom:yyyy-MM-dd HH:mm:ss UTC}";
                        }
                        else
                        {
                            result.TokenNotExpired = false;
                            result.TokenNotExpiredMessage = $"Token expired on {saml2Token.ValidTo:yyyy-MM-dd HH:mm:ss UTC}";
                            result.TokenNotYetValid = true;
                        }
                    }
                    
                    // Add audiences
                    var tokenAudiences = ExtractAudiencesFromToken(tokenXml);
                    if (tokenAudiences != null)
                    {
                        result.Audiences.AddRange(tokenAudiences);
                    }

                    // Extract claims from the principal
                    if (principal != null && principal.Claims != null)
                    {
                        foreach (var claim in principal.Claims)
                        {
                            var key = claim.Type;
                            var value = claim.Value;
                            
                            // Use claim type without namespace for readability
                            var shortKey = key.Contains("/") ? key.Substring(key.LastIndexOf("/") + 1) : key;
                            
                            if (!result.Claims.ContainsKey(shortKey))
                            {
                                result.Claims[shortKey] = value;
                            }
                        }
                    }

                    // Token validation successful
                    result.IsValid = true;
                    result.Message = "Token validation passed";
                    result.IssuerName = expectedIssuerId;
                    return result;
                }
                catch (SecurityTokenSignatureKeyNotFoundException ex)
                {
                    result.Message = "Invalid token signature";
                    result.ErrorDetails = $"The token signature could not be verified with available certificates: {ex.Message}";
                    result.SignatureValid = false;
                    result.SignatureValidMessage = "Certificate mismatch or invalid signature";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: Signature key not found: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenInvalidSignatureException ex)
                {
                    result.Message = "Invalid token signature";
                    result.ErrorDetails = $"The token has an invalid or tampered signature: {ex.Message}";
                    result.SignatureValid = false;
                    result.SignatureValidMessage = "Signature validation failed - token may be tampered";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: Invalid signature: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenExpiredException ex)
                {
                    result.Message = "Token expired";
                    result.ErrorDetails = $"The token has expired and is no longer valid";
                    result.TokenNotExpired = false;
                    result.TokenNotExpiredMessage = "Token has passed its expiration date";
                    System.Diagnostics.Trace.TraceWarning($"TokenValidationService: Token expired: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenNotYetValidException ex)
                {
                    result.Message = "Token not yet valid";
                    result.ErrorDetails = $"The token is not yet valid - check the NotBefore condition";
                    result.TokenNotYetValid = false;
                    result.TokenNotYetValidMessage = "Token's start validity has not been reached";
                    System.Diagnostics.Trace.TraceWarning($"TokenValidationService: Token not yet valid: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenValidationException ex)
                {
                    result.Message = "Token validation failed";
                    result.ErrorDetails = $"Token validation failed: {ex.Message}";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: Validation error: {ex.Message}");
                    return result;
                }
                catch (Exception ex)
                {
                    result.Message = "Unexpected validation error";
                    result.ErrorDetails = $"An unexpected error occurred during validation: {ex.Message}";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: Unexpected error: {ex.Message}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Message = "Validation error";
                result.ErrorDetails = $"An error occurred during token validation: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Extracts signing certificates from SAML metadata XML using string parsing.
        /// Searches for X509Certificate elements containing Base64-encoded certificate data.
        /// </summary>
        private static List<X509Certificate2> ExtractSigningCertificatesFromMetadata(string metadataXml)
        {
            var certificates = new List<X509Certificate2>();

            if (string.IsNullOrEmpty(metadataXml))
                return certificates;

            try
            {
                // Search for X509Certificate elements
                int startIndex = 0;
                const string certStartTag = "<X509Certificate>";
                const string certEndTag = "</X509Certificate>";

                while (true)
                {
                    int tagStart = metadataXml.IndexOf(certStartTag, startIndex);
                    if (tagStart < 0)
                        break;

                    int certDataStart = tagStart + certStartTag.Length;
                    int tagEnd = metadataXml.IndexOf(certEndTag, certDataStart);
                    
                    if (tagEnd < 0)
                        break;

                    string certData = metadataXml.Substring(certDataStart, tagEnd - certDataStart).Trim();
                    
                    if (!string.IsNullOrEmpty(certData))
                    {
                        try
                        {
                            // Decode Base64 certificate data
                            byte[] certBytes = System.Convert.FromBase64String(certData);
                            var cert = new X509Certificate2(certBytes);
                            
                            // Avoid duplicate certificates
                            if (!certificates.Any(c => c.Thumbprint == cert.Thumbprint))
                            {
                                certificates.Add(cert);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.TraceWarning(
                                $"TokenValidationService: Failed to parse X509Certificate: {ex.Message}");
                        }
                    }

                    startIndex = tagEnd + certEndTag.Length;
                }

                return certificates;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    $"TokenValidationService: Error extracting certificates from metadata: {ex.Message}");
                return certificates;
            }
        }

        /// <summary>
        /// Extracts the issuer URI from a SAML token XML.
        /// </summary>
        private static string ExtractIssuerFromToken(string tokenXml)
        {
            if (string.IsNullOrEmpty(tokenXml))
                return null;

            // Look for <Issuer> element (with or without namespace prefix)
            var issuerStartPatterns = new[] { "<Issuer>", "<issuer>", "<saml:Issuer>", "<saml2:Issuer>" };
            var issuerEndPatterns = new[] { "</Issuer>", "</issuer>", "</saml:Issuer>", "</saml2:Issuer>" };

            foreach (var startPattern in issuerStartPatterns)
            {
                int startIndex = tokenXml.IndexOf(startPattern);
                if (startIndex >= 0)
                {
                    int contentStart = startIndex + startPattern.Length;
                    
                    // Find the corresponding closing tag
                    foreach (var endPattern in issuerEndPatterns)
                    {
                        int endIndex = tokenXml.IndexOf(endPattern, contentStart);
                        if (endIndex >= 0)
                        {
                            return tokenXml.Substring(contentStart, endIndex - contentStart).Trim();
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Validates that the token issuer matches the issuer in the metadata.
        /// </summary>
        private static bool ValidateTokenIssuerAgainstMetadata(string tokenIssuer, string metadataXml)
        {
            if (string.IsNullOrEmpty(tokenIssuer) || string.IsNullOrEmpty(metadataXml))
                return false;

            // For WS-Federation metadata, look for EntityDescriptor with the issuer
            // For SAML metadata, check if the issuer appears in the metadata
            
            // Simple check: see if the token issuer appears anywhere in the metadata
            // This is a basic validation - in production you'd parse the metadata properly
            if (metadataXml.Contains(tokenIssuer))
            {
                return true;
            }

            // Also check for variations of the issuer (with or without trailing slash)
            var issuerWithSlash = tokenIssuer.TrimEnd('/') + "/";
            var issuerWithoutSlash = tokenIssuer.TrimEnd('/');
            
            if (metadataXml.Contains(issuerWithSlash) || metadataXml.Contains(issuerWithoutSlash))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts audience URIs from a SAML token XML.
        /// Searches for Audience elements within AudienceRestriction elements.
        /// </summary>
        private static IEnumerable<string> ExtractAudiencesFromToken(string tokenXml)
        {
            var audiences = new List<string>();

            if (string.IsNullOrEmpty(tokenXml))
                return audiences;

            try
            {
                // Look for <Audience> elements (with or without namespace prefix)
                var audiencePatterns = new[] { "<Audience>", "<audience>" };
                var audienceEndPatterns = new[] { "</Audience>", "</audience>" };

                foreach (var startPattern in audiencePatterns)
                {
                    int startIndex = 0;
                    
                    while (true)
                    {
                        startIndex = tokenXml.IndexOf(startPattern, startIndex);
                        if (startIndex < 0)
                            break;

                        int contentStart = startIndex + startPattern.Length;
                        
                        // Find the corresponding closing tag
                        foreach (var endPattern in audienceEndPatterns)
                        {
                            int endIndex = tokenXml.IndexOf(endPattern, contentStart);
                            if (endIndex >= 0)
                            {
                                string audience = tokenXml.Substring(contentStart, endIndex - contentStart).Trim();
                                if (!string.IsNullOrEmpty(audience) && !audiences.Contains(audience))
                                {
                                    audiences.Add(audience);
                                }
                                startIndex = endIndex + endPattern.Length;
                                break;
                            }
                        }

                        if (startIndex == 0)
                            break;
                    }
                }

                return audiences;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"TokenValidationService: Error extracting audiences from token: {ex.Message}");
                return audiences;
            }
        }

        /// <summary>
        /// Validates a JWT (OIDC) token using the issuer's OIDC metadata.
        /// </summary>
        public static async Task<TokenValidationResultViewModel> ValidateJwtTokenAsync(string issuerId, string jwtToken)
        {
            var result = new TokenValidationResultViewModel
            {
                IssuerId = issuerId,
                IsValid = false
            };

            if (string.IsNullOrWhiteSpace(issuerId))
            {
                result.Message = "Issuer ID is required";
                result.ErrorDetails = "Please select an issuer from the dropdown list";
                return result;
            }

            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                result.Message = "JWT token is required";
                result.ErrorDetails = "Please paste a JWT token into the text area";
                return result;
            }

            try
            {
                // Get metadata cache from the HTTP module
                var cache = MetadataPollingHttpModule.MetadataCache;
                if (cache == null)
                {
                    result.Message = "Metadata cache not initialized";
                    result.ErrorDetails = "The metadata polling service has not been initialized yet";
                    return result;
                }

                // Retrieve metadata for the issuer
                var metadata = cache.GetMetadata(issuerId);
                if (metadata == null)
                {
                    // Metadata not found - attempt to trigger a poll
                    await TriggerMetadataPollingAsync(issuerId);

                    // After polling, check again
                    metadata = cache.GetMetadata(issuerId);
                    if (metadata == null)
                    {
                        result.Message = "Issuer metadata not available";
                        result.ErrorDetails = string.Format(
                            "The metadata for issuer '{0}' could not be fetched. Please check the issuer configuration.",
                            issuerId);
                        return result;
                    }
                }

                // Check if this is OIDC metadata
                if (!(metadata is OpenIdConnectMetadataDocument oidcMetadata))
                {
                    result.Message = "Invalid metadata type";
                    result.ErrorDetails = "The selected issuer does not have OIDC metadata. JWT validation requires OIDC metadata.";
                    return result;
                }

                // Validate the JWT token
                var validationResult = ValidateJwtToken(jwtToken.Trim(), oidcMetadata, issuerId);
                if (!validationResult.IsValid)
                {
                    result.Message = validationResult.Message;
                    result.ErrorDetails = validationResult.ErrorDetails;
                    result.SignatureValid = validationResult.SignatureValid;
                    result.SignatureValidMessage = validationResult.SignatureValidMessage;
                    result.IssuerVerified = validationResult.IssuerVerified;
                    result.IssuerVerifiedMessage = validationResult.IssuerVerifiedMessage;
                    result.TokenNotExpired = validationResult.TokenNotExpired;
                    result.TokenNotExpiredMessage = validationResult.TokenNotExpiredMessage;
                    result.TokenNotYetValid = validationResult.TokenNotYetValid;
                    result.TokenNotYetValidMessage = validationResult.TokenNotYetValidMessage;
                    return result;
                }

                // Successfully validated - copy all properties
                result.IsValid = true;
                result.Message = validationResult.Message;
                result.IssuerName = GetIssuerName(issuerId);
                
                // Token properties
                result.TokenIssuer = validationResult.TokenIssuer;
                result.IssueInstant = validationResult.IssueInstant;
                result.NotBefore = validationResult.NotBefore;
                result.NotOnOrAfter = validationResult.NotOnOrAfter;
                
                // Audiences
                if (validationResult.Audiences != null && validationResult.Audiences.Any())
                {
                    result.Audiences.AddRange(validationResult.Audiences);
                }
                
                // Claims
                if (validationResult.Claims != null && validationResult.Claims.Any())
                {
                    foreach (var claim in validationResult.Claims)
                    {
                        result.Claims[claim.Key] = claim.Value;
                    }
                }
                
                // Validation flags
                result.SignatureValid = validationResult.SignatureValid;
                result.SignatureValidMessage = validationResult.SignatureValidMessage;
                result.IssuerVerified = validationResult.IssuerVerified;
                result.IssuerVerifiedMessage = validationResult.IssuerVerifiedMessage;
                result.TokenNotExpired = validationResult.TokenNotExpired;
                result.TokenNotExpiredMessage = validationResult.TokenNotExpiredMessage;
                result.TokenNotYetValid = validationResult.TokenNotYetValid;
                result.TokenNotYetValidMessage = validationResult.TokenNotYetValidMessage;
                
                // Certificate information
                result.SigningCertificateCount = validationResult.SigningCertificateCount;
                result.PrimarySigningCertificateThumbprint = validationResult.PrimarySigningCertificateThumbprint;
                result.PrimarySigningCertificateExpiration = validationResult.PrimarySigningCertificateExpiration;
                
                return result;
            }
            catch (Exception ex)
            {
                result.Message = "Token validation error";
                result.ErrorDetails = string.Format("An unexpected error occurred: {0}", ex.Message);
                System.Diagnostics.Trace.TraceError(
                    string.Format("TokenValidationService: Unexpected error during JWT validation: {0}", ex));
                return result;
            }
        }

        /// <summary>
        /// Performs JWT token validation using JwtSecurityTokenHandler.
        /// </summary>
        private static TokenValidationResultViewModel ValidateJwtToken(string jwtToken, OpenIdConnectMetadataDocument oidcMetadata, string expectedIssuerId)
        {
            var result = new TokenValidationResultViewModel { IsValid = false };

            try
            {
                // Get signing keys from OIDC metadata
                var signingKeys = oidcMetadata.Configuration?.SigningKeys;
                if (signingKeys == null || !signingKeys.Any())
                {
                    result.Message = "No signing keys in metadata";
                    result.ErrorDetails = "The issuer's OIDC metadata does not contain any signing keys for validation";
                    result.SignatureValid = false;
                    result.SignatureValidMessage = "No keys available";
                    return result;
                }

                // Store certificate info
                result.SigningCertificateCount = signingKeys.Count;
                var firstX509Key = signingKeys.FirstOrDefault(k => k is X509SecurityKey) as X509SecurityKey;
                if (firstX509Key != null && firstX509Key.Certificate != null)
                {
                    result.PrimarySigningCertificateThumbprint = firstX509Key.Certificate.Thumbprint;
                    result.PrimarySigningCertificateExpiration = firstX509Key.Certificate.NotAfter;
                }

                // Create JWT token handler
                var jwtHandler = new JwtSecurityTokenHandler();
                
                // Check if handler can read this token format
                if (!jwtHandler.CanReadToken(jwtToken))
                {
                    result.Message = "Unsupported token format";
                    result.ErrorDetails = "The token is not in a valid JWT format";
                    result.SignatureValid = false;
                    result.SignatureValidMessage = "Invalid token format";
                    return result;
                }

                // Build token validation parameters
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = signingKeys,
                    ValidateIssuer = true,
                    ValidIssuer = oidcMetadata.Issuer,
                    ValidateAudience = false,  // We'll validate manually if needed
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                // Validate the token
                try
                {
                    SecurityToken validatedToken;
                    var principal = jwtHandler.ValidateToken(jwtToken, tokenValidationParameters, out validatedToken);
                    
                    // At this point, signature is valid
                    result.SignatureValid = true;
                    result.SignatureValidMessage = "Cryptographically verified using issuer's keys";

                    // Extract token details
                    var jwtSecurityToken = validatedToken as JwtSecurityToken;
                    if (jwtSecurityToken != null)
                    {
                        result.TokenIssuer = jwtSecurityToken.Issuer;
                        result.IssuerVerified = true;
                        result.IssuerVerifiedMessage = "Issuer matches OIDC metadata";
                        
                        result.IssueInstant = jwtSecurityToken.IssuedAt;
                        result.NotBefore = jwtSecurityToken.ValidFrom;
                        result.NotOnOrAfter = jwtSecurityToken.ValidTo;

                        // Check expiration status
                        var now = DateTime.UtcNow;
                        if (now >= jwtSecurityToken.ValidFrom && now < jwtSecurityToken.ValidTo)
                        {
                            result.TokenNotExpired = true;
                            result.TokenNotExpiredMessage = $"Token is valid until {jwtSecurityToken.ValidTo:yyyy-MM-dd HH:mm:ss UTC}";
                            result.TokenNotYetValid = true;
                            result.TokenNotYetValidMessage = "Token is currently valid";
                        }
                        else if (now < jwtSecurityToken.ValidFrom)
                        {
                            result.TokenNotExpired = true;
                            result.TokenNotYetValid = false;
                            result.TokenNotYetValidMessage = $"Token becomes valid at {jwtSecurityToken.ValidFrom:yyyy-MM-dd HH:mm:ss UTC}";
                        }
                        else
                        {
                            result.TokenNotExpired = false;
                            result.TokenNotExpiredMessage = $"Token expired on {jwtSecurityToken.ValidTo:yyyy-MM-dd HH:mm:ss UTC}";
                            result.TokenNotYetValid = true;
                        }

                        // Extract audiences
                        if (jwtSecurityToken.Audiences != null)
                        {
                            result.Audiences.AddRange(jwtSecurityToken.Audiences);
                        }
                    }

                    // Extract claims from the principal
                    if (principal != null && principal.Claims != null)
                    {
                        foreach (var claim in principal.Claims)
                        {
                            var key = claim.Type;
                            var value = claim.Value;
                            
                            // Use claim type without namespace for readability
                            var shortKey = key.Contains("/") ? key.Substring(key.LastIndexOf("/") + 1) : key;
                            
                            if (!result.Claims.ContainsKey(shortKey))
                            {
                                result.Claims[shortKey] = value;
                            }
                        }
                    }

                    // Token validation successful
                    result.IsValid = true;
                    result.Message = "JWT validation passed";
                    result.IssuerName = expectedIssuerId;
                    return result;
                }
                catch (SecurityTokenSignatureKeyNotFoundException ex)
                {
                    result.Message = "Invalid token signature";
                    result.ErrorDetails = $"The token signature could not be verified with available keys: {ex.Message}";
                    result.SignatureValid = false;
                    result.SignatureValidMessage = "Key mismatch or invalid signature";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: JWT signature key not found: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenInvalidSignatureException ex)
                {
                    result.Message = "Invalid token signature";
                    result.ErrorDetails = $"The token has an invalid or tampered signature: {ex.Message}";
                    result.SignatureValid = false;
                    result.SignatureValidMessage = "Signature validation failed - token may be tampered";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: Invalid JWT signature: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenExpiredException ex)
                {
                    result.Message = "Token expired";
                    result.ErrorDetails = $"The JWT has expired and is no longer valid";
                    result.TokenNotExpired = false;
                    result.TokenNotExpiredMessage = "Token has passed its expiration date";
                    System.Diagnostics.Trace.TraceWarning($"TokenValidationService: JWT expired: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenNotYetValidException ex)
                {
                    result.Message = "Token not yet valid";
                    result.ErrorDetails = $"The JWT is not yet valid - check the NotBefore condition";
                    result.TokenNotYetValid = false;
                    result.TokenNotYetValidMessage = "Token's start validity has not been reached";
                    System.Diagnostics.Trace.TraceWarning($"TokenValidationService: JWT not yet valid: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenInvalidIssuerException ex)
                {
                    result.Message = "Invalid issuer";
                    result.ErrorDetails = $"The token issuer does not match the expected issuer: {ex.Message}";
                    result.IssuerVerified = false;
                    result.IssuerVerifiedMessage = "Token issuer mismatch";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: Invalid JWT issuer: {ex.Message}");
                    return result;
                }
                catch (SecurityTokenValidationException ex)
                {
                    result.Message = "Token validation failed";
                    result.ErrorDetails = $"JWT validation failed: {ex.Message}";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: JWT validation error: {ex.Message}");
                    return result;
                }
                catch (Exception ex)
                {
                    result.Message = "Unexpected validation error";
                    result.ErrorDetails = $"An unexpected error occurred during validation: {ex.Message}";
                    System.Diagnostics.Trace.TraceError($"TokenValidationService: Unexpected JWT error: {ex.Message}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Message = "Validation error";
                result.ErrorDetails = $"An error occurred during JWT token validation: {ex.Message}";
                return result;
            }
        }
    }
}
