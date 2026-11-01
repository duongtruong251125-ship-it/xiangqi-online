using System.Security.Cryptography;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.GameEngine;

namespace XiangqiOnline.Application.Rooms;

public sealed record RoomMemberInfo(Guid UserId, string Username, string DisplayName, Side? Side, RoomRole Role, bool IsReady);
public sealed record RoomInfo(Guid Id, string Code, string Name, Visibility Visibility, RoomStatus Status, Guid OwnerId, int MaxPlayers, long Version, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ClosedAt, Guid? GameId, IReadOnlyList<RoomMemberInfo> Members);
public enum RoomActionStatus { Success, NotFound, InvalidCode, Full, AlreadyMember, NotMember, Closed, NotWaiting, SideTaken, HostOnly, NotReady, Conflict, GameAlreadyStarted }
public sealed record RoomActionResult(RoomActionStatus Status, RoomInfo? Room = null);
public sealed record PublicRoomsPage(IReadOnlyList<RoomInfo> Rooms, int Page, int PageSize, int TotalCount);

public interface IRoomRepository
{
    Task<bool> CreateAsync(Room room, RoomMember owner, CancellationToken cancellationToken);
    Task<RoomActionResult> JoinAsync(string? normalizedCode, Guid? roomId, Guid userId, bool allowPrivateById, DateTime now, CancellationToken cancellationToken);
    Task<RoomInfo?> GetAsync(Guid roomId, Guid userId, CancellationToken cancellationToken);
    Task<PublicRoomsPage> GetPublicRoomsAsync(string? search, RoomStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<RoomActionResult> ChangeSideAsync(Guid roomId, Guid userId, Side side, long expectedVersion, DateTime now, CancellationToken cancellationToken);
    Task<RoomActionResult> SetReadyAsync(Guid roomId, Guid userId, bool isReady, long expectedVersion, DateTime now, CancellationToken cancellationToken);
    Task<RoomActionResult> LeaveAsync(Guid roomId, Guid userId, DateTime now, CancellationToken cancellationToken);
    Task<RoomActionResult> StartAsync(Guid roomId, Guid userId, long expectedVersion, string initialFen, DateTime now, CancellationToken cancellationToken);
}

public interface IRoomService
{
    Task<RoomInfo> CreateAsync(Guid ownerId, string name, Visibility visibility, CancellationToken cancellationToken);
    Task<RoomActionResult> JoinByCodeAsync(Guid userId, string code, CancellationToken cancellationToken);
    Task<RoomActionResult> JoinAsync(Guid userId, Guid roomId, CancellationToken cancellationToken);
    Task<RoomInfo?> GetAsync(Guid roomId, Guid userId, CancellationToken cancellationToken);
    Task<PublicRoomsPage> GetPublicRoomsAsync(string? search, RoomStatus? status, int page, CancellationToken cancellationToken);
    Task<RoomActionResult> ChangeSideAsync(Guid roomId, Guid userId, Side side, long expectedVersion, CancellationToken cancellationToken);
    Task<RoomActionResult> SetReadyAsync(Guid roomId, Guid userId, bool isReady, long expectedVersion, CancellationToken cancellationToken);
    Task<RoomActionResult> LeaveAsync(Guid roomId, Guid userId, CancellationToken cancellationToken);
    Task<RoomActionResult> StartAsync(Guid roomId, Guid userId, long expectedVersion, CancellationToken cancellationToken);
}

public sealed class RoomService(IRoomRepository repository, TimeProvider timeProvider) : IRoomService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<RoomInfo> CreateAsync(Guid ownerId, string name, Visibility visibility, CancellationToken cancellationToken)
    {
        name = name.Trim();
        if (name.Length is < 3 or > 80) throw new ArgumentException("Tên phòng phải có từ 3 đến 80 ký tự.", nameof(name));
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = string.Create(6, 0, static (span, _) =>
            {
                for (var index = 0; index < span.Length; index++) span[index] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            });
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var room = new Room { Code = code, Name = name, OwnerId = ownerId, Visibility = visibility, Status = RoomStatus.Waiting, MaxPlayers = 2, CreatedAt = now, UpdatedAt = now };
            var owner = new RoomMember { RoomId = room.Id, UserId = ownerId, Role = RoomRole.Owner, Side = Side.Red, JoinedAt = now, IsReady = false };
            if (await repository.CreateAsync(room, owner, cancellationToken))
                return new RoomInfo(room.Id, room.Code, room.Name, room.Visibility, room.Status, ownerId, room.MaxPlayers, room.Version, room.CreatedAt, room.UpdatedAt, room.ClosedAt, null, [new RoomMemberInfo(ownerId, string.Empty, string.Empty, Side.Red, RoomRole.Owner, false)]);
        }
        throw new InvalidOperationException("Không thể tạo mã phòng duy nhất. Vui lòng thử lại.");
    }

    public Task<RoomActionResult> JoinByCodeAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length != 6 || normalizedCode.Any(character => !CodeAlphabet.Contains(character)))
            return Task.FromResult(new RoomActionResult(RoomActionStatus.InvalidCode));
        return repository.JoinAsync(normalizedCode, null, userId, false, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public Task<RoomActionResult> JoinAsync(Guid userId, Guid roomId, CancellationToken cancellationToken) =>
        repository.JoinAsync(null, roomId, userId, false, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    public Task<RoomInfo?> GetAsync(Guid roomId, Guid userId, CancellationToken cancellationToken) => repository.GetAsync(roomId, userId, cancellationToken);

    public Task<PublicRoomsPage> GetPublicRoomsAsync(string? search, RoomStatus? status, int page, CancellationToken cancellationToken) =>
        repository.GetPublicRoomsAsync(search?.Trim(), status, page, 12, cancellationToken);

    public Task<RoomActionResult> ChangeSideAsync(Guid roomId, Guid userId, Side side, long expectedVersion, CancellationToken cancellationToken) =>
        repository.ChangeSideAsync(roomId, userId, side, expectedVersion, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    public Task<RoomActionResult> SetReadyAsync(Guid roomId, Guid userId, bool isReady, long expectedVersion, CancellationToken cancellationToken) =>
        repository.SetReadyAsync(roomId, userId, isReady, expectedVersion, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    public Task<RoomActionResult> LeaveAsync(Guid roomId, Guid userId, CancellationToken cancellationToken) =>
        repository.LeaveAsync(roomId, userId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    public Task<RoomActionResult> StartAsync(Guid roomId, Guid userId, long expectedVersion, CancellationToken cancellationToken) =>
        repository.StartAsync(roomId, userId, expectedVersion, BoardState.CreateInitialBoard().ToFen(), timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}
