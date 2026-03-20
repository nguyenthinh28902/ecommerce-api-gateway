using Ecom.ApiGateway.Models.Settings;
using Ecom.ApiGateway.Service.Interfaces;
using Ecom.ApiGateway.Service.Services;
using StackExchange.Redis;

namespace Ecom.ApiGateway.Common.Helpers
{
    public static class StackExchangeRedisCacheExtensions
    {
        public static IServiceCollection AddStackExchangeRedis(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Mapping từ Section "RedisConnection" trong appsettings vào Model
            services.Configure<RedisConnection>(configuration.GetSection(nameof(RedisConnection)));
            var redisSettings = configuration.GetSection("RedisConfig").Get<RedisConnection>()
                ?? throw new InvalidOperationException("RedisConfig configuration is missing.");

            // 2. Đăng ký StackExchangeRedis với thông số từ Model
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisSettings.RedisConnectionString;
                options.InstanceName = redisSettings.GatewayInstance;
                var configOptions = ConfigurationOptions.Parse(redisSettings?.RedisConnectionString ?? string.Empty);
                configOptions.ConnectTimeout = 1000;
                configOptions.SyncTimeout = 500;
                configOptions.AsyncTimeout = 500;
                configOptions.ConnectRetry = 0;
                configOptions.AbortOnConnectFail = false;

                options.ConfigurationOptions = configOptions;
            });

            // Đăng ký Service xử lý cache user (như đã bàn ở bước trước)
            services.AddScoped<IUserCacheService, UserCacheService>();
            return services;
        }
    }
}
