using Microsoft.AspNetCore.Mvc;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Contracts.Authentication;

namespace XiangqiOnline.API.Infrastructure;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request exception for {RequestId}", context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            if (exception is AuthException authException)
            {
                context.Response.StatusCode = authException.StatusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ApiErrorEnvelope(false, null, new ApiError(authException.Code, authException.Message), context.TraceIdentifier));
                return;
            }
            if (exception is DuplicateUserException duplicate)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/json";
                var code = duplicate.Field == "email" ? "EMAIL_ALREADY_EXISTS" : "USERNAME_ALREADY_EXISTS";
                var message = duplicate.Field == "email" ? "Email đã được sử dụng." : "Tên đăng nhập đã được sử dụng.";
                await context.Response.WriteAsJsonAsync(new ApiErrorEnvelope(false, null, new ApiError(code, message), context.TraceIdentifier));
                return;
            }
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 500, Title = "The request could not be completed.", Detail = environment.IsDevelopment() ? exception.Message : null, Extensions = { ["requestId"] = context.TraceIdentifier } });
        }
    }
}
