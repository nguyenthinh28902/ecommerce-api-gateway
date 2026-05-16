namespace Ecom.ApiGateway.Common.Middleware
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            // Kiểm tra xem header có mã chưa, chưa có thì tạo mới
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                                ?? Guid.NewGuid().ToString();

            // Đẩy vào LogContext để log của Gateway cũng có mã này
            using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
            {
                // Quan trọng: Gắn lại vào Header để YARP tí nữa nó "bốc" đi theo
                context.Request.Headers["X-Correlation-ID"] = correlationId;

                // Trả về cho Client biết luôn (để ný debug trên trình duyệt)
                context.Response.Headers["X-Correlation-ID"] = correlationId;

                await _next(context);
            }
        }
    }
}
