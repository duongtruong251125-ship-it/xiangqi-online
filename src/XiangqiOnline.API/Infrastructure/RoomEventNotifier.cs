using Microsoft.AspNetCore.SignalR;
using XiangqiOnline.API.Hubs;
using XiangqiOnline.Application.Rooms;

namespace XiangqiOnline.API.Infrastructure;

public interface IRoomEventNotifier
{
    Task RoomChangedAsync(RoomInfo room, CancellationToken cancellationToken);
    Task RoomClosedAsync(Guid roomId, CancellationToken cancellationToken);
    Task GameStartedAsync(RoomInfo room, CancellationToken cancellationToken);
    Task InvitationReceivedAsync(InvitationInfo invitation, CancellationToken cancellationToken);
}

public sealed class RoomEventNotifier(IHubContext<RoomHub> hub) : IRoomEventNotifier
{
    public Task RoomChangedAsync(RoomInfo room, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.RoomGroup(room.Id)).SendAsync("RoomChanged", new { roomId = room.Id, room.Version }, cancellationToken);

    public Task RoomClosedAsync(Guid roomId, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.RoomGroup(roomId)).SendAsync("RoomClosed", new { roomId }, cancellationToken);

    public Task GameStartedAsync(RoomInfo room, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.RoomGroup(room.Id)).SendAsync("GameStarted", new { roomId = room.Id }, cancellationToken);

    public Task InvitationReceivedAsync(InvitationInfo invitation, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.UserGroup(invitation.InviteeId)).SendAsync("InvitationReceived", invitation, cancellationToken);
}
