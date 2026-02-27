namespace Ecom.ApiGateway.Models.Settings
{
    public class RedisConnection
    {
        public string RedisConnectionString { get; set; } = string.Empty;
        public string GatewayInstance { get; set; } = string.Empty;
    }
}
