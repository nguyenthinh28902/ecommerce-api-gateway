using Ecom.ApiGateway.Common.Auth;
using Ecom.ApiGateway.Common.Helpers;
using Ecom.ApiGateway.Middleware;
using Ecom.ApiGateway.Models.Settings;

var builder = WebApplication.CreateBuilder(args);
Console.OutputEncoding = System.Text.Encoding.UTF8;
builder.Services.AddHttpClient();
// Add services to the container.
builder.Services.Configure<InternalAuth>(
            builder.Configuration.GetSection("InternalAuth"));
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Nạp file cấu hình (appsettings.reverseproxy.identity.json)
builder.Services.AddCustomAppSettings(builder.Configuration);

//redis 
builder.Services.AddStackExchangeRedis(builder.Configuration);
//AddRedisRateLimiter
builder.Services.AddRedisRateLimiterExtention();
// 1. Cài đặt Authentication (Dùng hàm bạn đã viết)
builder.Services.AddGatewayAuthentication(builder.Configuration);

// 2. Cài đặt Proxy (Dùng hàm mới thêm AddTransforms ở trên)
builder.Services.AddGatewayProxy(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<GuestIdentifierMiddleware>(); //thêm định danh cho máy khách.
app.UseRateLimiter();
app.MapReverseProxy();

app.MapControllers();

app.Run();
