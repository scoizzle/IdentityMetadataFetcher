using IdentityMetadataFetcher.Models;
using System;
using System.Configuration;

namespace IdentityMetadataFetcher.Iis.Configuration
{
    /// <summary>
    /// Configuration element for a single issuer endpoint.
    /// </summary>
    public class IssuerElement : ConfigurationElement
    {
        /// <summary>
        /// Gets or sets the unique identifier for this issuer.
        /// </summary>
        [ConfigurationProperty("id", IsRequired = true, IsKey = true)]
        public string Id
        {
            get { return (string)this["id"]; }
            set { this["id"] = value; }
        }

        /// <summary>
        /// Gets or sets the metadata endpoint URL.
        /// </summary>
        [ConfigurationProperty("endpoint", IsRequired = true)]
        public string Endpoint
        {
            get { return (string)this["endpoint"]; }
            set { this["endpoint"] = value; }
        }

        /// <summary>
        /// Gets or sets the human-readable name for this issuer.
        /// </summary>
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        /// <summary>
        /// Gets or sets the metadata type (WSFED or SAML).
        /// </summary>
        [ConfigurationProperty("metadataType", IsRequired = true)]
        public string MetadataType
        {
            get { return (string)this["metadataType"]; }
            set { this["metadataType"] = value; }
        }

        /// <summary>
        /// Gets or sets the optional per-endpoint timeout in seconds.
        /// </summary>
        [ConfigurationProperty("timeoutSeconds", IsRequired = false)]
        public int TimeoutSeconds
        {
            get 
            { 
                object value = this["timeoutSeconds"];
                return value != null ? (int)value : 0;
            }
            set { this["timeoutSeconds"] = value; }
        }

        /// <summary>
        /// Converts this configuration element to an IssuerEndpoint object.
        /// </summary>
        public IssuerEndpoint ToIssuerEndpoint()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new ConfigurationErrorsException("Issuer 'id' attribute is required");

            if (string.IsNullOrWhiteSpace(Endpoint))
                throw new ConfigurationErrorsException($"Issuer '{Id}' 'endpoint' attribute is required");

            if (string.IsNullOrWhiteSpace(Name))
                throw new ConfigurationErrorsException($"Issuer '{Id}' 'name' attribute is required");

            var endpoint = new IssuerEndpoint
            {
                Id = Id,
                Endpoint = Endpoint,
                Name = Name,
                Timeout = TimeoutSeconds > 0 ? TimeoutSeconds * 1000 : (int?)null
            };

            return endpoint;
        }
    }
}
