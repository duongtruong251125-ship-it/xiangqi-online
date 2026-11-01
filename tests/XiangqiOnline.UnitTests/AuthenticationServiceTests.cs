using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Domain.Entities;

namespace XiangqiOnline.UnitTests;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task RegisterNormalizesIdentityAndStoresOnlyPasswordHash()
    {
        var repository = new FakeAuthRepository();
        var service = CreateService(repository);

        var user = await service.RegisterAsync(" KỳDài ", "player@example.com", "correct horse battery", CancellationToken.None);

        Assert.Equal("KỳDài", user.Username);
        Assert.Equal("KỲDÀI", repository.User!.NormalizedUsername);
        Assert.Equal("PLAYER@EXAMPLE.COM", repository.User.NormalizedEmail);
        Assert.NotEqual("correct horse battery", repository.User.PasswordHash);
        Assert.Equal("KỳDài", repository.User.DisplayName);
    }

    [Fact]
    public async Task DuplicateUsernameAndEmailHaveDistinctErrors()
    {
        var usernameService = CreateService(new FakeAuthRepository { ExistingUsername = true });
        var emailService = CreateService(new FakeAuthRepository { ExistingEmail = true });

        var usernameError = await Assert.ThrowsAsync<AuthException>(() => usernameService.RegisterAsync("Player_1", "player@example.com", "correct horse battery", CancellationToken.None));
        var emailError = await Assert.ThrowsAsync<AuthException>(() => emailService.RegisterAsync("Player_2", "player@example.com", "correct horse battery", CancellationToken.None));

        Assert.Equal("USERNAME_ALREADY_EXISTS", usernameError.Code);
        Assert.Equal("EMAIL_ALREADY_EXISTS", emailError.Code);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("short")]
    [InlineData("password123")]
    public async Task WeakPasswordsAreRejected(string password)
    {
        var service = CreateService(new FakeAuthRepository());
        var exception = await Assert.ThrowsAsync<AuthException>(() => service.RegisterAsync("Player_1", "player@example.com", password, CancellationToken.None));
        Assert.Equal("VALIDATION_ERROR", exception.Code);
    }

    [Fact]
    public async Task LoginAcceptsNormalizedUsernameOrEmailAndIssuesCookieTokenMaterial()
    {
        var repository = new FakeAuthRepository();
        var service = CreateService(repository);
        await service.RegisterAsync("Player_1", "player@example.com", "correct horse battery", CancellationToken.None);

        var byUsername = await service.LoginSessionAsync("PLAYER_1", "correct horse battery", true, CancellationToken.None);
        var byEmail = await service.LoginSessionAsync("Player@Example.com", "correct horse battery", true, CancellationToken.None);

        Assert.Equal("access-token", byUsername.AccessToken.Value);
        Assert.NotEmpty(byUsername.RefreshToken);
        Assert.Equal(2, repository.RefreshTokens.Count);
        Assert.All(repository.RefreshTokens, token => Assert.DoesNotContain("correct horse battery", token.TokenHash, StringComparison.Ordinal));
        Assert.Equal(byUsername.User.Id, byEmail.User.Id);
    }

    [Fact]
    public async Task InvalidLoginDoesNotRevealWhetherIdentifierExists()
    {
        var service = CreateService(new FakeAuthRepository());
        var missing = await Assert.ThrowsAsync<AuthException>(() => service.LoginSessionAsync("missing", "not-a-password", true, CancellationToken.None));
        Assert.Equal("AUTH_INVALID_CREDENTIALS", missing.Code);
        Assert.Equal(401, missing.StatusCode);
    }

    [Fact]
    public async Task ProfileUpdateAndPasswordChangeArePersistedAndRevokeSessions()
    {
        var repository = new FakeAuthRepository();
        var service = CreateService(repository);
        var user = await service.RegisterAsync("Player_1", "player@example.com", "correct horse battery", CancellationToken.None);
        repository.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = "old-hash", ExpiresAt = DateTime.UtcNow.AddDays(1) });

        var changed = await service.UpdateProfileAsync(user.Id, "Kỳ thủ", CancellationToken.None);
        await service.ChangePasswordAsync(user.Id, "correct horse battery", "another strong phrase", CancellationToken.None);

        Assert.Equal("Kỳ thủ", changed.DisplayName);
        Assert.Equal("another strong phrase", repository.User!.PasswordHash[5..]);
        Assert.All(repository.RefreshTokens, token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task RefreshRotatesHashedTokenAndLogoutRevokesIt()
    {
        var repository = new FakeAuthRepository();
        var service = CreateService(repository);
        var user = await service.RegisterAsync("Player_1", "player@example.com", "correct horse battery", CancellationToken.None);
        var session = await service.LoginSessionAsync("Player_1", "correct horse battery", true, CancellationToken.None);

        var refreshed = await service.RefreshAsync(session.RefreshToken, CancellationToken.None);
        await service.LogoutAsync(refreshed.RefreshToken, CancellationToken.None);

        Assert.Equal("refresh-token-2", refreshed.RefreshToken);
        Assert.Equal(2, repository.RefreshTokens.Count);
        Assert.NotNull(repository.RefreshTokens[0].RevokedAt);
        Assert.NotNull(repository.RefreshTokens[0].ReplacedByTokenId);
        Assert.NotNull(repository.RefreshTokens[1].RevokedAt);
        Assert.Equal(user.Id, repository.RefreshTokens[1].UserId);
    }

    private static AuthenticationService CreateService(FakeAuthRepository repository) =>
        new(repository, new FakePasswordHashing(), new FakeTokenIssuer(), TimeProvider.System);

    private sealed class FakePasswordHashing : IPasswordHashing
    {
        public string Hash(User user, string password) => "hash:" + password;
        public bool Verify(User user, string passwordHash, string password) => passwordHash == Hash(user, password);
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        private int _counter;
        public IssuedAccessToken CreateAccessToken(User user) => new("access-token", DateTime.UtcNow.AddMinutes(15));
        public string CreateRefreshToken() => $"refresh-token-{Interlocked.Increment(ref _counter)}";
        public string HashRefreshToken(string token) => "hash:" + token;
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public User? User { get; set; }
        public bool ExistingUsername { get; init; }
        public bool ExistingEmail { get; init; }
        public List<RefreshToken> RefreshTokens { get; } = [];
        public Task<User?> FindUserByIdentifierAsync(string normalizedIdentifier, CancellationToken cancellationToken) =>
            Task.FromResult(User is not null && (User.NormalizedUsername == normalizedIdentifier || User.NormalizedEmail == normalizedIdentifier) ? User : null);
        public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(User?.Id == userId ? User : null);
        public Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken) => Task.FromResult(ExistingUsername || User?.NormalizedUsername == normalizedUsername);
        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(ExistingEmail || User?.NormalizedEmail == normalizedEmail);
        public Task CreateUserAsync(User user, CancellationToken cancellationToken) { User = user; return Task.CompletedTask; }
        public Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken) { RefreshTokens.Add(token); return Task.CompletedTask; }
        public Task<RefreshRotationResult> RotateRefreshTokenAsync(string presentedHash, RefreshToken replacement, DateTime now, TimeSpan reuseGracePeriod, CancellationToken cancellationToken)
        {
            var previous = RefreshTokens.SingleOrDefault(token => token.TokenHash == presentedHash && token.RevokedAt == null && token.ExpiresAt > now);
            if (previous is null || User is null) return Task.FromResult(new RefreshRotationResult(RefreshRotationStatus.Invalid, null));
            previous.RevokedAt = now;
            previous.ReplacedByTokenId = replacement.Id;
            replacement.UserId = previous.UserId;
            RefreshTokens.Add(replacement);
            return Task.FromResult(new RefreshRotationResult(RefreshRotationStatus.Rotated, User, previous.IsPersistent));
        }
        public Task RevokeRefreshTokenAsync(string tokenHash, DateTime now, CancellationToken cancellationToken)
        {
            foreach (var token in RefreshTokens.Where(item => item.TokenHash == tokenHash && item.RevokedAt == null)) token.RevokedAt = now;
            return Task.CompletedTask;
        }
        public Task RevokeAllRefreshTokensAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
        {
            foreach (var token in RefreshTokens.Where(item => item.UserId == userId && item.RevokedAt == null)) token.RevokedAt = now;
            return Task.CompletedTask;
        }
        public Task UpdateUserAndRevokeSessionsAsync(User user, DateTime now, CancellationToken cancellationToken)
        {
            User = user;
            foreach (var token in RefreshTokens.Where(item => item.UserId == user.Id && item.RevokedAt == null)) token.RevokedAt = now;
            return Task.CompletedTask;
        }
        public Task UpdateUserAsync(User user, CancellationToken cancellationToken) { User = user; return Task.CompletedTask; }
    }
}
