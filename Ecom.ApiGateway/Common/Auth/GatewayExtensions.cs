using Ecom.ApiGateway.Models.Settings;
using Ecom.ApiGateway.Service.Interfaces;
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

            configuration.AddYamlFile("proxy-config-user-service.yaml", optional: false, reloadOnChange: true);
            configuration.AddYamlFile("proxy-config-product-service.yaml", optional: false, reloadOnChange: true);
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
                    var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (!string.IsNullOrEmpty(sub))
                    {
                        // 2. KỸ THUẬT ĐÚNG: Load thông tin chi tiết (nên dùng Redis để nhanh)
                        // Giả sử bạn có UserService hoặc Redis lưu thông tin user theo sub
                        var userCache = transformContext.HttpContext.RequestServices.GetRequiredService<IUserCacheService>();
                        var userInfo = await userCache.GetUserInfoAsync(sub);
                        if (userInfo == null)
                        {
                            // 1. Gán mã trạng thái 401 (hoặc 403 tùy logic của bạn)
                            transformContext.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

                            // 2. Tùy chọn: Trả về một thông điệp JSON để Client (Nuxt/Mobile) dễ xử lý
                            transformContext.HttpContext.Response.ContentType = "application/json";
                            await transformContext.HttpContext.Response.WriteAsJsonAsync(new
                            {
                                error = "Unauthorized",
                                message = "Không tìm thấy thông tin người dùng trong hệ thống hoặc đã hết hạn."
                            });

                            // 3. QUAN TRỌNG: Ngắt luồng tại đây để YARP không chuyển tiếp request đi nữa
                            // Lấy Feature để báo ngắt Proxy
                            transformContext.HttpContext.Items["Yarp.ReverseProxy.Model.IHttpProxyFeature"] = null;

                            // Hoặc cách "chính thống" hơn nếu ný đã using Yarp.ReverseProxy.Forwarder:
                            // transformContext.HttpContext.Features.Set<IHttpProxyFeature>(null);

                            return;
                        }
                        // 3. Truyền xuống Service qua Header (Không truyền ngược vào Token để giữ Token gọn)
                        transformContext.ProxyRequest.Headers.Add("X-User-Id", sub);

                        if (!string.IsNullOrEmpty(userInfo?.Email))
                        {
                            transformContext.ProxyRequest.Headers.Add("X-User-Email", userInfo.Email);
                        }
                        if (userInfo?.Roles != null && userInfo.Roles.Any())
                        {
                            // Chuyển List<string> thành "Admin,Manager,Editor"
                            var rolesString = string.Join(",", userInfo.Roles);
                            var scopesString = string.Join(",", userInfo.Scopes);

                            // Sử dụng TryAddWithoutValidation để tránh lỗi format header
                            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Roles", rolesString);
                            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Scopes", scopesString);
                        } // Ví dụ: "Admin,Manager"
                        transformContext.ProxyRequest.Headers.Add("X-User-WorkplaceId", userInfo.WorkplaceId.ToString());

                        // --- PHẦN 2: XIN TOKEN MỚI (SERVICE-TO-SERVICE) ---
                        // Gateway dùng danh nghĩa "hệ thống" để gọi các service phía sau
                        var tokenService = transformContext.HttpContext.RequestServices.GetRequiredService<ITokenClientService>();
                        var systemToken = await tokenService.GetSystemTokenAsync();
                        
                        // Ghi đè hoặc thêm Token hệ thống vào Header Authorization
                        transformContext.ProxyRequest.Headers.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", systemToken);
                    }
                });
            });
            return services;
        }

    }
}
