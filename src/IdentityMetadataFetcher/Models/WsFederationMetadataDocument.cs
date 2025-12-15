using Microsoft.IdentityModel.Protocols.WsFederation;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace IdentityMetadataFetcher.Models
{
    /// <summary>
    /// Represents a WS-Federation metadata document.
    /// </summary>
    public class WsFederationMetadataDocument : MetadataDocument
    {
        private readonly WsFederationConfiguration _configuration;
        private readonly List<X509Certificate2> _signingCertificates;
        private readonly Dictionary<string, string> _endpoints;

        /// <summary>
        /// Gets the underlying WS-Federation configuration object.
        /// </summary>
        public WsFederationConfiguration Configuration => _configuration;

        /// <summary>
        /// Gets the issuer identifier from the metadata.
        /// </summary>
        public override string Issuer => _configuration?.Issuer;

        /// <summary>
        /// Gets the raw XML representation of the metadata.
        /// </summary>
        public string RawXml { get; }

        /// <summary>
        /// Gets the raw metadata representation.
        /// </summary>
        public override string RawMetadata => RawXml;

        /// <summary>
        /// Gets the signing certificates extracted from the metadata.
        /// </summary>
        public override IReadOnlyList<X509Certificate2> SigningCertificates => _signingCertificates.AsReadOnly();

        /// <summary>
        /// Gets additional endpoints from the metadata.
        /// </summary>
        public override IReadOnlyDictionary<string, string> Endpoints => _endpoints;

        /// <summary>
        /// Initializes a new instance of the WsFederationMetadataDocument class.
        /// </summary>
        /// <param name="configuration">The parsed WS-Federation configuration.</param>
        /// <param name="rawXml">The raw XML of the metadata.</param>
        public WsFederationMetadataDocument(WsFederationConfiguration configuration, string rawXml)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            RawXml = rawXml ?? throw new ArgumentNullException(nameof(rawXml));

            _signingCertificates = new List<X509Certificate2>();
            _endpoints = new Dictionary<string, string>();

            ExtractSigningCertificates();
            ExtractEndpoints();
        }

        private void ExtractSigningCertificates()
        {
            if (_configuration?.SigningKeys == null)
                return;

            foreach (var key in _configuration.SigningKeys)
            {
                try
                {
                    if (key is X509SecurityKey x509Key && x509Key.Certificate != null)
                    {
                        _signingCertificates.Add(x509Key.Certificate);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"WsFederationMetadataDocument: Failed to extract certificate from signing key: {ex.Message}");
                }
            }
        }

        private void ExtractEndpoints()
        {
            if (_configuration == null)
                return;

            if (!string.IsNullOrWhiteSpace(_configuration.TokenEndpoint))
            {
                _endpoints["TokenEndpoint"] = _configuration.TokenEndpoint;
                _endpoints["PassiveSts"] = _configuration.TokenEndpoint; // Alias for compatibility
            }
        }

        /// <summary>
        /// Returns a string representation of this metadata document.
        /// </summary>
        public override string ToString()
        {
            return $"WS-Federation Metadata: Issuer={Issuer}, Certificates={_signingCertificates.Count}";
        }
    }
}
