namespace IdentityMetadataFetcher.Models
{
    /// <summary>
    /// Configuration options for metadata fetching operations.
    /// </summary>
    public class MetadataFetchOptions
    {
        /// <summary>
        /// Gets or sets the default timeout in milliseconds for HTTP requests.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000; // 30 seconds

        /// <summary>
        /// Gets or sets a value indicating whether to continue fetching from other endpoints
        /// if one fails. Default is true.
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to validate SSL/TLS certificates.
        /// </summary>
        public bool ValidateServerCertificate { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed requests.
        /// </summary>
        public int MaxRetries { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether to cache metadata.
        /// </summary>
        public bool CacheMetadata { get; set; } = false;

        /// <summary>
        /// Gets or sets the cache duration in minutes.
        /// </summary>
        public int CacheDurationMinutes { get; set; } = 60;
    }
}
