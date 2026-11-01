using XiangqiOnline.Contracts.Authentication;

namespace XiangqiOnline.API.Infrastructure;

public static class ApiResponses
{
    public static ApiEnvelope<T> Success<T>(T data, HttpContext context) => new(true, data, context.TraceIdentifier);

    public static ApiErrorEnvelope Failure(string code, string message, HttpContext context) =>
        new(false, null, new ApiError(code, message), context.TraceIdentifier);
}
