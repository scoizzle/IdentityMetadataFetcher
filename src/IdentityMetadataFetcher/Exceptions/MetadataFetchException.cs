using System;

namespace IdentityMetadataFetcher.Exceptions
{
    /// <summary>
    /// Exception thrown when metadata fetching fails.
    /// </summary>
    public class MetadataFetchException : Exception
    {
        /// <summary>
        /// Gets or sets the endpoint that failed to provide metadata.
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code if applicable.
        /// </summary>
        public int? HttpStatusCode { get; set; }

        public MetadataFetchException(string message)
            : base(message)
        {
        }

        public MetadataFetchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public MetadataFetchException(string message, string endpoint)
            : base(message)
        {
            Endpoint = endpoint;
        }

        public MetadataFetchException(string message, string endpoint, Exception innerException)
            : base(message, innerException)
        {
            Endpoint = endpoint;
        }

        public MetadataFetchException(string message, string endpoint, int? httpStatusCode, Exception innerException)
            : base(message, innerException)
        {
            Endpoint = endpoint;
            HttpStatusCode = httpStatusCode;
        }
    }
}
