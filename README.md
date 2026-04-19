## 🔗 Technical Implementation (Chi tiết triển khai kỹ thuật)

Để hiểu rõ cách hệ thống vận hành, bạn có thể tham khảo chi tiết cấu hình tại các tầng sau:
> **Tổng quan dự án xem tại đây:** [Xem đầy đủ kiến trúc tại đây](https://github.com/nguyenthinh28902/mini-project-ecommerce)
* **Presentation Security (Client-Side):**
    * Triển khai OIDC Middleware để quản lý phiên đăng nhập và bảo mật Cookie.
    * Cấu hình chuyển hướng tự động tới Identity Server.
    * [Xem cấu hình tại Web CMS](https://github.com/nguyenthinh28902/ecommerce-cms-web)
* **Identity Provider Configuration:**
    * Định nghĩa các `IdentityResources`, `ApiScopes` và `ApiResources`.
    * Cấu hình Client Credentials cho Gateway và Authorization Code cho các ứng dụng MVC.
    * Triển khai Custom Profile Service để mapping Claims từ API User/Customer.
    * [Xem cấu hình tại Identity Server](https://github.com/nguyenthinh28902/ecommerce-identity-server-cms)
* **Gateway Routing & Security (YARP):**
    * Cấu hình Reverse Proxy chuyển tiếp yêu cầu dựa trên Route.
    * Triển khai Policy xác thực tại Gateway để đảm bảo chỉ các request có Token hợp lệ mới được đi vào tầng Service.
    * [Xem cấu hình tại Gateway](https://github.com/nguyenthinh28902/ecommerce-api-gateway-cms)
* **Service-Level:**
    * **Xác thực & Phân quyền:** Triển khai **JWT Bearer** và **Policy-based Authorization** (Scopes/Claims) tập trung tại từng Microservice.
    * **Bảo mật giao tiếp (gRPC):** Sử dụng **Interceptors** để thực thi xác thực Client/Server trong các lời gọi hàm đồng bộ cao tốc.
    * **Bảo mật thông điệp (Async):** Tích hợp cấu hình hạ tầng và mã hóa cho **RabbitMQ & MassTransit** đảm bảo an toàn luồng sự kiện.
    * [Xem cấu hình tại Product Service](https://github.com/nguyenthinh28902/Ecom.ProductService)
* **Identity CMS Core & Authorization Logic:**
    * Định nghĩa cấu trúc các thực thể cốt lõi bao gồm `ApplicationUser`, `ApplicationDepartment` và `DepartmentPermission`.
    * Triển khai logic ánh xạ và chuyển đổi quyền hạn từ cơ sở dữ liệu sang định dạng Scopes.
    * [Xem cấu hình tại Identity CMS Core](https://github.com/nguyenthinh28902/ecommerce-identity-cms/blob/main/README.md)
---
