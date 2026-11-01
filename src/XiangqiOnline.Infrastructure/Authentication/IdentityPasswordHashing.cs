using Microsoft.AspNetCore.Identity;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Domain.Entities;

namespace XiangqiOnline.Infrastructure.Authentication;

public sealed class IdentityPasswordHashing : IPasswordHashing
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(User user, string passwordHash, string password)
    {
        if (passwordHash.StartsWith("$2", StringComparison.Ordinal))
        {
            try { return BCrypt.Net.BCrypt.Verify(password, passwordHash); }
            catch (ArgumentException) { return false; }
        }

        return _hasher.VerifyHashedPassword(user, passwordHash, password) != PasswordVerificationResult.Failed;
    }
}
