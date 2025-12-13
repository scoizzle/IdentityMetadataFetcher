using IdentityMetadataFetcher.Iis.Modules;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using Microsoft.IdentityModel.Protocols.WsFederation;
using Microsoft.IdentityModel.Tokens;
using MvcDemo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace MvcDemo.Services
{
    /// <summary>
    /// Service for managing issuers in the running MetadataPollingHttpModule.
    /// Allows adding, editing, and removing issuers without requiring an application restart.
    /// </summary>
    public class IssuerManagementService
    {
        /// <summary>
        /// Adds a new issuer to the running metadata polling service.
        /// Immediately attempts to poll the newly added issuer for metadata.
        /// </summary>
        public static bool AddIssuer(IssuerViewModel issuerModel)
        {
            if (issuerModel == null || string.IsNullOrWhiteSpace(issuerModel.Id))
            {
                return false;
            }

            try
            {
                var pollingService = MetadataPollingHttpModule.PollingService;
                if (pollingService == null)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "IssuerManagementService: Polling service not initialized");
                    return false;
                }

                // Parse metadata type
                if (!Enum.TryParse<MetadataType>(issuerModel.MetadataType, out var metadataType))
                {
                    metadataType = MetadataType.SAML;
                }

                // Create the issuer endpoint
                var endpoint = new IssuerEndpoint
                {
                    Id = issuerModel.Id,
                    Name = issuerModel.Name,
                    Endpoint = issuerModel.Endpoint,
                    MetadataType = metadataType
                };

                // Add to the polling service
                var result = pollingService.AddIssuer(endpoint);

                if (result)
                {
                    System.Diagnostics.Trace.TraceInformation(
                        string.Format("IssuerManagementService: Added issuer '{0}' to running poller", issuerModel.Id));

                    // Immediately poll the newly added issuer
                    PollIssuerImmediately(pollingService, issuerModel.Id);
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning(
                        string.Format("IssuerManagementService: Failed to add issuer '{0}' - may already exist", issuerModel.Id));
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    string.Format("IssuerManagementService: Error adding issuer: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Updates an existing issuer in the running metadata polling service.
        /// Immediately attempts to poll the updated issuer for metadata.
        /// </summary>
        public static bool UpdateIssuer(IssuerViewModel issuerModel)
        {
            if (issuerModel == null || string.IsNullOrWhiteSpace(issuerModel.Id))
            {
                return false;
            }

            try
            {
                var pollingService = MetadataPollingHttpModule.PollingService;
                if (pollingService == null)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "IssuerManagementService: Polling service not initialized");
                    return false;
                }

                // Parse metadata type
                if (!Enum.TryParse<MetadataType>(issuerModel.MetadataType, out var metadataType))
                {
                    metadataType = MetadataType.SAML;
                }

                // Create the updated issuer endpoint
                var endpoint = new IssuerEndpoint
                {
                    Id = issuerModel.Id,
                    Name = issuerModel.Name,
                    Endpoint = issuerModel.Endpoint,
                    MetadataType = metadataType
                };

                // Update in the polling service
                var result = pollingService.UpdateIssuer(endpoint);

                if (result)
                {
                    System.Diagnostics.Trace.TraceInformation(
                        string.Format("IssuerManagementService: Updated issuer '{0}' in running poller", issuerModel.Id));

                    // Immediately poll the updated issuer
                    PollIssuerImmediately(pollingService, issuerModel.Id);
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning(
                        string.Format("IssuerManagementService: Failed to update issuer '{0}' - not found", issuerModel.Id));
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    string.Format("IssuerManagementService: Error updating issuer: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Removes an issuer from the running metadata polling service.
        /// </summary>
        public static bool RemoveIssuer(string issuerId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
            {
                return false;
            }

            try
            {
                var pollingService = MetadataPollingHttpModule.PollingService;
                if (pollingService == null)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "IssuerManagementService: Polling service not initialized");
                    return false;
                }

                // Remove from the polling service
                var result = pollingService.RemoveIssuer(issuerId);

                if (result)
                {
                    System.Diagnostics.Trace.TraceInformation(
                        string.Format("IssuerManagementService: Removed issuer '{0}' from running poller", issuerId));
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning(
                        string.Format("IssuerManagementService: Failed to remove issuer '{0}' - not found", issuerId));
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    string.Format("IssuerManagementService: Error removing issuer: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Gets the current issuer endpoints from the running polling service.
        /// </summary>
        public static List<IssuerDetailViewModel> GetCurrentIssuers()
        {
            var issuers = new List<IssuerDetailViewModel>();

            try
            {
                var pollingService = MetadataPollingHttpModule.PollingService;
                if (pollingService != null)
                {
                    var endpoints = pollingService.GetCurrentEndpoints();
                    if (endpoints != null)
                    {
                        foreach (var endpoint in endpoints)
                        {
                            var issuer = new IssuerDetailViewModel
                            {
                                Id = endpoint.Id,
                                Name = endpoint.Name,
                                Endpoint = endpoint.Endpoint,
                                MetadataType = endpoint.MetadataType.ToString()
                            };

                            var cacheEntry = pollingService.GetMetadataCacheEntry(endpoint.Id);
                            if (cacheEntry != null)
                            {
                                issuer.HasMetadata = true;
                                issuer.LastMetadataFetch = cacheEntry.CachedAt;

                                // Extract metadata based on type
                                if (cacheEntry.Metadata != null)
                                {
                                    // Handle WsFederationConfiguration
                                    if (cacheEntry.Metadata is WsFederationConfiguration config)
                                    {
                                        issuer.EntityId = config.Issuer;

                                        // Signing certificates from WsFederationConfiguration
                                        if (config.SigningKeys != null)
                                        {
                                            foreach (var key in config.SigningKeys)
                                            {
                                                if (key is X509SecurityKey x509Key)
                                                {
                                                    var cert = new SigningCertificateViewModel
                                                    {
                                                        Subject = x509Key.Certificate.Subject,
                                                        Issuer = x509Key.Certificate.Issuer,
                                                        Thumbprint = x509Key.Certificate.Thumbprint,
                                                        NotBefore = x509Key.Certificate.NotBefore,
                                                        NotAfter = x509Key.Certificate.NotAfter,
                                                        Status = GetCertificateStatus(x509Key.Certificate)
                                                    };
                                                    AddCertificateIfNotExists(issuer.SigningCertificates, cert);
                                                }
                                            }
                                        }

                                        issuer.RoleType = "WS-Federation / SAML2 Token Service";
                                    }
                                    // Handle SAML metadata (XElement or other types)
                                    else if (cacheEntry.Metadata.GetType().Name == "XElement")
                                    {
                                        // SAML metadata - try to extract entityID attribute
                                        dynamic samlMetadata = cacheEntry.Metadata;
                                        try
                                        {
                                            var entityIdAttr = samlMetadata.Attribute("entityID");
                                            if (entityIdAttr != null)
                                            {
                                                issuer.EntityId = entityIdAttr.Value;
                                            }
                                        }
                                        catch
                                        {
                                            // If we can't extract entityID, just mark it as SAML
                                        }
                                        issuer.RoleType = "SAML 2.0 Identity Provider";
                                        issuer.MetadataError = "SAML metadata is cached but detailed certificate extraction is not yet supported in this UI.";
                                    }
                                    else
                                    {
                                        issuer.HasMetadata = false;
                                        issuer.MetadataError = "Metadata type is not recognized.";
                                    }
                                }
                                else
                                {
                                    issuer.HasMetadata = false;
                                    issuer.MetadataError = "Metadata has not been fetched yet. It will be available after the next polling cycle.";
                                }
                            }
                            else
                            {
                                issuer.HasMetadata = false;
                                issuer.MetadataError = "Metadata has not been fetched yet. It will be available after the next polling cycle.";
                            }

                            issuers.Add(issuer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    string.Format("IssuerManagementService: Error getting current issuers: {0}", ex.Message));
            }

            return issuers;
        }

        /// <summary>
        /// Triggers an immediate poll for the specified issuer.
        /// Uses fire-and-forget approach to avoid blocking the HTTP request.
        /// </summary>
        private static void PollIssuerImmediately(IdentityMetadataFetcher.Services.MetadataPollingService pollingService, string issuerId)
        {
            try
            {
                // Fire and forget - don't wait for the async operation to complete
                // This allows the HTTP request to complete quickly
                var pollTask = Task.Run(async () =>
                {
                    try
                    {
                        var pollResult = await pollingService.PollIssuerNowAsync(issuerId);
                        if (pollResult)
                        {
                            System.Diagnostics.Trace.TraceInformation(
                                string.Format("IssuerManagementService: Successfully polled issuer '{0}' immediately after add/update", issuerId));
                        }
                        else
                        {
                            System.Diagnostics.Trace.TraceWarning(
                                string.Format("IssuerManagementService: Immediate poll for issuer '{0}' was throttled", issuerId));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError(
                            string.Format("IssuerManagementService: Error during immediate poll of issuer '{0}': {1}", issuerId, ex.Message));
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    string.Format("IssuerManagementService: Error initiating immediate poll for issuer '{0}': {1}", issuerId, ex.Message));
            }
        }

        private static string GetCertificateStatus(X509Certificate2 cert)
        {
            var now = DateTime.Now;
            if (cert.NotAfter < now)
            {
                return "EXPIRED";
            }
            else if (cert.NotBefore > now)
            {
                return "Not yet valid";
            }
            else
            {
                var daysToExpiry = (cert.NotAfter - now).TotalDays;
                if (daysToExpiry <= 30)
                {
                    return $"Expires in {Math.Ceiling(daysToExpiry)} days";
                }
                else
                {
                    return "Valid";
                }
            }
        }

        private static void AddCertificateIfNotExists(List<SigningCertificateViewModel> certificates, SigningCertificateViewModel newCert)
        {
            // Check if certificate with same thumbprint already exists
            if (!certificates.Any(c => c.Thumbprint == newCert.Thumbprint))
            {
                certificates.Add(newCert);
            }
        }
    }
}
