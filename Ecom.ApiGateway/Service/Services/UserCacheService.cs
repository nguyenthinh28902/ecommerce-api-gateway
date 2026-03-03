using Ecom.ApiGateway.Models;
using Ecom.ApiGateway.Models.Auths;
using Ecom.ApiGateway.Service.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using YamlDotNet.Core.Tokens;

namespace Ecom.ApiGateway.Service.Services
{
    public class UserCacheService : IUserCacheService
    {
        private readonly ILogger<UserCacheService> _logger;
        private readonly IDistributedCache _cache;
        // Đây là "vùng tên" riêng cho Identity để không lẫn với UserSession của Gateway
        private const string IDENTITY_INTERNAL_PREFIX = "WebInternalAuth:";

        public UserCacheService(IDistributedCache cache, ILogger<UserCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<UserInternalInfo?> GetUserInfoAsync(string userId)
        {
            // Key trong Redis: ví dụ "user_info:123"
            try
            {
                var cacheKey = $"{IDENTITY_INTERNAL_PREFIX}{AuthCacheOptions.CacheUserInfor}{userId}";
                var jsonData = await _cache.GetStringAsync(cacheKey);

                if (string.IsNullOrEmpty(jsonData)) return null;

                return JsonSerializer.Deserialize<UserInternalInfo>(jsonData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key {Key}", userId);
                return null;
            }
        }
    }
}