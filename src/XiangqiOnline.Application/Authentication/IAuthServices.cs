using XiangqiOnline.Domain.Entities;

namespace XiangqiOnline.Application.Authentication;

public interface IAuthRepository
{
    Task<User?> FindUserByIdentifierAsync(string normalizedIdentifier, CancellationToken cancellationToken);
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task CreateUserAsync(User user, CancellationToken cancellationToken);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken);
    Task<RefreshRotationResult> RotateRefreshTokenAsync(string presentedHash, RefreshToken replacement, DateTime now, TimeSpan reuseGracePeriod, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(string tokenHash, DateTime now, CancellationToken cancellationToken);
    Task RevokeAllRefreshTokensAsync(Guid userId, DateTime now, CancellationToken cancellationToken);
    Task UpdateUserAndRevokeSessionsAsync(User user, DateTime now, CancellationToken cancellationToken);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken);
}

public interface IPasswordHashing
{
    string Hash(User user, string password);
    bool Verify(User user, string passwordHash, string password);
}

public interface ITokenIssuer
{
    IssuedAccessToken CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
}
