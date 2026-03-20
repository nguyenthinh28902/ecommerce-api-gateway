using Ecom.ApiGateway.Models;
using Ecom.ApiGateway.Models.Settings;
using Ecom.ApiGateway.Service.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Ecom.ApiGateway.Service.Services
{
    public class TokenClientService : ITokenClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDistributedCache _cache;
        private readonly RedisConnection _redisConnection;
        private readonly InternalAuth _options;
        private readonly ILogger<TokenClientService> _logger;
        private const string CacheKey = "gatewayWeb_internal_token";

        public TokenClientService(
            IHttpClientFactory httpClientFactory,
            IDistributedCache cache,
            IOptions<InternalAuth> options,
            ILogger<TokenClientService> logger,
            IOptions<RedisConnection> option)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _options = options.Value;
            _logger = logger;
            _redisConnection = option.Value;
        }

        public async Task<string> GetSystemTokenAsync()
        {
            // 1. Kiểm tra Token trong Redis
            if(_redisConnection.Enabled)
            {
                try
                {
                    var cachedToken = await _cache.GetStringAsync(CacheKey);
                    if (!string.IsNullOrEmpty(cachedToken)) return cachedToken;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi truy xuất token từ cache");
                }
            }

            // 2. Chuẩn bị request xin token mới
            var client = _httpClientFactory.CreateClient();

            var requestData = new Dictionary<string, string>
            {
            { "grant_type", "client_credentials" },
            { "client_id", _options.ClientId },
            { "client_secret", _options.ClientSecret },
            { "scope", _options.Scopes }
        };

            var tokenUrl = $"{_options.Issuer.TrimEnd('/')}/connect/token";

            try
            {
                var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(requestData));

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Lỗi lấy token hệ thống: {errorDetails}");
                }

                var tokenResult = await response.Content.ReadFromJsonAsync<TokenResponse>();

                if (tokenResult != null && !string.IsNullOrEmpty(tokenResult.AccessToken))
                {
                    // 3. Lưu vào Cache (Trừ 30 giây trừ hao thời gian mạng)
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(4)
                    };

                    if (_redisConnection.Enabled)
                    {
                        try
                        {
                            await _cache.SetStringAsync(CacheKey, tokenResult.AccessToken, cacheOptions);
                        }
                        catch (Exception ex)
                        {

                            _logger.LogError(ex, "Lỗi khi set token vào cache");
                        }
                    }
                    return tokenResult.AccessToken;
                }
            }
            catch (Exception ex)
            {
                // Log lỗi tại đây (Sử dụng ILogger)
                throw new Exception("Hệ thống không thể xác thực nội bộ.", ex);
            }

            return string.Empty;
        }
    }
}
