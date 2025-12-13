using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services; // Now references core library

namespace IdentityMetadataFetcher.Iis.Services
{
    /// <summary>
    /// Manages authentication failure recovery by detecting certificate trust issues
    /// and triggering on-demand metadata refresh when appropriate.
    /// </summary>
    public class AuthenticationFailureRecoveryService
    {
        private readonly AuthenticationFailureInterceptor _interceptor;
        private readonly MetadataPollingService _pollingService; // From core library
        private readonly MetadataCache _metadataCache; // From core library
        private readonly IdentityModelConfigurationUpdater _configUpdater;

        /// <summary>
        /// Initializes a new instance of the AuthenticationFailureRecoveryService.
        /// </summary>
        /// <param name="pollingService">The metadata polling service (which handles throttling).</param>
        /// <param name="metadataCache">The metadata cache.</param>
        /// <param name="configUpdater">The configuration updater for applying metadata changes.</param>
        public AuthenticationFailureRecoveryService(
            MetadataPollingService pollingService,
            MetadataCache metadataCache,
            IdentityModelConfigurationUpdater configUpdater)
        {
            _interceptor = new AuthenticationFailureInterceptor();
            _pollingService = pollingService ?? throw new ArgumentNullException(nameof(pollingService));
            _metadataCache = metadataCache ?? throw new ArgumentNullException(nameof(metadataCache));
            _configUpdater = configUpdater ?? throw new ArgumentNullException(nameof(configUpdater));
        }

        /// <summary>
        /// Attempts to recover from an authentication failure by refreshing metadata
        /// if the failure is due to certificate trust issues.
        /// </summary>
        /// <param name="exception">The authentication exception.</param>
        /// <param name="endpoints">The configured issuer endpoints.</param>
        /// <returns>True if recovery was attempted and successful; otherwise false.</returns>
        public async Task<bool> TryRecoverFromAuthenticationFailureAsync(
            Exception exception, 
            IEnumerable<IssuerEndpoint> endpoints)
        {
            if (exception == null || endpoints == null)
                return false;

            // Check if this is a certificate trust failure
            if (!_interceptor.IsCertificateTrustFailure(exception))
            {
                return false;
            }

            System.Diagnostics.Trace.TraceWarning(
                $"AuthenticationFailureRecoveryService: Detected certificate trust failure in authentication");

            // Try to extract issuer from exception
            var issuerFromException = _interceptor.ExtractIssuerFromException(exception);
            
            // Find matching endpoint(s)
            var matchingEndpoints = FindMatchingEndpoints(issuerFromException, endpoints);
            
            if (!matchingEndpoints.Any())
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"AuthenticationFailureRecoveryService: No matching configured endpoints found for issuer '{issuerFromException ?? "unknown"}'");
                return false;
            }

            bool anyRecovered = false;

            foreach (var endpoint in matchingEndpoints)
            {
                System.Diagnostics.Trace.TraceInformation(
                    $"AuthenticationFailureRecoveryService: Attempting metadata refresh for issuer '{endpoint.Name}' ({endpoint.Id})");

                try
                {
                    // Fetch fresh metadata for this specific endpoint
                    // The polling service handles throttling
                    var recovered = await RefreshMetadataForEndpointAsync(endpoint);
                    
                    if (recovered)
                    {
                        anyRecovered = true;
                        
                        System.Diagnostics.Trace.TraceInformation(
                            $"AuthenticationFailureRecoveryService: Successfully refreshed and applied metadata for '{endpoint.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(
                        $"AuthenticationFailureRecoveryService: Error refreshing metadata for '{endpoint.Name}': {ex.Message}");
                }
            }

            return anyRecovered;
        }

        /// <summary>
        /// Finds endpoints that match the issuer from the exception.
        /// </summary>
        private IEnumerable<IssuerEndpoint> FindMatchingEndpoints(string issuerFromException, IEnumerable<IssuerEndpoint> endpoints)
        {
            var matches = new List<IssuerEndpoint>();

            foreach (var endpoint in endpoints)
            {
                // Check if cached metadata exists for this endpoint
                var cachedEntry = _metadataCache.GetCacheEntry(endpoint.Id);
                
                if (cachedEntry != null && cachedEntry.Metadata != null)
                {
                    // Try to match by EntityId in the metadata
                    var entityDescriptor = cachedEntry.Metadata as System.IdentityModel.Metadata.EntityDescriptor;
                    if (entityDescriptor != null && entityDescriptor.EntityId != null)
                    {
                        var entityId = entityDescriptor.EntityId.Id;
                        
                        if (!string.IsNullOrEmpty(issuerFromException) && 
                            entityId.Equals(issuerFromException, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add(endpoint);
                            continue;
                        }
                    }
                }

                // If no specific issuer was extracted or no match found, include all configured endpoints
                if (string.IsNullOrEmpty(issuerFromException))
                {
                    matches.Add(endpoint);
                }
            }

            // If no matches found and we have an issuer, return all endpoints as fallback
            if (!matches.Any() && !string.IsNullOrEmpty(issuerFromException))
            {
                return endpoints;
            }

            return matches;
        }

        /// <summary>
        /// Refreshes metadata for a specific endpoint and applies it to IdentityModel.
        /// The polling service handles throttling internally.
        /// </summary>
        private async Task<bool> RefreshMetadataForEndpointAsync(IssuerEndpoint endpoint)
        {
            // Poll this specific endpoint (polling service handles throttling)
            var polled = await _pollingService.PollIssuerNowAsync(endpoint.Id);
            
            if (!polled)
            {
                System.Diagnostics.Trace.TraceInformation(
                    $"AuthenticationFailureRecoveryService: Poll throttled for '{endpoint.Name}' ({endpoint.Id})");
                return false;
            }

            // Check if the metadata was successfully updated
            var updatedEntry = _metadataCache.GetCacheEntry(endpoint.Id);
            
            if (updatedEntry != null && updatedEntry.Metadata != null)
            {
                // Apply the updated metadata to IdentityModel configuration
                try
                {
                    _configUpdater.Apply(updatedEntry, endpoint.Name);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(
                        $"AuthenticationFailureRecoveryService: Failed to apply metadata for '{endpoint.Name}': {ex.Message}");
                    return false;
                }
            }

            return false;
        }
    }
}
