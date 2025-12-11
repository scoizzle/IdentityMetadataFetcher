using System;

namespace IdentityMetadataFetcher.Models
{
    /// <summary>
    /// Represents an issuer endpoint from which metadata can be fetched.
    /// </summary>
    public class IssuerEndpoint
    {
        /// <summary>
        /// Gets or sets the unique identifier for this endpoint.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the endpoint URL for the issuer.
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the issuer name/description.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the metadata type (WSFED or SAML).
        /// </summary>
        public MetadataType MetadataType { get; set; }

        /// <summary>
        /// Gets or sets the timeout for fetching metadata from this endpoint (in milliseconds).
        /// </summary>
        public int? Timeout { get; set; }

        public IssuerEndpoint()
        {
        }

        public IssuerEndpoint(string id, string endpoint, string name, MetadataType metadataType)
        {
            Id = id;
            Endpoint = endpoint;
            Name = name;
            MetadataType = metadataType;
        }
    }

    /// <summary>
    /// Specifies the type of metadata being fetched.
    /// </summary>
    public enum MetadataType
    {
        /// <summary>
        /// WS-Federation metadata
        /// </summary>
        WSFED,

        /// <summary>
        /// SAML metadata
        /// </summary>
        SAML
    }
}
