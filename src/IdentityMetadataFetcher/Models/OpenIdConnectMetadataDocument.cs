using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace IdentityMetadataFetcher.Models
{
    /// <summary>
    /// Represents an OpenID Connect metadata document.
    /// </summary>
    public class OpenIdConnectMetadataDocument
    {
        private readonly OpenIdConnectConfiguration _configuration;
        private readonly List<X509Certificate2> _signingCertificates;
        private readonly Dictionary<string, string> _endpoints;

        /// <summary>
        /// Gets the underlying OpenID Connect configuration object.
        /// </summary>
        public OpenIdConnectConfiguration Configuration => _configuration;

        /// <summary>
        /// Gets the issuer identifier from the metadata.
        /// </summary>
        public string Issuer => _configuration?.Issuer;

        /// <summary>
        /// Gets the raw JSON representation of the metadata.
        /// </summary>
        public string RawJson { get; }

        /// <summary>
        /// Gets the signing certificates extracted from the metadata.
        /// </summary>
        public IReadOnlyList<X509Certificate2> SigningCertificates => _signingCertificates.AsReadOnly();

        /// <summary>
        /// Gets the timestamp when this document was created.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets additional endpoints from the metadata.
        /// </summary>
        public IReadOnlyDictionary<string, string> Endpoints => _endpoints;

        /// <summary>
        /// Initializes a new instance of the OpenIdConnectMetadataDocument class.
        /// </summary>
        /// <param name="configuration">The parsed OpenID Connect configuration.</param>
        /// <param name="rawJson">The raw JSON of the metadata.</param>
        public OpenIdConnectMetadataDocument(OpenIdConnectConfiguration configuration, string rawJson)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            RawJson = rawJson ?? throw new ArgumentNullException(nameof(rawJson));
            CreatedAt = DateTime.UtcNow;

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
                        $"OpenIdConnectMetadataDocument: Failed to extract certificate from signing key: {ex.Message}");
                }
            }
        }

        private void ExtractEndpoints()
        {
            if (_configuration == null)
                return;

            if (!string.IsNullOrWhiteSpace(_configuration.AuthorizationEndpoint))
                _endpoints["AuthorizationEndpoint"] = _configuration.AuthorizationEndpoint;

            if (!string.IsNullOrWhiteSpace(_configuration.TokenEndpoint))
                _endpoints["TokenEndpoint"] = _configuration.TokenEndpoint;

            if (!string.IsNullOrWhiteSpace(_configuration.UserInfoEndpoint))
                _endpoints["UserInfoEndpoint"] = _configuration.UserInfoEndpoint;

            if (!string.IsNullOrWhiteSpace(_configuration.EndSessionEndpoint))
                _endpoints["EndSessionEndpoint"] = _configuration.EndSessionEndpoint;

            if (!string.IsNullOrWhiteSpace(_configuration.JwksUri))
                _endpoints["JwksUri"] = _configuration.JwksUri;
        }

        /// <summary>
        /// Returns a string representation of this metadata document.
        /// </summary>
        public override string ToString()
        {
            return $"OpenID Connect Metadata: Issuer={Issuer}, Certificates={_signingCertificates.Count}";
        }
    }
}
