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
        public MetadataDocument Document { get; set; }

        /// <summary>
        /// Gets or sets the parsed WS-Federation metadata document if fetch was successful.
        /// </summary>
        [Obsolete("Use Document property instead. This property will be removed in a future version.")]
        public WsFederationMetadataDocument Metadata
        {
            get => Document as WsFederationMetadataDocument;
            set => Document = value;
        }

        /// <summary>
        /// Gets or sets the parsed OpenID Connect metadata document if fetch was successful.
        /// </summary>
        [Obsolete("Use Document property instead. This property will be removed in a future version.")]
        public OpenIdConnectMetadataDocument OidcMetadata
        {
            get => Document as OpenIdConnectMetadataDocument;
            set => Document = value;
        }

        /// <summary>
        /// Gets or sets the raw metadata (XML or JSON) if fetch was successful.
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
        /// Creates a successful result for any metadata document type.
        /// </summary>
        public static MetadataFetchResult Success(IssuerEndpoint endpoint, MetadataDocument document, string rawMetadata)
        {
            return new MetadataFetchResult
            {
                Endpoint = endpoint,
                IsSuccess = true,
                Document = document,
                RawMetadata = rawMetadata
            };
        }

        /// <summary>
        /// Creates a successful result for WS-Federation metadata.
        /// </summary>
        [Obsolete("Use Success(IssuerEndpoint, MetadataDocument, string) instead.")]
        public static MetadataFetchResult Success(IssuerEndpoint endpoint, WsFederationMetadataDocument metadata, string rawMetadata)
        {
            return Success(endpoint, (MetadataDocument)metadata, rawMetadata);
        }

        /// <summary>
        /// Creates a successful result for OpenID Connect metadata.
        /// </summary>
        [Obsolete("Use Success(IssuerEndpoint, MetadataDocument, string) instead.")]
        public static MetadataFetchResult SuccessOidc(IssuerEndpoint endpoint, OpenIdConnectMetadataDocument oidcMetadata, string rawMetadata)
        {
            return Success(endpoint, (MetadataDocument)oidcMetadata, rawMetadata);
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static MetadataFetchResult Failure(IssuerEndpoint endpoint, string errorMessage, Exception exception = null)
        {
            return new MetadataFetchResult
            {
                Endpoint = endpoint,
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }
}
