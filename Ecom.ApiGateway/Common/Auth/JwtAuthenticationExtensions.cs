using Ecom.ApiGateway.Models.Settings;
using Ecom.ApiGateway.Service.Interfaces;
using Ecom.ApiGateway.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Ecom.ApiGateway.Common.Auth
{
    public static class JwtAuthenticationExtensions
    {
        public static IServiceCollection AddGatewayAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            // 1. Lấy cấu hình Jwt từ appsettings.reverseproxy.identity.json
            var internalAuth = configuration.GetSection(nameof(InternalAuth)).Get<InternalAuth>()
                ?? throw new InvalidOperationException("JwtSettings missing in configuration");
            services.AddScoped<ITokenClientService, TokenClientService>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // IdentityServer URL
                    options.Authority = internalAuth.Issuer;
                    options.RequireHttpsMetadata = false; // Dev mode
                    // BẮT BUỘC: Lưu token để dùng trong AddTransforms (Token Relay)
                    options.SaveToken = true;

                    options.TokenValidationParameters = new TokenValidationParameters {
                        ValidateIssuer = true,
                        ValidIssuer = internalAuth.Issuer,
                        ValidateAudience = false, // gateway không kiểm tra audience
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(20),// Khớp thời gian chính xác giữa Gateway và IdentityServer
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            // Log khi Validation thất bại (Sai Issuer, Hết hạn, Sai Key...)
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogError($"Xác thực thất bại: {context.Exception.Message}");

                            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                            {
                                logger.LogWarning("Token đã hết hạn.");
                            }
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            // Log khi thành công (Để biết là ít nhất nó đã chạy vào đây)
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogInformation("Xác thực Token thành công!");
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            // Log khi Middleware từ chối truy cập (Thiếu Token hoặc Token không hợp lệ)
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogWarning($"Phản hồi 401: {context.Error}, {context.ErrorDescription}");
                            return Task.CompletedTask;
                        }
                    };
                });

            // 2. Cấu hình Policy sử dụng cho Routes trong proxy-config.yaml
            services.AddAuthorization(options =>
            {
                options.AddPolicy("CustomerService", policy =>
                    policy.RequireClaim("scope", "customer.read", "customer.write", "customer.internal"));
                options.AddPolicy("OrderService", policy =>
                    policy.RequireClaim("scope", "oder.read.web", "order.write.web", "order.internal.web"));
                options.AddPolicy("ProductService", policy =>
                    policy.RequireClaim("scope", "product.read.web", "product.write.web", "product.internal.web"));
                options.AddPolicy("PaymentService", policy =>
                    policy.RequireClaim("scope", "payment.read.web", "payment.write.web", "payment.internal.web"));
            });

            return services;
        }
    }
}
