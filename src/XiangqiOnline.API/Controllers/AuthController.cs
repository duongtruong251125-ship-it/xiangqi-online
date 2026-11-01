using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Contracts.Authentication;

namespace XiangqiOnline.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthenticationService authentication, IConfiguration configuration, IHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "xiangqi_refresh";

    [AllowAnonymous, EnableRateLimiting("auth-register"), HttpPost("register")]
    [ProducesResponseType(typeof(ApiEnvelope<RegisterResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await authentication.RegisterAsync(request.Username, request.Email, request.Password, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponses.Success(new RegisterResponse(user.Id, user.Username, user.DisplayName), HttpContext));
    }

    [AllowAnonymous, EnableRateLimiting("auth-login"), HttpPost("login")]
    [ProducesResponseType(typeof(ApiEnvelope<AuthSessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authentication.LoginSessionAsync(request.Identifier, request.Password, request.RememberMe, cancellationToken);
        WriteRefreshCookie(session.RefreshToken, session.IsPersistent);
        return Ok(ApiResponses.Success(ToResponse(session), HttpContext));
    }

    [AllowAnonymous, EnableRateLimiting("auth-refresh"), HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        ValidateCookieRequestOrigin();
        var session = await authentication.RefreshAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        WriteRefreshCookie(session.RefreshToken, session.IsPersistent);
        return Ok(ApiResponses.Success(ToResponse(session), HttpContext));
    }

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        ValidateCookieRequestOrigin();
        await authentication.LogoutAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        ClearRefreshCookie();
        return Ok(ApiResponses.Success(new { loggedOut = true }, HttpContext));
    }

    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await authentication.LogoutAllAsync(CurrentUserId(), cancellationToken);
        ClearRefreshCookie();
        return Ok(ApiResponses.Success(new { loggedOut = true }, HttpContext));
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id
        : throw new AuthException("AUTH_UNAUTHORIZED", "Phiên đăng nhập không hợp lệ.", 401);

    private static AuthSessionResponse ToResponse(CreatedSession session) =>
        new(session.AccessToken.Value, Math.Max(0, (int)(session.AccessToken.ExpiresAt - DateTime.UtcNow).TotalSeconds), ToResponse(session.User));

    private static UserResponse ToResponse(AuthUser user) =>
        new(user.Id, user.Username, user.Email, user.DisplayName, user.AvatarUrl, user.CreatedAt);

    private void WriteRefreshCookie(string token, bool persistent) => Response.Cookies.Append(RefreshCookieName, token, RefreshCookieOptions(persistent));
    private void ClearRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());

    private CookieOptions RefreshCookieOptions(bool persistent = true) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",
        MaxAge = persistent ? TimeSpan.FromDays(7) : null,
        IsEssential = true
    };

    private void ValidateCookieRequestOrigin()
    {
        var origin = Request.Headers.Origin.ToString().TrimEnd('/');
        var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var sameOrigin = string.Equals(origin, $"{Request.Scheme}://{Request.Host}", StringComparison.OrdinalIgnoreCase);
        if (origin.Length == 0 || (!sameOrigin && !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)))
            throw new AuthException("CSRF_ORIGIN_INVALID", "Nguồn yêu cầu không được phép.", 403);
    }
}
