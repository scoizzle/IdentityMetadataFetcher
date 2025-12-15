using System;

namespace IdentityMetadataFetcher.Models
{
    /// <summary>
    /// Represents the result of a metadata fetch operation.
    /// </summary>
    public class MetadataFetchResult
    {
        /// <summary>
        /// Gets or sets the issuer endpoint that was queried.
        /// </summary>
        public IssuerEndpoint Endpoint { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the fetch was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the parsed metadata document if fetch was successful.
        /// </summary>
        public WsFederationMetadataDocument Metadata { get; set; }

        /// <summary>
        /// Gets or sets the raw metadata XML if fetch was successful.
        /// </summary>
        public string RawMetadata { get; set; }

        /// <summary>
        /// Gets or sets the exception if the fetch failed.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the error message if the fetch failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the metadata was fetched.
        /// </summary>
        public DateTime FetchedAt { get; set; }

        public MetadataFetchResult()
        {
            FetchedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a failed metadata fetch result.
        /// </summary>
        /// <param name="endpoint">The issuer endpoint that was queried.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="ex">Optional exception that caused the failure.</param>
        /// <returns>A MetadataFetchResult indicating failure.</returns>
        public static MetadataFetchResult Failure(IssuerEndpoint endpoint, string errorMessage, Exception ex = null)
        {
            return new MetadataFetchResult
            {
                Endpoint = endpoint,
                ErrorMessage = errorMessage,
                Exception = ex,
                IsSuccess = false,
                FetchedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates a successful metadata fetch result.
        /// </summary>
        /// <param name="endpoint">The issuer endpoint that was queried.</param>
        /// <param name="metadata">The parsed metadata document.</param>
        /// <param name="rawXml">The raw XML representation of the metadata.</param>
        /// <returns>A MetadataFetchResult indicating success.</returns>
        public static MetadataFetchResult Success(IssuerEndpoint endpoint, object metadata, string rawXml)
        {
            return new MetadataFetchResult
            {
                Endpoint = endpoint,
                Metadata = metadata as WsFederationMetadataDocument,
                RawMetadata = rawXml,
                IsSuccess = true,
                FetchedAt = DateTime.UtcNow
            };
        }
    }
}
