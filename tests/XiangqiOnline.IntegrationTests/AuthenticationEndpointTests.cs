using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.IntegrationTests;

[Collection(AuthIntegrationCollection.Name)]
public sealed class AuthenticationEndpointTests(AuthIntegrationFixture fixture)
{
    [Fact]
    public async Task RegisterLoginByUsernameAndEmailMeAndProfileUpdateWorkAgainstSqlServer()
    {
        using var client = CreateClient();
        var username = $"User_{Guid.NewGuid():N}"[..18];
        var email = $"{Guid.NewGuid():N}@example.com";
        var registered = await Register(client, username, email);
        Assert.True(registered.Status == HttpStatusCode.Created, registered.Body);
        Assert.Equal(username, registered.Data.GetProperty("username").GetString());

        var usernameLogin = await Login(client, username);
        Assert.Equal(HttpStatusCode.OK, usernameLogin.Status);
        using var loginJson = JsonDocument.Parse(usernameLogin.Body);
        var token = loginJson.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        Assert.True(loginJson.RootElement.GetProperty("data").GetProperty("expiresIn").GetInt32() > 0);
        Assert.NotNull(usernameLogin.Cookie);
        Assert.Contains("httponly", usernameLogin.CookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", usernameLogin.CookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", usernameLogin.CookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=604800", usernameLogin.CookieHeader, StringComparison.OrdinalIgnoreCase);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storedUser = await db.Users.SingleAsync(item => item.NormalizedUsername == username.ToUpperInvariant());
            Assert.NotEqual("Strong horse 456!", storedUser.PasswordHash);
            Assert.StartsWith("$2", storedUser.PasswordHash, StringComparison.Ordinal);
            Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHashing>().Verify(storedUser, storedUser.PasswordHash, "Strong horse 456!"));
            var rawRefreshToken = CookieValue(usernameLogin.Cookie!);
            var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken)));
            var storedToken = await db.RefreshTokens.SingleAsync(item => item.TokenHash == expectedHash);
            Assert.Equal(64, storedToken.TokenHash.Length);
            Assert.DoesNotContain(rawRefreshToken, storedToken.TokenHash, StringComparison.Ordinal);
            Assert.True(storedToken.IsPersistent);
        }

        using var me = await AuthorizedGet(client, token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meJson = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync());
        Assert.Equal(email, meJson.RootElement.GetProperty("data").GetProperty("email").GetString());
        Assert.False(meJson.RootElement.GetProperty("data").TryGetProperty("passwordHash", out _));

        using var stats = await AuthorizedGet(client, token, "/api/v1/users/me/stats");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        using var statsJson = await JsonDocument.ParseAsync(await stats.Content.ReadAsStreamAsync());
        Assert.Equal(0, statsJson.RootElement.GetProperty("data").GetProperty("played").GetInt32());

        var emailLogin = await Login(client, email);
        Assert.Equal(HttpStatusCode.OK, emailLogin.Status);
        using var update = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/users/me")
        {
            Content = JsonContent.Create(new { displayName = "Người chơi" })
        };
        update.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var updated = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedJson = await JsonDocument.ParseAsync(await updated.Content.ReadAsStreamAsync());
        Assert.Equal("Người chơi", updatedJson.RootElement.GetProperty("data").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task DuplicateRegistrationIsRejectedEvenUnderConcurrentRequests()
    {
        using var client = CreateClient();
        var username = $"Race_{Guid.NewGuid():N}"[..18];
        var email = $"{Guid.NewGuid():N}@example.com";
        var first = Register(client, username, email);
        var second = Register(client, username, email);
        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, results.Count(result => result.Status == HttpStatusCode.Created));
        Assert.Equal(1, results.Count(result => result.Status == HttpStatusCode.Conflict));
        var duplicate = results.Single(result => result.Status == HttpStatusCode.Conflict);
        Assert.True(duplicate.Body.Contains("USERNAME_ALREADY_EXISTS", StringComparison.Ordinal) || duplicate.Body.Contains("EMAIL_ALREADY_EXISTS", StringComparison.Ordinal), duplicate.Body);
    }

    [Fact]
    public async Task LegacyIdentityPasswordHashStillAuthenticatesWithoutRewritingStoredHash()
    {
        using var client = CreateClient();
        var username = $"Legacy_{Guid.NewGuid():N}"[..18];
        await Register(client, username, $"{Guid.NewGuid():N}@example.com");
        string legacyHash;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.NormalizedUsername == username.ToUpperInvariant());
            legacyHash = new PasswordHasher<User>().HashPassword(user, "Strong horse 456!");
            user.PasswordHash = legacyHash;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await Login(client, username)).Status);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.NormalizedUsername == username.ToUpperInvariant());
            Assert.Equal(legacyHash, user.PasswordHash);
        }
    }

    [Fact]
    public async Task RefreshRotatesCookieAndLogoutInvalidatesIt()
    {
        using var client = CreateClient();
        var username = $"Rotate_{Guid.NewGuid():N}"[..18];
        await Register(client, username, $"{Guid.NewGuid():N}@example.com");
        var login = await Login(client, username);
        var oldCookie = login.Cookie!;

        var refresh = await Post(client, "/api/v1/auth/refresh", new { }, oldCookie);
        Assert.Equal(HttpStatusCode.OK, refresh.Status);
        Assert.NotNull(refresh.Cookie);
        Assert.DoesNotContain("refreshToken", refresh.Body, StringComparison.OrdinalIgnoreCase);
        var access = JsonDocument.Parse(refresh.Body).RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        using var me = await AuthorizedGet(client, access);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var logout = await Post(client, "/api/v1/auth/logout", new { }, refresh.Cookie);
        Assert.Equal(HttpStatusCode.OK, logout.Status);
        var reuse = await Post(client, "/api/v1/auth/refresh", new { }, refresh.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.Status);
        Assert.Contains("AUTH_REFRESH_INVALID", reuse.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAcceptsViteLoopbackIpOrigin()
    {
        using var client = CreateClient();
        var username = $"Vite_{Guid.NewGuid():N}"[..18];
        await Register(client, username, $"{Guid.NewGuid():N}@example.com");
        var login = await Login(client, username);

        var refresh = await Post(client, "/api/v1/auth/refresh", new { }, login.Cookie,
            origin: "http://127.0.0.1:5173");

        Assert.Equal(HttpStatusCode.OK, refresh.Status);
        Assert.NotNull(refresh.Cookie);
    }

    [Fact]
    public async Task ConcurrentRefreshDoesNotRevokeWinningReplacement()
    {
        using var client = CreateClient();
        var username = $"Concurrent_{Guid.NewGuid():N}"[..18];
        await Register(client, username, $"{Guid.NewGuid():N}@example.com");
        var login = await Login(client, username);

        var attempts = await Task.WhenAll(
            Post(client, "/api/v1/auth/refresh", new { }, login.Cookie),
            Post(client, "/api/v1/auth/refresh", new { }, login.Cookie));
        var winner = Assert.Single(attempts.Where(item => item.Status == HttpStatusCode.OK));
        Assert.Single(attempts.Where(item => item.Status == HttpStatusCode.Unauthorized));
        Assert.NotNull(winner.Cookie);

        var next = await Post(client, "/api/v1/auth/refresh", new { }, winner.Cookie);
        Assert.Equal(HttpStatusCode.OK, next.Status);
    }

    [Fact]
    public async Task ReuseAfterGracePeriodRevokesCurrentSessions()
    {
        using var client = CreateClient();
        var username = $"Reuse_{Guid.NewGuid():N}"[..18];
        var registered = await Register(client, username, $"{Guid.NewGuid():N}@example.com");
        var userId = registered.Data.GetProperty("id").GetGuid();
        var login = await Login(client, username);
        var firstRotation = await Post(client, "/api/v1/auth/refresh", new { }, login.Cookie);
        Assert.Equal(HttpStatusCode.OK, firstRotation.Status);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(CookieValue(login.Cookie!))));
            var old = await db.RefreshTokens.SingleAsync(token => token.TokenHash == hash);
            Assert.NotNull(old.ReplacedByTokenId);
            old.RevokedAt = DateTime.UtcNow.AddSeconds(-20);
            await db.SaveChangesAsync();
        }

        var replay = await Post(client, "/api/v1/auth/refresh", new { }, login.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.Status);
        Assert.Contains("AUTH_REFRESH_INVALID", replay.Body, StringComparison.Ordinal);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.All(await db.RefreshTokens.Where(token => token.UserId == userId).ToListAsync(), token => Assert.NotNull(token.RevokedAt));
        }
        var currentSession = await Post(client, "/api/v1/auth/refresh", new { }, firstRotation.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, currentSession.Status);
    }

    [Fact]
    public async Task LogoutAllAndPasswordChangeRevokeRefreshSessions()
    {
        using var client = CreateClient();
        var username = $"Password_{Guid.NewGuid():N}"[..18];
        await Register(client, username, $"{Guid.NewGuid():N}@example.com");
        var loginA = await Login(client, username);
        var loginB = await Login(client, username);
        var accessA = JsonDocument.Parse(loginA.Body).RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        var logoutAll = await Post(client, "/api/v1/auth/logout-all", new { }, authorization: accessA);
        Assert.Equal(HttpStatusCode.OK, logoutAll.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/v1/auth/refresh", new { }, loginB.Cookie)).Status);

        var current = await Login(client, username);
        var currentAccess = JsonDocument.Parse(current.Body).RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        var change = await Put(client, "/api/v1/users/me/password", new
        {
            currentPassword = "Strong horse 456!",
            newPassword = "Another horse 789!",
            confirmNewPassword = "Another horse 789!"
        }, currentAccess);
        Assert.Equal(HttpStatusCode.OK, change.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, username, "Strong horse 456!")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Login(client, username, "Another horse 789!")).Status);
    }

    [Fact]
    public async Task MissingInvalidAndExpiredJwtAreRejectedWithStableErrors()
    {
        using var client = CreateClient();
        using var missing = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var missingJson = await JsonDocument.ParseAsync(await missing.Content.ReadAsStreamAsync());
        Assert.Equal("AUTH_UNAUTHORIZED", missingJson.RootElement.GetProperty("error").GetProperty("code").GetString());

        using var invalid = await AuthorizedGet(client, "not.a.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        var expired = CreateExpiredToken(fixture.TestSigningKey);
        using var expiredResponse = await AuthorizedGet(client, expired);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
        using var expiredJson = await JsonDocument.ParseAsync(await expiredResponse.Content.ReadAsStreamAsync());
        Assert.Equal("AUTH_TOKEN_EXPIRED", expiredJson.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private async Task<(HttpStatusCode Status, string Body, string? Cookie, JsonElement Data)> Register(HttpClient client, string username, string email)
    {
        using var response = await PostRequest(client, "/api/v1/auth/register", new { username, email, password = "Strong horse 456!", confirmPassword = "Strong horse 456!" });
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return (response.StatusCode, body, GetCookie(response), json.RootElement.TryGetProperty("data", out var data) ? data.Clone() : default);
    }

    private async Task<(HttpStatusCode Status, string Body, string? Cookie, string? CookieHeader)> Login(HttpClient client, string identifier, string password = "Strong horse 456!")
    {
        using var response = await PostRequest(client, "/api/v1/auth/login", new { identifier, password, rememberMe = true });
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), GetCookie(response),
            response.Headers.TryGetValues("Set-Cookie", out var values) ? values.FirstOrDefault() : null);
    }

    private static async Task<(HttpStatusCode Status, string Body, string? Cookie)> Post(HttpClient client, string path, object body, string? cookie = null, string? authorization = null, string origin = "http://localhost:5173")
    {
        using var request = BuildRequest(HttpMethod.Post, path, body, cookie, authorization, origin);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), GetCookie(response));
    }

    private static async Task<(HttpStatusCode Status, string Body)> Put(HttpClient client, string path, object body, string authorization)
    {
        using var request = BuildRequest(HttpMethod.Put, path, body, authorization: authorization);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> AuthorizedGet(HttpClient client, string token, string path = "/api/v1/users/me")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostRequest(HttpClient client, string path, object body) => client.SendAsync(BuildRequest(HttpMethod.Post, path, body));

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, object body, string? cookie = null, string? authorization = null, string origin = "http://localhost:5173")
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Origin", origin);
        if (cookie is not null) request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (authorization is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorization);
        return request;
    }

    private static string? GetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.FirstOrDefault()?.Split(';')[0] : null;

    private static string CookieValue(string cookie) => cookie[(cookie.IndexOf('=') + 1)..];

    private HttpClient CreateClient() => fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static string CreateExpiredToken(string base64Key)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(base64Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow.AddMinutes(-5);
        var token = new JwtSecurityToken("XiangqiOnline.Tests", "XiangqiOnline.Tests.Web", [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())], now.AddMinutes(-5), now, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

[Collection(RateLimitCollection.Name)]
public sealed class AuthenticationRateLimitTests(RateLimitAuthFixture fixture)
{
    [Fact]
    public async Task RegistrationIsRateLimitedByRemoteIp()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        HttpStatusCode status = HttpStatusCode.OK;
        for (var index = 0; index < 6; index++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
            {
                Content = JsonContent.Create(new
                {
                    username = $"Limit_{Guid.NewGuid():N}"[..18],
                    email = $"{Guid.NewGuid():N}@example.com",
                    password = "Strong horse 456!",
                    confirmPassword = "Strong horse 456!"
                })
            };
            request.Headers.TryAddWithoutValidation("Origin", "http://localhost:5173");
            using var response = await client.SendAsync(request);
            status = response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
    }
}
