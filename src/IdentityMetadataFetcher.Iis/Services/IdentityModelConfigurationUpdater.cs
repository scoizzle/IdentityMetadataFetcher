using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.IdentityModel.Services;
using System.IdentityModel.Tokens;
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

        private static IEnumerable<X509Certificate2> ExtractSigningCertificates(MetadataBase metadata)
        {
            var result = new List<X509Certificate2>();

            var entity = metadata as EntityDescriptor;
            if (entity != null && entity.RoleDescriptors != null)
            {
                foreach (var role in entity.RoleDescriptors)
                {
                    foreach (var key in role.Keys.Where(k => k.Use == KeyType.Signing || k.Use == KeyType.Unspecified))
                    {
                        foreach (var clause in key.KeyInfo)
                        {
                            // Common clause types in WIF metadata
                            var x509Raw = clause as X509RawDataKeyIdentifierClause;
                            if (x509Raw != null)
                            {
                                try 
                                { 
                                    result.Add(new X509Certificate2(x509Raw.GetX509RawData())); 
                                } 
                                catch (Exception ex) 
                                { 
                                    System.Diagnostics.Trace.TraceWarning($"IdentityModelConfigurationUpdater: Failed to parse X509 raw data from key identifier clause: {ex.GetType().Name}: {ex.Message}");
                                }
                                continue;
                            }

                            var x509Thumb = clause as X509ThumbprintKeyIdentifierClause;
                            if (x509Thumb != null)
                            {
                                try
                                {
                                    // We don't have raw data; attempt to resolve via store if available
                                    using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                                    {
                                        try
                                        {
                                            store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
                                            var found = store.Certificates.Find(X509FindType.FindByThumbprint, NormalizeThumbprint(x509Thumb.GetX509Thumbprint()), false);
                                            if (found != null && found.Count > 0)
                                            {
                                                result.AddRange(found.Cast<X509Certificate2>());
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Trace.TraceWarning($"IdentityModelConfigurationUpdater: Failed to open certificate store or find certificate by thumbprint: {ex.GetType().Name}: {ex.Message}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Trace.TraceWarning($"IdentityModelConfigurationUpdater: Failed to retrieve certificate from thumbprint key identifier clause: {ex.GetType().Name}: {ex.Message}");
                                }
                                continue;
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static string TryGetPassiveStsEndpoint(MetadataBase metadata)
        {
            var entity = metadata as EntityDescriptor;
            if (entity != null && entity.RoleDescriptors != null)
            {
                var sts = entity.RoleDescriptors.OfType<SecurityTokenServiceDescriptor>().FirstOrDefault();
                if (sts != null)
                {
                    var passive = sts.PassiveRequestorEndpoints?.FirstOrDefault();
                    if (passive != null && passive.Uri != null)
                        return passive.Uri.ToString();
                }
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
