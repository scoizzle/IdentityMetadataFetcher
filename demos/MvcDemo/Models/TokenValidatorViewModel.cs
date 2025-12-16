namespace MvcDemo.Models
{
    public class TokenValidatorViewModel
    {
        public string IssuerId { get; set; }
        public string SamlToken { get; set; }
        public string TokenType { get; set; } = "SAML";  // "SAML" or "JWT"
    }

    public class TokenValidationResultViewModel
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public string IssuerId { get; set; }
        public string IssuerName { get; set; }
        public string ErrorDetails { get; set; }

        // Token details
        public string TokenIssuer { get; set; }
        public System.DateTime? IssueInstant { get; set; }
        public System.DateTime? NotBefore { get; set; }
        public System.DateTime? NotOnOrAfter { get; set; }
        public System.Collections.Generic.List<string> Audiences { get; set; } = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> Claims { get; set; } = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();

        // Validation details - track which checks passed/failed
        public bool? SignatureValid { get; set; }
        public string SignatureValidMessage { get; set; }
        public bool? IssuerVerified { get; set; }
        public string IssuerVerifiedMessage { get; set; }
        public bool? TokenNotExpired { get; set; }
        public string TokenNotExpiredMessage { get; set; }
        public bool? TokenNotYetValid { get; set; }
        public string TokenNotYetValidMessage { get; set; }

        // Metadata and certificate info
        public int? SigningCertificateCount { get; set; }
        public string PrimarySigningCertificateThumbprint { get; set; }
        public System.DateTime? PrimarySigningCertificateExpiration { get; set; }
        public string MetadataSourceUrl { get; set; }
    }
}
