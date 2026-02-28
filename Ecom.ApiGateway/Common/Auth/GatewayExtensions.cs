using Ecom.ApiGateway.Models.Settings;
using Ecom.ApiGateway.Service.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
namespace Ecom.ApiGateway.Common.Auth
{
    public static class GatewayExtensions
    {
        public static IServiceCollection AddCustomAppSettings(
        this IServiceCollection services,
        ConfigurationManager configuration
        )
        {
            // Nạp file cấu hình Reverse Proxy và Identity

            configuration.AddYamlFile("proxy-config-customer.yaml", optional: false, reloadOnChange: true);
            return services;
        }

        public static IServiceCollection AddGatewayProxy(this IServiceCollection services, IConfiguration configuration)
        {
            var authSettings = configuration.GetSection("InternalAuthHeader").Get<InternalAuthHeader>();
            services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(builderContext =>
            {
                builderContext.AddRequestTransform(async transformContext =>
                {
                    // 1. Lấy sub (User ID) từ Token ban đầu
                    var user = transformContext.HttpContext.User;
                    var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                    var email = user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
                    var phoneNumber = user.FindFirst(JwtRegisteredClaimNames.PhoneNumber)?.Value;

                    if (!string.IsNullOrEmpty(sub))
                    {
                        // 2. KỸ THUẬT ĐÚNG: Load thông tin chi tiết (nên dùng Redis để nhanh)
                        // Giả sử bạn có UserService hoặc Redis lưu thông tin user theo sub
                        var userCache = transformContext.HttpContext.RequestServices.GetRequiredService<IUserCacheService>();
                        
                        // 3. Truyền xuống Service qua Header (Không truyền ngược vào Token để giữ Token gọn)
                        transformContext.ProxyRequest.Headers.Add("X-User-Id", sub);

                        if (!string.IsNullOrEmpty(email))
                        {
                            transformContext.ProxyRequest.Headers.Add("X-User-Email", email);
                        }
                        if (!string.IsNullOrEmpty(phoneNumber))
                        {
                            transformContext.ProxyRequest.Headers.Add("X-User-Phone", phoneNumber);
                        }                      
                    }
                    // --- PHẦN 2: XIN TOKEN MỚI (SERVICE-TO-SERVICE) ---
                    // Gateway dùng danh nghĩa "hệ thống" để gọi các service phía sau
                    var tokenService = transformContext.HttpContext.RequestServices.GetRequiredService<ITokenClientService>();
                    var systemToken = await tokenService.GetSystemTokenAsync();

                    // Ghi đè hoặc thêm Token hệ thống vào Header Authorization
                    transformContext.ProxyRequest.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", systemToken);
                });
            });
            return services;
        }

    }
}
