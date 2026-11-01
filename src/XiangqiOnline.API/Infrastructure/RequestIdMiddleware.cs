namespace XiangqiOnline.API.Infrastructure;

public sealed class RequestIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        context.Response.OnStarting(() => { context.Response.Headers["X-Request-Id"] = requestId; return Task.CompletedTask; });
        await next(context);
    }
}
