namespace Ecom.ApiGateway.Models.Settings
{
    public class InternalAuth
    {
        public string Issuer { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Scopes { get; set; } = string.Empty;
    }
}
