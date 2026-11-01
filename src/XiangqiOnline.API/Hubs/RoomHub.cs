using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using XiangqiOnline.API.Controllers;
using XiangqiOnline.Application.Rooms;

namespace XiangqiOnline.API.Hubs;

[Authorize]
public sealed class RoomHub(IRoomService rooms) : Hub
{
    public static string RoomGroup(Guid roomId) => $"room:{roomId:N}";
    public static string UserGroup(Guid userId) => $"user:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId)) await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    public async Task JoinRoomGroup(Guid roomId)
    {
        var state = await AuthorizedRoom(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
        await Clients.Caller.SendAsync("RoomStateSynced", RoomsController.ToResponse(state));
    }

    public async Task RequestRoomSync(Guid roomId)
    {
        var state = await AuthorizedRoom(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
        await Clients.Caller.SendAsync("RoomStateSynced", RoomsController.ToResponse(state));
    }

    public Task LeaveRoomGroup(Guid roomId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));

    private async Task<RoomInfo> AuthorizedRoom(Guid roomId)
    {
        if (!TryGetUserId(out var userId)) throw new HubException("Phiên đăng nhập không hợp lệ.");
        var room = await rooms.GetAsync(roomId, userId, Context.ConnectionAborted);
        return room ?? throw new HubException("Bạn không có quyền truy cập phòng này.");
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
