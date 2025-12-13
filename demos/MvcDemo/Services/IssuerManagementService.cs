using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityMetadataFetcher.Iis.Modules;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using MvcDemo.Models;
using System.IdentityModel.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Tokens;

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

                                // Extract metadata
                                var entityDescriptor = cacheEntry.Metadata as EntityDescriptor;
                                if (entityDescriptor != null)
                                {
                                    issuer.EntityId = entityDescriptor.EntityId?.Id;

                                    // Get roles
                                    var roles = entityDescriptor.RoleDescriptors;
                                    if (roles.Any())
                                    {
                                        var firstRole = roles.First();
                                        if (firstRole is IdentityProviderSingleSignOnDescriptor)
                                        {
                                            issuer.RoleType = "Identity Provider";
                                            var idpDescriptor = firstRole as IdentityProviderSingleSignOnDescriptor;

                                            // Endpoints: SingleSignOnServices
                                            foreach (var sso in idpDescriptor.SingleSignOnServices)
                                            {
                                                issuer.Endpoints.Add(new IssuerEndpointViewModel
                                                {
                                                    Binding = sso.Binding?.AbsoluteUri,
                                                    Location = sso.Location?.AbsoluteUri
                                                });
                                            }

                                            // Signing certificates
                                            foreach (var keyDescriptor in idpDescriptor.Keys.Where(k => k.Use == KeyType.Signing))
                                            {
                                                var cert = ExtractCertificate(keyDescriptor);
                                                if (cert != null)
                                                {
                                                    issuer.SigningCertificates.Add(cert);
                                                }
                                            }
                                        }
                                        else if (firstRole is ServiceProviderSingleSignOnDescriptor)
                                        {
                                            issuer.RoleType = "Service Provider";
                                            var spDescriptor = firstRole as ServiceProviderSingleSignOnDescriptor;

                                            // Endpoints: AssertionConsumerServices
                                            foreach (var acs in spDescriptor.AssertionConsumerServices)
                                            {
                                                issuer.Endpoints.Add(new IssuerEndpointViewModel
                                                {
                                                    Binding = acs.Value.Binding?.AbsoluteUri,
                                                    Location = acs.Value.Location?.AbsoluteUri
                                                });
                                            }

                                            // Signing certificates
                                            foreach (var keyDescriptor in spDescriptor.Keys.Where(k => k.Use == KeyType.Signing))
                                            {
                                                var cert = ExtractCertificate(keyDescriptor);
                                                if (cert != null)
                                                {
                                                    issuer.SigningCertificates.Add(cert);
                                                }
                                            }
                                        }
                                    }
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

        private static SigningCertificateViewModel ExtractCertificate(KeyDescriptor keyDescriptor)
        {
            var keyInfo = keyDescriptor.KeyInfo;
            if (keyInfo != null)
            {
                foreach (var clause in keyInfo)
                {
                    if (clause is X509RawDataKeyIdentifierClause x509Clause)
                    {
                        var cert = new X509Certificate2(x509Clause.GetX509RawData());
                        return new SigningCertificateViewModel
                        {
                            Subject = cert.Subject,
                            Issuer = cert.Issuer,
                            Thumbprint = cert.Thumbprint,
                            NotBefore = cert.NotBefore,
                            NotAfter = cert.NotAfter,
                            Status = GetCertificateStatus(cert)
                        };
                    }
                }
            }
            return null;
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
    }
}
