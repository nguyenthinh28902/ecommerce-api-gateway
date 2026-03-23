using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Ecom.ApiGateway.Common.Helpers
{
    public static class RedisRateLimitExtentions
    {
        public static IServiceCollection AddRedisRateLimiterExtention(this IServiceCollection services)
        {
            var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("RateLimitLogger");
            services.AddRateLimiter(options =>
            {
                // 1. Policy cho Đơn hàng/Thanh toán (Token Bucket)
                // Mục đích: Cho phép khách hàng "vụt" nhanh (burst) lúc cao điểm. 
                // Yêu cầu: Bắt buộc login (sub) để tránh thiết bị ảo spam tạo đơn hàng giả.
                options.AddPolicy("ratelimit-order-policy", context =>
                {
                    var userId = GetUserSub(context);
                    if (string.IsNullOrEmpty(userId)) return CreateUnauthorizedPartition();

                    return RateLimitPartition.GetTokenBucketLimiter(userId, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 10,          // Thùng chứa tối đa 10 thẻ bài
                        ReplenishmentPeriod = TimeSpan.FromSeconds(2), // Hồi lại thẻ sau mỗi 2 giây
                        TokensPerPeriod = 2,      // Mỗi lần hồi phục được 2 thẻ
                        QueueLimit = 2            // Cho phép tối đa 2 người đứng đợi (tránh báo lỗi ngay khi vừa hết thẻ)
                    });
                });

                // 2. Policy cho Giỏ hàng (Sliding Window)
                // Mục đích: Dàn đều các thao tác thêm/sửa giỏ hàng để bảo vệ Database.
                // Yêu cầu: Bắt buộc login (sub) để xác định đúng giỏ hàng của người dùng.
                options.AddPolicy("ratelimit-cart-policy", context =>
                {
                    var userId = GetUserSub(context);
                    if (string.IsNullOrEmpty(userId)) return CreateUnauthorizedPartition();
                    return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 20,            // Giới hạn 20 lần thao tác giỏ hàng trong 1 phút
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 2,       // Chia nhỏ cửa sổ để tính toán mượt mà hơn
                        QueueLimit = 0               // Thao tác giỏ hàng không cần hàng chờ, quá giới hạn là chặn ngay
                    });
                });

                // 3. Policy cơ bản cho xem sản phẩm (Sliding Window)
                // Mục đích: Chống Bot cào dữ liệu nhưng vẫn đảm bảo khách ở quán cafe không bị "vạ lây" IP.
                options.AddPolicy("ratelimit-basic-policy", context =>
                {
                    // Ưu tiên 1: Dùng User ID nếu đã login
                    var userId = GetUserSub(context);
                    

                    // Ưu tiên 2: Dùng Guest ID từ Cookie (do GuestIdentifierMiddleware cấp)
                    if (string.IsNullOrEmpty(userId))
                    {
                        userId = context.Request.Cookies["X-Guest-DeviceId"]
                                     ?? context.Request.Headers["X-Internal-Guest-Id"].ToString();
                    }

                    // Ưu tiên 3: Dùng IP Address (Hạ sách cuối cùng nếu không có 2 cái trên)
                    if (string.IsNullOrEmpty(userId))
                    {
                        userId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    }

                    return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 100,           // 100 request/phút đủ cho người dùng thật lướt xem hàng
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0
                    });
                });

                // Cấu hình phản hồi tập trung: Phân biệt giữa 401 và 429
                options.OnRejected = async (context, token) =>
                {
                    context.Lease.TryGetMetadata("CommonMetadataName.ResourceName", out var resource);
                    bool isUnauthorized = resource?.ToString() == "unauthorized_user";

                    if (isUnauthorized)
                    {
                        logger.LogWarning("RateLimit 401: Access denied due to missing Authentication.");
                        context.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.HttpContext.Response.WriteAsync("Ný chưa đăng nhập thì sao phục vụ được!", token);
                    }
                    else
                    {
                        logger.LogCritical("RateLimit 429: Spam detected from Identifier.");
                        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        await context.HttpContext.Response.WriteAsync("Thao tác quá nhanh ný ơi, bình tĩnh tí nào!", token);
                    }
                };
            });

            return services;
        }

        // Hàm gộp lấy định danh User (sub)
        private static string GetUserSub(HttpContext context)
            => context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value ?? string.Empty;

        // Hàm tạo Partition "vô danh" để bắt lỗi 401 trong OnRejected
        private static RateLimitPartition<string> CreateUnauthorizedPartition()
            => RateLimitPartition.GetFixedWindowLimiter("unauthorized_user", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 0,
                Window = TimeSpan.FromSeconds(1)
            });
    }
}