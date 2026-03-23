using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Ecom.ApiGateway.Middleware
{
    public class GuestIdentifierMiddleware
    {
        private readonly RequestDelegate _next;
        private const string GuestCookieName = "X-Guest-DeviceId";
        private readonly ILogger<GuestIdentifierMiddleware> _logger;

        public GuestIdentifierMiddleware(RequestDelegate next, ILogger<GuestIdentifierMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;

            if (!isAuthenticated)
            {
                // TRƯỜNG HỢP 1: CHƯA ĐĂNG NHẬP -> Cấp hoặc duy trì Cookie
                if (!context.Request.Cookies.ContainsKey(GuestCookieName))
                {
                    string guestId = Guid.NewGuid().ToString();
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true, // Bảo mật: Chống script đọc cookie
                        Secure = true,   // Bảo mật: Chỉ gửi qua HTTPS
                        SameSite = SameSiteMode.Strict, // Chống CSRF
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    };

                    context.Response.Cookies.Append(GuestCookieName, guestId, cookieOptions);
                    context.Request.Headers["X-Internal-Guest-Id"] = guestId; // Gắn tạm để Rate Limit dùng ngay

                    _logger.LogInformation("Generated new Guest ID: {GuestId}", guestId);
                }
            }
            else
            {
                // TRƯỜNG HỢP 2: ĐÃ ĐĂNG NHẬP -> Xóa Cookie định danh khách (vì đã có sub)
                if (context.Request.Cookies.ContainsKey(GuestCookieName))
                {
                    context.Response.Cookies.Delete(GuestCookieName);
                    _logger.LogInformation("User authenticated. Guest Cookie removed.");
                }
            }

            await _next(context);
        }
    }
}