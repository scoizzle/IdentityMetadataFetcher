using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using Microsoft.IdentityModel.Protocols.WsFederation;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Services;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace IdentityMetadataFetcher.Iis.Services
{
    /// <summary>
    /// Updates System.IdentityModel configuration with metadata from cache.
    /// This is Windows/.NET Framework specific functionality.
    /// </summary>
    public class IdentityModelConfigurationUpdater
    {
        /// <summary>
        /// Applies metadata from a cache entry to System.IdentityModel's issuer name registry.
        /// </summary>
        /// <param name="entry">The metadata cache entry containing updated metadata.</param>
        /// <param name="issuerDisplayName">Display name for the issuer (for logging).</param>
        public void Apply(MetadataCacheEntry entry, string issuerDisplayName)
        {
            if (entry == null || entry.Metadata == null)
                return;

            var wsFedDoc = entry.Metadata;

            var authConfig = FederatedAuthentication.FederationConfiguration;
            if (authConfig == null || authConfig.IdentityConfiguration == null)
                return; // Federation not initialized in this app

            // Ensure we have a configuration-based issuer name registry to receive cert thumbprints
            var registry = authConfig.IdentityConfiguration.IssuerNameRegistry as ConfigurationBasedIssuerNameRegistry;
            if (registry == null)
            {
                registry = new ConfigurationBasedIssuerNameRegistry();
                authConfig.IdentityConfiguration.IssuerNameRegistry = registry;
            }

            // Extract signing certificates from the metadata document
            var signingCerts = wsFedDoc.SigningCertificates;

            // Register each certificate thumbprint as trusted for this issuer
            foreach (var cert in signingCerts)
            {
                try
                {
                    // Avoid duplicate entries
                    registry.AddTrustedIssuer(NormalizeThumbprint(cert.Thumbprint), issuerDisplayName ?? entry.IssuerId);
                }
                catch (InvalidOperationException ex)
                {
                    // ID4265: The issuer certificate Thumbprint already exists in the set of configured trusted issuers
                    // This can happen when multiple keys share the same certificate or in test scenarios
                    if (ex.Message != null && ex.Message.Contains("already exists"))
                    {
                        System.Diagnostics.Trace.TraceInformation(
                            $"IdentityModelConfigurationUpdater: Certificate thumbprint already registered for issuer '{issuerDisplayName ?? entry.IssuerId}'");
                    }
                    else
                    {
                        throw; // Re-throw unexpected InvalidOperationException
                    }
                }
            }

            // Optionally refresh WS-Fed issuer endpoint if metadata provides it
            string issuerEndpoint;
            if (wsFedDoc.Endpoints.TryGetValue("TokenEndpoint", out issuerEndpoint) && 
                !string.IsNullOrWhiteSpace(issuerEndpoint))
            {
                try
                {
                    if (FederatedAuthentication.WSFederationAuthenticationModule != null)
                    {
                        FederatedAuthentication.WSFederationAuthenticationModule.Issuer = issuerEndpoint;
                    }
                }
                catch (Exception ex)
                {
                    // Best-effort; some apps may restrict runtime changes
                    System.Diagnostics.Trace.TraceWarning(
                        $"IdentityModelConfigurationUpdater: Failed to update WS-Federation issuer endpoint to '{issuerEndpoint}': {ex.Message}");
                }
            }
        }

        private static string NormalizeThumbprint(string thumbprint)
        {
            if (thumbprint == null)
                throw new ArgumentNullException(nameof(thumbprint), "Thumbprint cannot be null.");
            if (thumbprint.Length == 0)
                throw new ArgumentException("Thumbprint cannot be empty.", nameof(thumbprint));
            return new string(thumbprint.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        }
    }
}