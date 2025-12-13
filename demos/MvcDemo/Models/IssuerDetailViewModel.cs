using System;
using System.Collections.Generic;

namespace MvcDemo.Models
{
    public class IssuerEndpointViewModel
    {
        public string Binding { get; set; }
        public string Location { get; set; }
        public int? Index { get; set; }
        public bool? IsDefault { get; set; }
    }

    public class SigningCertificateViewModel
    {
        public string Subject { get; set; }
        public string Issuer { get; set; }
        public string Thumbprint { get; set; }
        public DateTime NotBefore { get; set; }
        public DateTime NotAfter { get; set; }
        public string Status { get; set; }
    }

    public class IssuerDetailViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Endpoint { get; set; }
        public string MetadataType { get; set; }
        public bool HasMetadata { get; set; }
        public DateTime? LastMetadataFetch { get; set; }
        public string RoleType { get; set; }
        public string EntityId { get; set; }

        // Organization information
        public string OrganizationName { get; set; }
        public string OrganizationDisplayName { get; set; }
        public string OrganizationUrl { get; set; }

        // Contact information
        public string TechnicalContactEmail { get; set; }
        public string TechnicalContactGivenName { get; set; }
        public string TechnicalContactSurname { get; set; }
        public string SupportContactEmail { get; set; }

        // Protocol and security settings
        public List<string> ProtocolsSupported { get; set; } = new List<string>();
        public bool? WantAuthnRequestsSigned { get; set; }
        public bool? AuthnRequestsSigned { get; set; }
        public bool? WantAssertionsSigned { get; set; }

        // NameID formats
        public List<string> NameIdFormats { get; set; } = new List<string>();

        // Endpoints
        public List<IssuerEndpointViewModel> Endpoints { get; set; } = new List<IssuerEndpointViewModel>();
        public List<IssuerEndpointViewModel> SingleLogoutEndpoints { get; set; } = new List<IssuerEndpointViewModel>();

        // Certificates
        public List<SigningCertificateViewModel> SigningCertificates { get; set; } = new List<SigningCertificateViewModel>();
        public List<SigningCertificateViewModel> EncryptionCertificates { get; set; } = new List<SigningCertificateViewModel>();

        public string MetadataError { get; set; }
    }
}