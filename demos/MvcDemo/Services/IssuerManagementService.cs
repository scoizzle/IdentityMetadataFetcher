using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityMetadataFetcher.Iis.Modules;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using MvcDemo.Models;
using Microsoft.IdentityModel.Protocols.WsFederation;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

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

                                    // Organization information (if available)
                                    if (entityDescriptor.Organization != null)
                                    {
                                        issuer.OrganizationName = entityDescriptor.Organization.Names?.FirstOrDefault()?.Name;
                                        issuer.OrganizationDisplayName = entityDescriptor.Organization.DisplayNames?.FirstOrDefault()?.Name;
                                        issuer.OrganizationUrl = entityDescriptor.Organization.Urls?.FirstOrDefault()?.Uri?.AbsoluteUri;
                                    }

                                    // Contact information
                                    if (entityDescriptor.Contacts != null && entityDescriptor.Contacts.Any())
                                    {
                                        var technicalContact = entityDescriptor.Contacts.FirstOrDefault(c => c.Type == ContactType.Technical);
                                        if (technicalContact != null)
                                        {
                                            issuer.TechnicalContactEmail = technicalContact.EmailAddresses?.FirstOrDefault();
                                            issuer.TechnicalContactGivenName = technicalContact.GivenName;
                                            issuer.TechnicalContactSurname = technicalContact.Surname;
                                        }

                                        var supportContact = entityDescriptor.Contacts.FirstOrDefault(c => c.Type == ContactType.Support);
                                        if (supportContact != null)
                                        {
                                            issuer.SupportContactEmail = supportContact.EmailAddresses?.FirstOrDefault();
                                        }
                                    }

                                    // Get roles
                                    var roles = entityDescriptor.RoleDescriptors;
                                    if (roles.Any())
                                    {
                                        foreach (var role in roles)
                                        {
                                            if (role is IdentityProviderSingleSignOnDescriptor)
                                            {
                                                issuer.RoleType = "Identity Provider";
                                                var idpDescriptor = role as IdentityProviderSingleSignOnDescriptor;

                                                // Protocol support
                                                if (idpDescriptor.ProtocolsSupported != null)
                                                {
                                                    issuer.ProtocolsSupported = idpDescriptor.ProtocolsSupported.Select(u => u.AbsoluteUri).ToList();
                                                }

                                                // Endpoints: SingleSignOnServices
                                                foreach (var sso in idpDescriptor.SingleSignOnServices)
                                                {
                                                    issuer.Endpoints.Add(new IssuerEndpointViewModel
                                                    {
                                                        Binding = sso.Binding?.AbsoluteUri,
                                                        Location = sso.Location?.AbsoluteUri
                                                    });
                                                }

                                                // NameID formats supported
                                                if (idpDescriptor.NameIdentifierFormats != null)
                                                {
                                                    issuer.NameIdFormats = idpDescriptor.NameIdentifierFormats.Select(n => n.AbsoluteUri).ToList();
                                                }

                                                // Single logout services
                                                if (idpDescriptor.SingleLogoutServices != null)
                                                {
                                                    foreach (var slo in idpDescriptor.SingleLogoutServices)
                                                    {
                                                        issuer.SingleLogoutEndpoints.Add(new IssuerEndpointViewModel
                                                        {
                                                            Binding = slo.Binding?.AbsoluteUri,
                                                            Location = slo.Location?.AbsoluteUri
                                                        });
                                                    }
                                                }

                                                // Signing certificates
                                                foreach (var keyDescriptor in idpDescriptor.Keys.Where(k => k.Use == KeyType.Signing))
                                                {
                                                    var cert = ExtractCertificate(keyDescriptor);
                                                    if (cert != null)
                                                    {
                                                        AddCertificateIfNotExists(issuer.SigningCertificates, cert);
                                                    }
                                                }

                                                // Encryption certificates
                                                foreach (var keyDescriptor in idpDescriptor.Keys.Where(k => k.Use == KeyType.Encryption))
                                                {
                                                    var cert = ExtractCertificate(keyDescriptor);
                                                    if (cert != null)
                                                    {
                                                        AddCertificateIfNotExists(issuer.EncryptionCertificates, cert);
                                                    }
                                                }
                                            }
                                            else if (role is ServiceProviderSingleSignOnDescriptor)
                                            {
                                                issuer.RoleType = "Service Provider";
                                                var spDescriptor = role as ServiceProviderSingleSignOnDescriptor;

                                                // Protocol support
                                                if (spDescriptor.ProtocolsSupported != null)
                                                {
                                                    issuer.ProtocolsSupported = spDescriptor.ProtocolsSupported.Select(u => u.AbsoluteUri).ToList();
                                                }

                                                // Endpoints: AssertionConsumerServices
                                                foreach (var acs in spDescriptor.AssertionConsumerServices)
                                                {
                                                    issuer.Endpoints.Add(new IssuerEndpointViewModel
                                                    {
                                                        Binding = acs.Value.Binding?.AbsoluteUri,
                                                        Location = acs.Value.Location?.AbsoluteUri,
                                                        Index = acs.Value.Index,
                                                        IsDefault = acs.Value.IsDefault
                                                    });
                                                }

                                                // NameID formats supported
                                                if (spDescriptor.NameIdentifierFormats != null)
                                                {
                                                    issuer.NameIdFormats = spDescriptor.NameIdentifierFormats.Select(n => n.AbsoluteUri).ToList();
                                                }

                                                // Single logout services
                                                if (spDescriptor.SingleLogoutServices != null)
                                                {
                                                    foreach (var slo in spDescriptor.SingleLogoutServices)
                                                    {
                                                        issuer.SingleLogoutEndpoints.Add(new IssuerEndpointViewModel
                                                        {
                                                            Binding = slo.Binding?.AbsoluteUri,
                                                            Location = slo.Location?.AbsoluteUri
                                                        });
                                                    }
                                                }

                                                // Signing certificates
                                                foreach (var keyDescriptor in spDescriptor.Keys.Where(k => k.Use == KeyType.Signing))
                                                {
                                                    var cert = ExtractCertificate(keyDescriptor);
                                                    if (cert != null)
                                                    {
                                                        AddCertificateIfNotExists(issuer.SigningCertificates, cert);
                                                    }
                                                }

                                                // Encryption certificates
                                                foreach (var keyDescriptor in spDescriptor.Keys.Where(k => k.Use == KeyType.Encryption))
                                                {
                                                    var cert = ExtractCertificate(keyDescriptor);
                                                    if (cert != null)
                                                    {
                                                        AddCertificateIfNotExists(issuer.EncryptionCertificates, cert);
                                                    }
                                                }
                                            }
                                            else if (role is ApplicationServiceDescriptor)
                                            {
                                                issuer.RoleType = "Application Service";
                                                var appDescriptor = role as ApplicationServiceDescriptor;

                                                // Protocol support
                                                if (appDescriptor.ProtocolsSupported != null)
                                                {
                                                    issuer.ProtocolsSupported = appDescriptor.ProtocolsSupported.Select(u => u.AbsoluteUri).ToList();
                                                }

                                                // Note: ApplicationServiceEndpoints property may not be available in this version
                                                // Add a placeholder endpoint to indicate this role exists
                                                issuer.Endpoints.Add(new IssuerEndpointViewModel
                                                {
                                                    Binding = "Application Service",
                                                    Location = "Application Service endpoints available"
                                                });

                                                // Signing certificates
                                                foreach (var keyDescriptor in appDescriptor.Keys.Where(k => k.Use == KeyType.Signing))
                                                {
                                                    var cert = ExtractCertificate(keyDescriptor);
                                                    if (cert != null)
                                                    {
                                                        AddCertificateIfNotExists(issuer.SigningCertificates, cert);
                                                    }
                                                }

                                                // Encryption certificates
                                                foreach (var keyDescriptor in appDescriptor.Keys.Where(k => k.Use == KeyType.Encryption))
                                                {
                                                    var cert = ExtractCertificate(keyDescriptor);
                                                    if (cert != null)
                                                    {
                                                        AddCertificateIfNotExists(issuer.EncryptionCertificates, cert);
                                                    }
                                                }
                                            }
                                            else if (role is SecurityTokenServiceDescriptor)
                                            {
                                                issuer.RoleType = "Security Token Service";
                                                var stsDescriptor = role as SecurityTokenServiceDescriptor;

                                                // Protocol support
                                                if (stsDescriptor.ProtocolsSupported != null)
                                                {
                                                    issuer.ProtocolsSupported = stsDescriptor.ProtocolsSupported.Select(u => u.AbsoluteUri).ToList();
                                                }

                                                // Note: SecurityTokenServiceEndpoints property may not be available in this version
                                                // Add a placeholder endpoint to indicate this role exists
                                                issuer.Endpoints.Add(new IssuerEndpointViewModel
                                                {
                                                    Binding = "Security Token Service",
                                                    Location = "Security Token Service endpoints available"
                                                });

                                                // Signing certificates
                                                foreach (var keyDescriptor in stsDescriptor.Keys.Where(k => k.Use == KeyType.Signing))
                                                {
                                                    var cert = ExtractCertificate(keyDescriptor);
                                                    if (cert != null)
                                                    {
                                                        AddCertificateIfNotExists(issuer.SigningCertificates, cert);
                                                    }
                                                }

                                                // Encryption certificates
                                                foreach (var keyDescriptor in stsDescriptor.Keys.Where(k => k.Use == KeyType.Encryption))
                                                {
                                                    var cert = ExtractCertificate(keyDescriptor);
                                                    if (cert != null)
                                                    {
                                                        AddCertificateIfNotExists(issuer.EncryptionCertificates, cert);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                // Handle any other role types we encounter
                                                issuer.RoleType = role.GetType().Name.Replace("Descriptor", "").Replace("SingleSignOn", "");
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
