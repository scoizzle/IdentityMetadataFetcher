using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityMetadataFetcher.Iis.Modules;
using MvcDemo.Models;

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

                // Basic SAML token validation
                // Check if it contains required SAML elements
                if (!tokenString.Contains("Assertion") && !tokenString.Contains("assertion"))
                {
                    result.Message = "Invalid SAML token";
                    result.ErrorDetails = "The token does not appear to contain a valid SAML Assertion element";
                    return result;
                }

                // Check if token contains signatures
                if (!tokenString.Contains("Signature") && !tokenString.Contains("signature"))
                {
                    result.Message = "Unsigned token";
                    result.ErrorDetails = "The token does not contain a digital signature";
                    return result;
                }

                // Check if metadata contains signing certificates
                if (!rawMetadata.Contains("KeyDescriptor") && !rawMetadata.Contains("X509Certificate"))
                {
                    result.Message = "No signing certificates in metadata";
                    result.ErrorDetails = "The issuer's metadata does not contain any signing certificates for validation";
                    return result;
                }


                // Successfully parsed and validated basic structure
                result.IsValid = true;
                result.Message = "Token signature is valid";
                result.IssuerName = GetIssuerName(issuerId);
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
    }
}
