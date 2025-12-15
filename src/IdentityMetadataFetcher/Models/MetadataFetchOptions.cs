namespace IdentityMetadataFetcher.Models
{
    /// <summary>
    /// Options for fetching metadata from an identity provider.
    /// </summary>
    public class MetadataFetchOptions
    {
        /// <summary>
        /// Gets or sets the timeout for the HTTP request in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum number of retries for failed requests.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay between retries in milliseconds.
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to validate SSL certificates.
        /// </summary>
        public bool ValidateSslCertificate { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to cache successfully fetched metadata.
        /// </summary>
        public bool CacheMetadata { get; set; } = false;

        /// <summary>
        /// Gets or sets the cache duration in minutes.
        /// </summary>
        public int CacheDurationMinutes { get; set; } = 60;
    }
}
