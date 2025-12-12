using System;
using System.Collections.Generic;
using System.Linq;
using IdentityMetadataFetcher.Iis.Modules;
using IdentityMetadataFetcher.Models;
using MvcDemo.Models;

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
                        $"IssuerManagementService: Added issuer '{issuerModel.Id}' to running poller");
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"IssuerManagementService: Failed to add issuer '{issuerModel.Id}' - may already exist");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    $"IssuerManagementService: Error adding issuer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates an existing issuer in the running metadata polling service.
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
                        $"IssuerManagementService: Updated issuer '{issuerModel.Id}' in running poller");
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"IssuerManagementService: Failed to update issuer '{issuerModel.Id}' - not found");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    $"IssuerManagementService: Error updating issuer: {ex.Message}");
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
                        $"IssuerManagementService: Removed issuer '{issuerId}' from running poller");
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"IssuerManagementService: Failed to remove issuer '{issuerId}' - not found");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    $"IssuerManagementService: Error removing issuer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current issuer endpoints from the running polling service.
        /// </summary>
        public static List<IssuerViewModel> GetCurrentIssuers()
        {
            var issuers = new List<IssuerViewModel>();

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
                            issuers.Add(new IssuerViewModel
                            {
                                Id = endpoint.Id,
                                Name = endpoint.Name,
                                Endpoint = endpoint.Endpoint,
                                MetadataType = endpoint.MetadataType.ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    $"IssuerManagementService: Error getting current issuers: {ex.Message}");
            }

            return issuers;
        }
    }
}
