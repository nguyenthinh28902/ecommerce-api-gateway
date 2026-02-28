using Ecom.ApiGateway.Models;
using Ecom.ApiGateway.Models.Auths;
using Ecom.ApiGateway.Service.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Ecom.ApiGateway.Service.Services
{
    public class UserCacheService : IUserCacheService
    {
        private readonly IDistributedCache _cache;
        // Đây là "vùng tên" riêng cho Identity để không lẫn với UserSession của Gateway
        private const string IDENTITY_INTERNAL_PREFIX = "WebInternalAuth:";

        public UserCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<UserInternalInfo?> GetUserInfoAsync(string userId)
        {
            // Key trong Redis: ví dụ "user_info:123"
            var cacheKey = $"{IDENTITY_INTERNAL_PREFIX}{AuthCacheOptions.CacheUserInfor}{userId}";
            var jsonData = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(jsonData)) return null;

            return JsonSerializer.Deserialize<UserInternalInfo>(jsonData);
        }
    }
}