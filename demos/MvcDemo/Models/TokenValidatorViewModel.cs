namespace MvcDemo.Models
{
    public class TokenValidatorViewModel
    {
        public string IssuerId { get; set; }
        public string SamlToken { get; set; }
    }

    public class TokenValidationResultViewModel
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public string IssuerId { get; set; }
        public string IssuerName { get; set; }
        public string ErrorDetails { get; set; }
    }
}
