using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Protocols.WsFederation;
using System.IdentityModel.Services;
using System.IdentityModel.Tokens;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using IdentityMetadataFetcher.Services; // Core library

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

            var fedConfig = FederatedAuthentication.FederationConfiguration;
            if (fedConfig == null || fedConfig.IdentityConfiguration == null)
                return; // Federation not initialized in this app

            // Ensure we have a configuration-based issuer name registry to receive cert thumbprints
            var registry = fedConfig.IdentityConfiguration.IssuerNameRegistry as ConfigurationBasedIssuerNameRegistry;
            if (registry == null)
            {
                registry = new ConfigurationBasedIssuerNameRegistry();
                fedConfig.IdentityConfiguration.IssuerNameRegistry = registry;
            }

            // Extract signing certificates from the metadata
            var signingCerts = ExtractSigningCertificates(entry.Metadata);

            // Register each certificate thumbprint as trusted for this issuer
            foreach (var cert in signingCerts)
            {
                try
                {
                    // Avoid duplicate entries
                    registry.AddTrustedIssuer(NormalizeThumbprint(cert.Thumbprint), issuerDisplayName ?? entry.IssuerId);
                }
                catch (ArgumentException)
                {
                    // Already present; ignore
                }
            }

            // Optionally refresh WS-Fed issuer endpoint if metadata provides it
            var issuerEndpoint = TryGetPassiveStsEndpoint(entry.Metadata);
            if (!string.IsNullOrWhiteSpace(issuerEndpoint))
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

        private static IEnumerable<X509Certificate2> ExtractSigningCertificates(WsFederationConfiguration metadata)
        {
            var result = new List<X509Certificate2>();

            if (metadata?.SigningKeys != null)
            {
                foreach (var key in metadata.SigningKeys)
                {
                    try
                    {
                        if (key is X509SecurityKey x509Key)
                        {
                            result.Add(x509Key.Certificate);
                        }
                        else if (key is Microsoft.IdentityModel.Tokens.RsaSecurityKey rsaKey)
                        {
                            // RSA keys don't have certificates, skip
                            System.Diagnostics.Trace.TraceInformation($"IdentityModelConfigurationUpdater: Skipping RSA key (no certificate available)");
                        }
                        else
                        {
                            System.Diagnostics.Trace.TraceWarning($"IdentityModelConfigurationUpdater: Unknown key type: {key.GetType().Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceWarning($"IdentityModelConfigurationUpdater: Failed to extract certificate from signing key: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            return result;
        }

        private static string TryGetPassiveStsEndpoint(WsFederationConfiguration metadata)
        {
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.TokenEndpoint))
            {
                return metadata.TokenEndpoint;
            }
            return null;
        }

        private static string NormalizeThumbprint(string thumbprint)
        {
            if (thumbprint == null)
                throw new ArgumentNullException(nameof(thumbprint), "Thumbprint cannot be null.");
            if (thumbprint.Length == 0)
                throw new ArgumentException("Thumbprint cannot be empty.", nameof(thumbprint));
            return new string(thumbprint.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        }

        private static string NormalizeThumbprint(byte[] thumbprint)
        {
            if (thumbprint == null || thumbprint.Length == 0) return null;
            return BitConverter.ToString(thumbprint).Replace("-", "").ToUpperInvariant();
        }
    }
}
