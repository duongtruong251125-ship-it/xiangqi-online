using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Application.Players;
using XiangqiOnline.Contracts.Authentication;

namespace XiangqiOnline.API.Controllers;

[ApiController, Authorize]
[Route("api/v1/users/me")]
public sealed class UsersController(IAuthenticationService authentication, IPlayerStatsService playerStats) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(ApiResponses.Success(ToResponse(await authentication.GetUserAsync(CurrentUserId(), cancellationToken)), HttpContext));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken) =>
        Ok(ApiResponses.Success(await playerStats.GetAsync(CurrentUserId(), cancellationToken), HttpContext));

    [HttpPatch]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponses.Success(ToResponse(await authentication.UpdateProfileAsync(CurrentUserId(), request.DisplayName, cancellationToken)), HttpContext));

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await authentication.ChangePasswordAsync(CurrentUserId(), request.CurrentPassword, request.NewPassword, cancellationToken);
        Response.Cookies.Delete("xiangqi_refresh", new CookieOptions { Path = "/api/v1/auth", HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict });
        return Ok(ApiResponses.Success(new { changed = true, accessTokenExpiresInSeconds = "The current access token remains valid for at most 15 minutes." }, HttpContext));
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id
        : throw new AuthException("AUTH_UNAUTHORIZED", "Phiên đăng nhập không hợp lệ.", 401);

    private static UserResponse ToResponse(AuthUser user) => new(user.Id, user.Username, user.Email, user.DisplayName, user.AvatarUrl, user.CreatedAt);
}
