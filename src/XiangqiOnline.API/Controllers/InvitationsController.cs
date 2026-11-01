using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Application.Rooms;
using XiangqiOnline.Contracts.Authentication;

namespace XiangqiOnline.API.Controllers;

[ApiController, Authorize]
[Route("api/v1")]
public sealed class InvitationsController(IInvitationService invitations, IRoomEventNotifier events) : ControllerBase
{
    [HttpPost("rooms/{id:guid}/invitations"), EnableRateLimiting("room-invitation")]
    public async Task<IActionResult> Create(Guid id, CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        var result = await invitations.CreateAsync(id, CurrentUserId(), request.Username ?? string.Empty, cancellationToken);
        if (result.Status != InvitationOperationStatus.Success || result.Invitation is null) return Map(result.Status);
        await events.InvitationReceivedAsync(result.Invitation, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponses.Success(ToResponse(result.Invitation), HttpContext));
    }

    [HttpGet("invitations")]
    public async Task<IActionResult> GetIncoming(CancellationToken cancellationToken)
    {
        var list = await invitations.GetIncomingAsync(CurrentUserId(), cancellationToken);
        return Ok(ApiResponses.Success(list.Select(ToResponse).ToArray(), HttpContext));
    }

    [HttpPost("invitations/{id:guid}/accept")]
    public Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken) => Respond(id, true, cancellationToken);

    [HttpPost("invitations/{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken) => Respond(id, false, cancellationToken);

    private async Task<IActionResult> Respond(Guid id, bool accept, CancellationToken cancellationToken)
    {
        var result = await invitations.RespondAsync(id, CurrentUserId(), accept, cancellationToken);
        if (result.Status != InvitationOperationStatus.Success || result.Invitation is null) return Map(result.Status);
        if (result.Room is not null) await events.RoomChangedAsync(result.Room, cancellationToken);
        return Ok(ApiResponses.Success(ToResponse(result.Invitation), HttpContext));
    }

    private IActionResult Map(InvitationOperationStatus status) => status switch
    {
        InvitationOperationStatus.NotAllowed => StatusCode(403, ApiResponses.Failure("INVITATION_NOT_ALLOWED", "Bạn không có quyền mời người chơi từ phòng này.", HttpContext)),
        InvitationOperationStatus.SelfInvite => BadRequest(ApiResponses.Failure("INVITATION_SELF", "Không thể tự gửi lời mời cho mình.", HttpContext)),
        InvitationOperationStatus.Duplicate => Conflict(ApiResponses.Failure("INVITATION_ALREADY_PENDING", "Lời mời đang còn hiệu lực hoặc người chơi đã ở trong phòng.", HttpContext)),
        InvitationOperationStatus.RoomUnavailable => Conflict(ApiResponses.Failure("ROOM_CLOSED", "Phòng không nhận thêm lời mời.", HttpContext)),
        InvitationOperationStatus.Full => Conflict(ApiResponses.Failure("ROOM_FULL", "Phòng đã đủ người; lời mời vẫn còn chờ xử lý.", HttpContext)),
        InvitationOperationStatus.Expired => Conflict(ApiResponses.Failure("INVITATION_EXPIRED", "Lời mời đã hết hạn.", HttpContext)),
        InvitationOperationStatus.AlreadyHandled => Conflict(ApiResponses.Failure("INVITATION_ALREADY_HANDLED", "Lời mời đã được xử lý.", HttpContext)),
        InvitationOperationStatus.Conflict => Conflict(ApiResponses.Failure("ROOM_CONFLICT", "Dữ liệu vừa thay đổi. Hãy tải lại rồi thử lại.", HttpContext)),
        _ => NotFound(ApiResponses.Failure("INVITATION_NOT_FOUND", "Không tìm thấy lời mời hoặc tài khoản.", HttpContext))
    };

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id
        : throw new AuthException("AUTH_UNAUTHORIZED", "Phiên đăng nhập không hợp lệ.", 401);

    private static InvitationResponse ToResponse(InvitationInfo invitation) => new(invitation.Id, invitation.RoomId, invitation.RoomCode,
        invitation.RoomName, invitation.InviterId, invitation.InviterUsername, invitation.InviteeId, invitation.InviteeUsername,
        invitation.Status.ToString(), invitation.CreatedAt, invitation.ExpiresAt, invitation.RespondedAt);
}
