using System.Net.Mail;
using System.Text.RegularExpressions;
using XiangqiOnline.Domain.Entities;

namespace XiangqiOnline.Application.Authentication;

public sealed partial class AuthenticationService(IAuthRepository repository, IPasswordHashing passwords, ITokenIssuer tokens, TimeProvider timeProvider) : IAuthenticationService
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(15);
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password123", "password1234", "1234567890", "qwerty12345", "iloveyou123", "admin123456"
    };

    public async Task<AuthUser> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken)
    {
        username = username.Trim();
        email = email.Trim();
        ValidateUsername(username);
        ValidateEmail(email);
        ValidatePassword(password);
        var normalizedUsername = Normalize(username);
        var normalizedEmail = Normalize(email);
        if (await repository.UsernameExistsAsync(normalizedUsername, cancellationToken))
            throw new AuthException("USERNAME_ALREADY_EXISTS", "Tên đăng nhập đã được sử dụng.", 409);
        if (await repository.EmailExistsAsync(normalizedEmail, cancellationToken))
            throw new AuthException("EMAIL_ALREADY_EXISTS", "Email đã được sử dụng.", 409);

        var now = UtcNow();
        var user = new User
        {
            Username = username,
            NormalizedUsername = normalizedUsername,
            Email = email,
            NormalizedEmail = normalizedEmail,
            DisplayName = username,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwords.Hash(user, password);
        await repository.CreateUserAsync(user, cancellationToken);
        return Map(user);
    }

    public async Task<CreatedSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new AuthException("AUTH_REFRESH_INVALID", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", 401);
        var now = UtcNow();
        var rawReplacement = tokens.CreateRefreshToken();
        var replacement = new RefreshToken
        {
            UserId = Guid.Empty,
            TokenHash = tokens.HashRefreshToken(rawReplacement),
            CreatedAt = now,
            ExpiresAt = now.Add(RefreshLifetime)
        };
        var result = await repository.RotateRefreshTokenAsync(tokens.HashRefreshToken(refreshToken), replacement, now, ReuseGracePeriod, cancellationToken);
        if (result.Status == RefreshRotationStatus.ReuseDetected)
            throw new AuthException("AUTH_REFRESH_INVALID", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", 401);
        if (result.Status != RefreshRotationStatus.Rotated || result.User is null)
            throw new AuthException("AUTH_REFRESH_INVALID", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", 401);
        return new CreatedSession(tokens.CreateAccessToken(result.User), Map(result.User), rawReplacement, result.IsPersistent);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await repository.RevokeRefreshTokenAsync(tokens.HashRefreshToken(refreshToken), UtcNow(), cancellationToken);
    }

    public Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken) =>
        repository.RevokeAllRefreshTokensAsync(userId, UtcNow(), cancellationToken);

    public async Task<AuthUser> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Map(await RequireUserAsync(userId, cancellationToken));

    public async Task<AuthUser> UpdateProfileAsync(Guid userId, string displayName, CancellationToken cancellationToken)
    {
        displayName = displayName.Trim();
        if (displayName.Length is < 2 or > 80)
            throw new AuthException("VALIDATION_ERROR", "Tên hiển thị phải có từ 2 đến 80 ký tự.");
        var user = await RequireUserAsync(userId, cancellationToken);
        user.DisplayName = displayName;
        user.UpdatedAt = UtcNow();
        await repository.UpdateUserAsync(user, cancellationToken);
        return Map(user);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword);
        var user = await RequireUserAsync(userId, cancellationToken);
        if (!passwords.Verify(user, user.PasswordHash, currentPassword))
            throw new AuthException("PASSWORD_INCORRECT", "Mật khẩu hiện tại không đúng.", 400);
        user.PasswordHash = passwords.Hash(user, newPassword);
        user.UpdatedAt = UtcNow();
        await repository.UpdateUserAndRevokeSessionsAsync(user, UtcNow(), cancellationToken);
    }

    public async Task<CreatedSession> LoginSessionAsync(string identifier, string password, bool rememberMe, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserByIdentifierAsync(Normalize(identifier), cancellationToken);
        if (user is null || !passwords.Verify(user, user.PasswordHash, password))
            throw new AuthException("AUTH_INVALID_CREDENTIALS", "Thông tin đăng nhập không hợp lệ.", 401);
        var now = UtcNow();
        var rawRefreshToken = tokens.CreateRefreshToken();
        await repository.AddRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashRefreshToken(rawRefreshToken),
            CreatedAt = now,
            ExpiresAt = now.Add(RefreshLifetime),
            IsPersistent = rememberMe
        }, cancellationToken);
        return new CreatedSession(tokens.CreateAccessToken(user), Map(user), rawRefreshToken, rememberMe);
    }

    private async Task<User> RequireUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await repository.FindUserByIdAsync(userId, cancellationToken)
        ?? throw new AuthException("USER_NOT_FOUND", "Không tìm thấy tài khoản.", 404);

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static AuthUser Map(User user) => new(user.Id, user.Username, user.Email, user.DisplayName, user.AvatarUrl, DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc));
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static void ValidateUsername(string username)
    {
        if (username.Length is < 3 or > 64 || !UsernamePattern().IsMatch(username))
            throw new AuthException("VALIDATION_ERROR", "Tên đăng nhập dài 3–64 ký tự và chỉ dùng chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.");
    }

    private static void ValidateEmail(string email)
    {
        try
        {
            if (email.Length > 254 || new MailAddress(email).Address != email)
                throw new FormatException();
        }
        catch (FormatException)
        {
            throw new AuthException("VALIDATION_ERROR", "Email không hợp lệ.");
        }
    }

    public static void ValidatePassword(string password)
    {
        if (password.Length is < 10 or > 256 || CommonPasswords.Contains(password))
            throw new AuthException("VALIDATION_ERROR", "Mật khẩu cần dài từ 10 đến 256 ký tự và không thuộc danh sách mật khẩu phổ biến.");
    }

    [GeneratedRegex("^[\\p{L}\\p{N}._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}

public interface IAuthenticationService
{
    Task<AuthUser> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken);
    Task<CreatedSession> LoginSessionAsync(string identifier, string password, bool rememberMe, CancellationToken cancellationToken);
    Task<CreatedSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<AuthUser> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<AuthUser> UpdateProfileAsync(Guid userId, string displayName, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}

public sealed record CreatedSession(IssuedAccessToken AccessToken, AuthUser User, string RefreshToken, bool IsPersistent);
