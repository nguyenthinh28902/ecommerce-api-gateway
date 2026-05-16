using Ecom.ApiGateway.Common.Auth;
using Ecom.ApiGateway.Common.Helpers;
using Ecom.ApiGateway.Common.Middleware;
using Ecom.ApiGateway.Middleware;
using Ecom.ApiGateway.Models.Settings;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Console.OutputEncoding = System.Text.Encoding.UTF8;
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext() // Quan trọng để bắt được UserId, RequestId
    .CreateLogger();

builder.Host.UseSerilog();
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


try
{
    Log.Information("Service {AppName} đang khởi động...", nameof(Ecom.ApiGateway));
    var app = builder.Build();
    app.UseMiddleware<CorrelationIdMiddleware>(); // Phải nằm trên cùng

    app.UseSerilogRequestLogging(); // Tự động ghi log request kèm CorrelationId
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
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service sập rồi!");
}
finally
{
    Log.CloseAndFlush();
}
