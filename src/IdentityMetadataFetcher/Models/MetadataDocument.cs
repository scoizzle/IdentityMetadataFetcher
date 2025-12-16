using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace IdentityMetadataFetcher.Models
{
    /// <summary>
    /// Base class for identity metadata documents.
    /// </summary>
    public abstract class MetadataDocument
    {
        /// <summary>
        /// Gets the issuer identifier from the metadata.
        /// </summary>
        public abstract string Issuer { get; }

        /// <summary>
        /// Gets the raw metadata representation (XML or JSON).
        /// </summary>
        public abstract string RawMetadata { get; }

        /// <summary>
        /// Gets the signing certificates extracted from the metadata.
        /// </summary>
        public abstract IReadOnlyList<X509Certificate2> SigningCertificates { get; }

        /// <summary>
        /// Gets the timestamp when this document was created.
        /// </summary>
        public DateTime CreatedAt { get; protected set; }

        /// <summary>
        /// Gets additional endpoints from the metadata.
        /// </summary>
        public abstract IReadOnlyDictionary<string, string> Endpoints { get; }

        protected MetadataDocument()
        {
            CreatedAt = DateTime.UtcNow;
        }
    }
}
