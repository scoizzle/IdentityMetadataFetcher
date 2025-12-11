using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityMetadataFetcher.Models;

namespace IdentityMetadataFetcher.Services
{
    /// <summary>
    /// Interface for fetching WSFED and SAML metadata from issuing authorities.
    /// </summary>
    public interface IMetadataFetcher
    {
        /// <summary>
        /// Fetches metadata from a single issuer endpoint.
        /// </summary>
        /// <param name="endpoint">The issuer endpoint to fetch metadata from.</param>
        /// <returns>A MetadataFetchResult containing the metadata or error information.</returns>
        MetadataFetchResult FetchMetadata(IssuerEndpoint endpoint);

        /// <summary>
        /// Asynchronously fetches metadata from a single issuer endpoint.
        /// </summary>
        /// <param name="endpoint">The issuer endpoint to fetch metadata from.</param>
        /// <returns>A task that returns a MetadataFetchResult containing the metadata or error information.</returns>
        Task<MetadataFetchResult> FetchMetadataAsync(IssuerEndpoint endpoint);

        /// <summary>
        /// Fetches metadata from multiple issuer endpoints.
        /// </summary>
        /// <param name="endpoints">The collection of issuer endpoints to fetch metadata from.</param>
        /// <returns>A collection of MetadataFetchResult for each endpoint.</returns>
        IEnumerable<MetadataFetchResult> FetchMetadataFromMultipleEndpoints(IEnumerable<IssuerEndpoint> endpoints);

        /// <summary>
        /// Asynchronously fetches metadata from multiple issuer endpoints.
        /// </summary>
        /// <param name="endpoints">The collection of issuer endpoints to fetch metadata from.</param>
        /// <returns>A task that returns a collection of MetadataFetchResult for each endpoint.</returns>
        Task<IEnumerable<MetadataFetchResult>> FetchMetadataFromMultipleEndpointsAsync(IEnumerable<IssuerEndpoint> endpoints);
    }
}
