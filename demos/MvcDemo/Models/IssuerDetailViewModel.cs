using System;
using System.Collections.Generic;

namespace MvcDemo.Models
{
    public class IssuerEndpointViewModel
    {
        public string Binding { get; set; }
        public string Location { get; set; }
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
        public List<IssuerEndpointViewModel> Endpoints { get; set; } = new List<IssuerEndpointViewModel>();
        public List<SigningCertificateViewModel> SigningCertificates { get; set; } = new List<SigningCertificateViewModel>();
        public string MetadataError { get; set; }
    }
}