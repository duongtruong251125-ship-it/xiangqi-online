using XiangqiOnline.Domain.Entities;

namespace XiangqiOnline.Application.Authentication;

public sealed record AuthUser(Guid Id, string Username, string Email, string DisplayName, string? AvatarUrl, DateTime CreatedAt);
public sealed record IssuedAccessToken(string Value, DateTime ExpiresAt);
public enum RefreshRotationStatus { Rotated, Invalid, ConcurrentReplay, ReuseDetected }
public sealed record RefreshRotationResult(RefreshRotationStatus Status, User? User, bool IsPersistent = true);

public sealed class AuthException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class DuplicateUserException(string field) : Exception($"Duplicate user field: {field}")
{
    public string Field { get; } = field;
}
