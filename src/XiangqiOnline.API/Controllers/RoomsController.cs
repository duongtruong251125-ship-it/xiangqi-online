using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Application.Rooms;
using XiangqiOnline.Contracts.Authentication;
using XiangqiOnline.Domain.Enums;

namespace XiangqiOnline.API.Controllers;

[ApiController, Authorize]
[Route("api/v1/rooms")]
public sealed class RoomsController(IRoomService rooms, IRoomEventNotifier events) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 3 or > 80)
            return BadRequest(ApiResponses.Failure("VALIDATION_ERROR", "Tên phòng phải có từ 3 đến 80 ký tự.", HttpContext));
        var userId = CurrentUserId();
        var room = await rooms.CreateAsync(userId, request.Name, request.IsPrivate ? Visibility.Private : Visibility.Public, cancellationToken);
        room = await rooms.GetAsync(room.Id, userId, cancellationToken) ?? room;
        await events.RoomChangedAsync(room, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponses.Success(ToResponse(room), HttpContext));
    }

    [HttpGet]
    public async Task<IActionResult> GetPublic([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1000) return BadRequest(ApiResponses.Failure("INVALID_PAGINATION", "Số trang không hợp lệ.", HttpContext));
        RoomStatus? roomStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RoomStatus>(status, true, out var parsed))
                return BadRequest(ApiResponses.Failure("VALIDATION_ERROR", "Trạng thái phòng không hợp lệ.", HttpContext));
            roomStatus = parsed;
        }
        var result = await rooms.GetPublicRoomsAsync(search, roomStatus, page, cancellationToken);
        return Ok(ApiResponses.Success(new PublicRoomsResponse(result.Rooms.Select(ToResponse).ToArray(), result.Page, result.PageSize, result.TotalCount), HttpContext));
    }

    [HttpPost("join")]
    public Task<IActionResult> JoinByCode(JoinRoomRequest request, CancellationToken cancellationToken) =>
        CompleteJoin(rooms.JoinByCodeAsync(CurrentUserId(), request.Code ?? string.Empty, cancellationToken), cancellationToken);

    [HttpPost("join-public")]
    public Task<IActionResult> JoinPublic(JoinPublicRoomRequest request, CancellationToken cancellationToken) =>
        CompleteJoin(rooms.JoinAsync(CurrentUserId(), request.RoomId, cancellationToken), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, CurrentUserId(), cancellationToken);
        return room is null
            ? NotFound(ApiResponses.Failure("ROOM_NOT_FOUND", "Không tìm thấy phòng hoặc bạn không phải thành viên.", HttpContext))
            : Ok(ApiResponses.Success(ToResponse(room), HttpContext));
    }

    [HttpPost("{id:guid}/side")]
    public async Task<IActionResult> ChangeSide(Guid id, ChangeRoomSideRequest request, CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 0 || !Enum.TryParse<Side>(request.Side, true, out var side))
            return BadRequest(ApiResponses.Failure("VALIDATION_ERROR", "Phe hoặc phiên bản không hợp lệ.", HttpContext));
        return await CompleteMutation(rooms.ChangeSideAsync(id, CurrentUserId(), side, request.ExpectedVersion, cancellationToken), cancellationToken);
    }

    [HttpPost("{id:guid}/ready")]
    public async Task<IActionResult> SetReady(Guid id, SetRoomReadyRequest request, CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 0) return BadRequest(ApiResponses.Failure("VALIDATION_ERROR", "Phiên bản không hợp lệ.", HttpContext));
        return await CompleteMutation(rooms.SetReadyAsync(id, CurrentUserId(), request.IsReady, request.ExpectedVersion, cancellationToken), cancellationToken);
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        var result = await rooms.LeaveAsync(id, CurrentUserId(), cancellationToken);
        if (result.Status != RoomActionStatus.Success) return MapAction(result.Status);
        if (result.Room is not null) await events.RoomChangedAsync(result.Room, cancellationToken);
        else await events.RoomClosedAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, StartRoomRequest request, CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 0) return BadRequest(ApiResponses.Failure("VALIDATION_ERROR", "Phiên bản không hợp lệ.", HttpContext));
        var result = await rooms.StartAsync(id, CurrentUserId(), request.ExpectedVersion, cancellationToken);
        if (result.Status != RoomActionStatus.Success || result.Room is null) return MapAction(result.Status);
        await events.RoomChangedAsync(result.Room, cancellationToken);
        await events.GameStartedAsync(result.Room, cancellationToken);
        return Ok(ApiResponses.Success(ToResponse(result.Room), HttpContext));
    }

    private async Task<IActionResult> CompleteJoin(Task<RoomActionResult> task, CancellationToken cancellationToken)
    {
        var result = await task;
        if (result.Status != RoomActionStatus.Success || result.Room is null) return MapAction(result.Status);
        await events.RoomChangedAsync(result.Room, cancellationToken);
        return Ok(ApiResponses.Success(ToResponse(result.Room), HttpContext));
    }

    private async Task<IActionResult> CompleteMutation(Task<RoomActionResult> task, CancellationToken cancellationToken)
    {
        var result = await task;
        if (result.Status != RoomActionStatus.Success || result.Room is null) return MapAction(result.Status);
        await events.RoomChangedAsync(result.Room, cancellationToken);
        return Ok(ApiResponses.Success(ToResponse(result.Room), HttpContext));
    }

    private IActionResult MapAction(RoomActionStatus status) => status switch
    {
        RoomActionStatus.InvalidCode => BadRequest(ApiResponses.Failure("ROOM_INVALID_CODE", "Mã phòng không đúng định dạng.", HttpContext)),
        RoomActionStatus.Full => Conflict(ApiResponses.Failure("ROOM_FULL", "Phòng đã đủ người chơi.", HttpContext)),
        RoomActionStatus.AlreadyMember => Conflict(ApiResponses.Failure("ROOM_ALREADY_JOINED", "Bạn đã ở trong phòng này.", HttpContext)),
        RoomActionStatus.NotMember => StatusCode(403, ApiResponses.Failure("ROOM_NOT_MEMBER", "Bạn không phải thành viên phòng.", HttpContext)),
        RoomActionStatus.Closed => Conflict(ApiResponses.Failure("ROOM_CLOSED", "Phòng đã đóng.", HttpContext)),
        RoomActionStatus.NotWaiting => Conflict(ApiResponses.Failure("ROOM_ALREADY_STARTED", "Phòng không còn ở trạng thái chờ.", HttpContext)),
        RoomActionStatus.SideTaken => Conflict(ApiResponses.Failure("ROOM_SIDE_TAKEN", "Phe đã chọn hoặc bạn cần chọn phe trước khi sẵn sàng.", HttpContext)),
        RoomActionStatus.HostOnly => StatusCode(403, ApiResponses.Failure("ROOM_HOST_ONLY", "Chỉ chủ phòng được bắt đầu trận.", HttpContext)),
        RoomActionStatus.NotReady => Conflict(ApiResponses.Failure("ROOM_NOT_READY", "Cần đủ hai người, chọn hai phe khác nhau và cả hai cùng sẵn sàng.", HttpContext)),
        RoomActionStatus.GameAlreadyStarted => Conflict(ApiResponses.Failure("GAME_ALREADY_STARTED", "Ván đấu của phòng đã được tạo.", HttpContext)),
        RoomActionStatus.Conflict => Conflict(ApiResponses.Failure("ROOM_CONFLICT", "Phòng vừa thay đổi. Hãy tải lại trạng thái.", HttpContext)),
        _ => NotFound(ApiResponses.Failure("ROOM_NOT_FOUND", "Không tìm thấy phòng.", HttpContext))
    };

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id
        : throw new AuthException("AUTH_UNAUTHORIZED", "Phiên đăng nhập không hợp lệ.", 401);

    public static RoomResponse ToResponse(RoomInfo room) => new(room.Id, room.Code, room.Name, room.Visibility == Visibility.Private,
        room.Status.ToString(), room.OwnerId, room.MaxPlayers, room.Version, room.CreatedAt, room.UpdatedAt, room.ClosedAt, room.GameId,
        room.Members.Select(m => new RoomMemberResponse(m.UserId, m.Username, m.DisplayName, m.Side?.ToString(), m.Role.ToString(), m.IsReady)).ToArray());
}
