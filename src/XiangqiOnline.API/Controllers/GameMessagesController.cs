using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Interactions;
using XiangqiOnline.Contracts;
using XiangqiOnline.Contracts.Interactions;

namespace XiangqiOnline.API.Controllers;

[ApiController, Authorize, Route("api/v1/games/{gameId:guid}/messages")]
public sealed class GameMessagesController(IChatService chat) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid gameId, [FromQuery] string? before = null, [FromQuery] int limit = 40, CancellationToken ct = default)
    {
        Guid? cursor = null;
        if (before is not null && (!Guid.TryParse(before, out var parsed) || parsed == Guid.Empty))
            return BadRequest(ApiResponses.Failure("CHAT_CURSOR_INVALID", "Mốc tải tin nhắn không hợp lệ.", HttpContext));
        if (before is not null) cursor = Guid.Parse(before);
        var result = await chat.GetMessagesAsync(gameId, CurrentUserId(), cursor, limit, ct);
        if (result.Status == ChatPageStatus.NotParticipant) return StatusCode(403, ApiResponses.Failure("GAME_FORBIDDEN", "Bạn không có quyền xem tin nhắn của ván này.", HttpContext));
        if (result.Page is null) return BadRequest(ApiResponses.Failure("CHAT_CURSOR_INVALID", "Mốc tải tin nhắn không tồn tại hoặc giới hạn trang không hợp lệ.", HttpContext));
        var page = result.Page;
        return Ok(ApiResponses.Success(new ChatMessagesPageResponse(page.Items.Select(ToResponse).ToArray(), page.NextCursor?.ToString("D"), page.HasMore), HttpContext));
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;
    internal static ChatMessageResponse ToResponse(ChatMessageInfo m) => new(m.Id, m.GameId, m.SenderUserId, m.ClientMessageId, m.SenderDisplayName, m.Content, m.CreatedAtUtc);
}
