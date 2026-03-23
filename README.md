# Enterprise API Gateway - Cửa ngõ điều hướng & Bảo mật tập trung

## 📝 Giới thiệu
Hệ thống API Gateway được xây dựng trên nền tảng **YARP (Yet Another Reverse Proxy)** của Microsoft. Đây là điểm tiếp nhận duy nhất cho mọi yêu cầu từ phía Client (CMS), chịu trách nhiệm điều hướng thông minh, kiểm soát truy cập và thực hiện chuyển đổi định danh (Token Exchange) trước khi gửi yêu cầu tới các dịch vụ nghiệp vụ.
### 🔗 Core Security & Implementation (Liên kết kỹ thuật trọng tâm)

> **Tổng quan dự án xem tại đây:** [Xem đầy đủ kiến trúc tại đây](https://github.com/nguyenthinh28902/mini-project-ecommerce)

Để đi sâu vào các cấu hình bảo mật hệ thống, bạn có thể tham khảo trực tiếp tại các module sau:

* **Client Security:** Triển khai OIDC Middleware, quản lý Secure Cookie và luồng Challenge.
  * [Cấu hình tại Web CMS](https://github.com/nguyenthinh28902/ecommerce-cms-web)
* **Identity Provider:** Định nghĩa Resource, Scope và Custom Profile Service để mapping Claims.
  * [Cấu hình tại Identity Server](https://github.com/nguyenthinh28902/ecommerce-identity-server-cms)
* **API Gateway (YARP):** Quản lý Reverse Proxy Routing và thiết lập Auth Policy tập trung.
  * [Cấu hình tại Gateway CMS](https://github.com/nguyenthinh28902/ecommerce-api-gateway-cms)
* **Resource Server:** Cấu hình JWT Bearer và phân quyền dựa trên Policy (Policy-based Authorization).
  * [Cấu hình tại Product Service](https://github.com/nguyenthinh28902/Ecom.ProductService)

---

## 🛠 Công nghệ & Giải pháp hạ tầng
- **Core Engine:** .NET Core API & **YARP**.
- **Security:** JWT Bearer Authentication & Policy-based Authorization.
- **Performance:**
    - **Redis Cache (Docker):** Lưu trữ thông tin định danh và tối ưu hóa cấp phát Token.
    - **Redis Rate Limiting:** Kiểm soát lưu lượng truy cập tập trung, ngăn chặn spam và đảm bảo tính ổn định cho toàn bộ hệ thống Microservices.
- **Data Access:** Entity Framework Core & SQL Server.

---

## 🔐 Technical Implementation (Triển khai kỹ thuật)

### 1. Centralized JWT Validation (Xác thực JWT tập trung)
Gateway đóng vai trò là chốt chặn đầu tiên để kiểm tra tính hợp lệ của Token từ Identity Server.

* **File:** [JwtAuthenticationExtensions.cs](https://github.com/nguyenthinh28902/ecommerce-api-gateway-cms/blob/main/Ecommerce.ApiGateway.Cms/Common/Auth/JwtAuthenticationExtensions.cs)
* **Giải pháp:** Cấu hình `SaveToken = true` để duy trì ngữ cảnh xác thực và thiết lập `ClockSkew` cực ngắn (20 giây) để đảm bảo sự đồng bộ thời gian tuyệt đối với Identity Server.

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.Authority = internalAuth.Issuer;
        options.SaveToken = true; // Lưu token để thực hiện Token Relay/Exchange
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(20) // Khớp thời gian chính xác giữa các tầng
        };
    });
```

### 2. Policy-Based Authorization (Phân quyền dựa trên Policy)
Định nghĩa các chính sách truy cập nghiêm ngặt dựa trên `scope` của Token ngay tại cửa ngõ hệ thống.

* **Giải pháp:** Phân tách rõ ràng các quyền đọc/ghi (`user.read`, `user.write`) và các quyền truy cập nội bộ (`user.internal`) để bảo vệ tài nguyên Microservices.

```csharp
options.AddPolicy("UserReadPolicy", policy => 
    policy.RequireClaim("scope", "user.read", "user.internal"));
options.AddPolicy("ProductPolicy", policy => 
    policy.RequireClaim("scope", "product.read", "product.write", "product.internal"));
```

### 3. Identity Transformation & Token Exchange (Chuyển đổi định danh)
Đây là kỹ thuật quan trọng nhất giúp Gateway bảo mật thông tin người dùng và thực hiện giao tiếp Service-to-Service.

* **File:** [GatewayExtensions.cs](https://github.com/nguyenthinh28902/ecommerce-api-gateway-cms/blob/main/Ecommerce.ApiGateway.Cms/Common/Auth/GatewayExtensions.cs)
* **Giải pháp:** * **Header Transformation:** Gateway tự động trích xuất Claims (Roles, Email, WorkplaceId) từ cache và đính kèm vào Header `X-User-*`.
    * **Token Exchange:** Gateway sử dụng Client Credentials riêng để hoán đổi lấy một **System Token** mới, dùng để gọi các dịch vụ Backend.

```csharp
// Đính kèm ngữ cảnh người dùng vào Proxy Request
transformContext.ProxyRequest.Headers.Add("X-User-Id", sub);
transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Roles", rolesString);

// --- TOKEN EXCHANGE (SERVICE-TO-SERVICE) ---
// Gateway đổi lấy Token hệ thống để đi tiếp vào vùng nội bộ
var systemToken = await tokenService.GetSystemTokenAsync();
transformContext.ProxyRequest.Headers.Authorization = 
    new AuthenticationHeaderValue("Bearer", systemToken);
```

### 4. Distributed Rate Limiting & Guest Identification (Kiểm soát lưu lượng & Định danh khách)
Hệ thống kết hợp giữa định danh thiết bị vãng lai và định danh người dùng để chặn đứng các hành vi spam API và khai thác dữ liệu trái phép.

* **Cấp phát Định danh (Guest ID):** Sử dụng Middleware để cấp Cookie định danh cho khách chưa đăng nhập. Khi người dùng login, hệ thống tự động dọn dẹp Cookie này để đồng bộ hóa định danh theo Token.
* **Phân loại phản hồi (Smart Rejection):** Gateway phân biệt lỗi dựa trên Metadata của Partition. Nếu yêu cầu vi phạm chính sách tại vùng cần xác thực, hệ thống trả về 401 Unauthorized. Nếu vi phạm tần suất, trả về 429 Too Many Requests.

#### 📂 Tài liệu kỹ thuật:
* [Cấu hình Middleware định danh (GuestIdentifierMiddleware.cs)](https://github.com/nguyenthinh28902/ecommerce-api-gateway/blob/main/Ecom.ApiGateway/Common/Middleware/GuestIdentifierMiddleware.cs)
* [Cấu hình Policy & Rate Limit (RedisRateLimitExtentions.cs)](https://github.com/nguyenthinh28902/ecommerce-api-gateway/blob/main/Ecom.ApiGateway/Common/Helpers/RedisRateLimitExtentions.cs)

#### 🛠 Chi tiết triển khai:

[**A. Guest Identification Middleware**](https://github.com/nguyenthinh28902/ecommerce-api-gateway/blob/main/Ecom.ApiGateway/Common/Middleware/GuestIdentifierMiddleware.cs)
```csharp
// Chỉ cấp Cookie nếu chưa Login và chưa có Cookie định danh
if (!isAuthenticated) {
    if (!context.Request.Cookies.ContainsKey("X-Guest-DeviceId")) {
        string guestId = Guid.NewGuid().ToString();
        var cookieOptions = new CookieOptions {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        context.Response.Cookies.Append("X-Guest-DeviceId", guestId, cookieOptions);
        context.Request.Headers["X-Internal-Guest-Id"] = guestId;
    }
} else {
    // Đã đăng nhập -> Xóa Cookie định danh khách (vì đã có sub)
    if (context.Request.Cookies.ContainsKey("X-Guest-DeviceId")) {
        context.Response.Cookies.Delete("X-Guest-DeviceId");
    }
}
```
[**B. Smart Rate Limit Policy**](https://github.com/nguyenthinh28902/ecommerce-api-gateway/blob/main/Ecom.ApiGateway/Common/Helpers/RedisRateLimitExtentions.cs)
```csharp
// Policy linh hoạt: Ưu tiên User ID > Guest ID > IP Address
options.AddPolicy("ratelimit-basic-policy", context => {
    var id = GetUserSub(context) 
             ?? context.Request.Cookies["X-Guest-DeviceId"]
             ?? context.Request.Headers["X-Internal-Guest-Id"].ToString()
             ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    return RateLimitPartition.GetSlidingWindowLimiter(id, _ => new SlidingWindowRateLimiterOptions {
        PermitLimit = 100, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 4, QueueLimit = 0
    });
});
```
[**C. Centralized Rejection Logic**](https://github.com/nguyenthinh28902/ecommerce-api-gateway/blob/main/Ecom.ApiGateway/Common/Helpers/RedisRateLimitExtentions.cs)
```csharp
// Phân biệt giữa 401 (Chưa login) và 429 (Quá nhanh) dựa trên ResourceName
options.OnRejected = async (context, token) => {
    context.Lease.TryGetMetadata("CommonMetadataName.ResourceName", out var resource);
    bool isUnauthorized = resource?.ToString() == "unauthorized_user";

    if (isUnauthorized) {
        context.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.HttpContext.Response.WriteAsync("Ný chưa đăng nhập thì sao phục vụ được!", token);
    } else {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Thao tác quá nhanh ný ơi, bình tĩnh tí nào!", token);
    }
};
```
---
