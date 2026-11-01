using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using XiangqiOnline.Application.Rooms;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.Infrastructure.Rooms;

public sealed class EfRoomRepository(AppDbContext db) : IRoomRepository
{
    public async Task<bool> CreateAsync(Room room, RoomMember owner, CancellationToken cancellationToken)
    {
        db.Rooms.Add(room);
        db.RoomMembers.Add(owner);
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException exception) when (FindSqlException(exception) is { Number: 2601 or 2627 })
        {
            db.Entry(room).State = EntityState.Detached;
            db.Entry(owner).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<RoomActionResult> JoinAsync(string? normalizedCode, Guid? roomId, Guid userId, bool allowPrivateById, DateTime now, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => JoinTransactionAsync(normalizedCode, roomId, userId, allowPrivateById, now, cancellationToken));
    }

    private async Task<RoomActionResult> JoinTransactionAsync(string? code, Guid? id, Guid userId, bool allowPrivateById, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var room = await LockRoomAsync(code, id, cancellationToken);
        if (room is null || (room.Visibility == Visibility.Private && id.HasValue && !allowPrivateById)) return new(RoomActionStatus.NotFound);
        var members = await ActiveMembers(room.Id, cancellationToken);
        if (members.Any(m => m.UserId == userId)) return new(RoomActionStatus.Success, await MapRoom(room, members, cancellationToken));
        if (room.Status == RoomStatus.Closed) return new(RoomActionStatus.Closed);
        if (room.Status == RoomStatus.InGame) return new(RoomActionStatus.NotWaiting);
        if (room.Status != RoomStatus.Waiting) return new(RoomActionStatus.NotWaiting);
        if (members.Count >= room.MaxPlayers) return new(RoomActionStatus.Full);
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken)) return new(RoomActionStatus.NotFound);

        var side = members.Any(m => m.Side == Side.Red) ? Side.Black : Side.Red;
        db.RoomMembers.Add(new RoomMember { RoomId = room.Id, UserId = userId, Role = RoomRole.Player, Side = side, JoinedAt = now });
        room.UpdatedAt = now;
        room.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return new(RoomActionStatus.Conflict); }
        catch (DbUpdateException exception) when (FindSqlException(exception) is { Number: 2601 or 2627 }) { return new(RoomActionStatus.Full); }
        return new(RoomActionStatus.Success, await LoadRoom(room.Id, cancellationToken));
    }

    public async Task<RoomInfo?> GetAsync(Guid roomId, Guid userId, CancellationToken cancellationToken)
    {
        var room = await LockRoomAsync(null, roomId, cancellationToken);
        if (room is null) return null;
        var members = await ActiveMembers(room.Id, cancellationToken);
        if (!members.Any(m => m.UserId == userId)) return null;
        return await MapRoom(room, members, cancellationToken);
    }

    public async Task<PublicRoomsPage> GetPublicRoomsAsync(string? search, RoomStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Rooms.AsNoTracking().Where(r => r.Visibility == Visibility.Public);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        else query = query.Where(r => r.Status == RoomStatus.Waiting);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.Name.Contains(term) || r.Code.Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var rooms = await query.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var result = new List<RoomInfo>(rooms.Count);
        foreach (var room in rooms)
            result.Add(await MapRoom(room, await ActiveMembers(room.Id, cancellationToken), cancellationToken));
        return new PublicRoomsPage(result, page, pageSize, total);
    }

    public Task<RoomActionResult> ChangeSideAsync(Guid roomId, Guid userId, Side side, long expectedVersion, DateTime now, CancellationToken cancellationToken) =>
        UpdateMemberAsync(roomId, userId, expectedVersion, now, (room, member, members) =>
        {
            if (members.Any(m => m.UserId != userId && m.Side == side)) return RoomActionStatus.SideTaken;
            member.Side = side;
            member.IsReady = false;
            return RoomActionStatus.Success;
        }, cancellationToken);

    public Task<RoomActionResult> SetReadyAsync(Guid roomId, Guid userId, bool isReady, long expectedVersion, DateTime now, CancellationToken cancellationToken) =>
        UpdateMemberAsync(roomId, userId, expectedVersion, now, (room, member, _) =>
        {
            if (isReady && member.Side is null) return RoomActionStatus.SideTaken;
            member.IsReady = isReady;
            return RoomActionStatus.Success;
        }, cancellationToken);

    private async Task<RoomActionResult> UpdateMemberAsync(Guid roomId, Guid userId, long expectedVersion, DateTime now,
        Func<Room, RoomMember, IReadOnlyList<RoomMemberInfo>, RoomActionStatus> update, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => UpdateMemberTransactionAsync(roomId, userId, expectedVersion, now, update, cancellationToken));
    }

    private async Task<RoomActionResult> UpdateMemberTransactionAsync(Guid roomId, Guid userId, long expectedVersion, DateTime now,
        Func<Room, RoomMember, IReadOnlyList<RoomMemberInfo>, RoomActionStatus> update, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var room = await LockRoomAsync(null, roomId, cancellationToken);
        if (room is null) return new(RoomActionStatus.NotFound);
        var members = await ActiveMembers(roomId, cancellationToken);
        var member = await db.RoomMembers.SingleOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId && m.LeftAt == null, cancellationToken);
        if (member is null) return new(RoomActionStatus.NotMember);
        if (room.Status != RoomStatus.Waiting) return new(RoomActionStatus.NotWaiting);
        if (room.Version != expectedVersion) return new(RoomActionStatus.Conflict);
        var result = update(room, member, members);
        if (result != RoomActionStatus.Success) return new(result);
        room.Version++;
        room.UpdatedAt = now;
        try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(RoomActionStatus.Conflict); }
        return new(RoomActionStatus.Success, await LoadRoom(roomId, cancellationToken));
    }

    public async Task<RoomActionResult> LeaveAsync(Guid roomId, Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => LeaveTransactionAsync(roomId, userId, now, cancellationToken));
    }

    private async Task<RoomActionResult> LeaveTransactionAsync(Guid roomId, Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room is null) return new(RoomActionStatus.NotFound);
        if (room.Status != RoomStatus.Waiting) return new(RoomActionStatus.NotWaiting);
        var member = await db.RoomMembers.SingleOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId && m.LeftAt == null, cancellationToken);
        if (member is null) return new(RoomActionStatus.NotMember);
        member.LeftAt = now;
        member.IsReady = false;
        var remaining = await db.RoomMembers.Where(m => m.RoomId == roomId && m.UserId != userId && m.LeftAt == null)
            .OrderBy(m => m.JoinedAt).ToListAsync(cancellationToken);
        if (room.OwnerId == userId)
        {
            if (remaining.Count == 0) { room.Status = RoomStatus.Closed; room.ClosedAt = now; }
            else { room.OwnerId = remaining[0].UserId; remaining[0].Role = RoomRole.Owner; }
        }
        room.Version++;
        room.UpdatedAt = now;
        try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(RoomActionStatus.Conflict); }
        return new(RoomActionStatus.Success, room.Status == RoomStatus.Closed ? null : await LoadRoom(roomId, cancellationToken));
    }

    public async Task<RoomActionResult> StartAsync(Guid roomId, Guid userId, long expectedVersion, string initialFen, DateTime now, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var room = await LockRoomAsync(null, roomId, cancellationToken);
            if (room is null) return new RoomActionResult(RoomActionStatus.NotFound);
            if (room.OwnerId != userId) return new RoomActionResult(RoomActionStatus.HostOnly);
            if (room.Status == RoomStatus.InGame || await db.Games.AnyAsync(g => g.RoomId == roomId, cancellationToken)) return new RoomActionResult(RoomActionStatus.GameAlreadyStarted);
            if (room.Status != RoomStatus.Waiting) return new RoomActionResult(RoomActionStatus.NotWaiting);
            if (room.Version != expectedVersion) return new RoomActionResult(RoomActionStatus.Conflict);
            var members = await db.RoomMembers.Where(m => m.RoomId == roomId && m.LeftAt == null).ToListAsync(cancellationToken);
            if (members.Count != 2 || members.Any(m => m.Side is null || !m.IsReady) || members.Select(m => m.Side).Distinct().Count() != 2)
                return new RoomActionResult(RoomActionStatus.NotReady);
            var red = members.Single(m => m.Side == Side.Red).UserId;
            var black = members.Single(m => m.Side == Side.Black).UserId;
            var game = new Game
            {
                RoomId = room.Id, RedPlayerId = red, BlackPlayerId = black, Mode = GameMode.Online,
                Status = GameStatus.InProgress, CurrentFen = initialFen, CurrentTurn = Side.Red, Version = 0,
                RedRemainingMs = 600_000, BlackRemainingMs = 600_000, TimeControlInitialMs = 600_000,
                TimeControlIncrementMs = 0, TurnStartedAtUtc = now, StartedAt = now, CreatedAt = now, UpdatedAt = now
            };
            db.Games.Add(game);
            db.GameSnapshots.Add(new GameSnapshot
            {
                GameId = game.Id, Ply = 0, Fen = initialFen, CurrentTurn = Side.Red,
                RedRemainingMs = game.RedRemainingMs, BlackRemainingMs = game.BlackRemainingMs,
                Version = game.Version, Metadata = JsonSerializer.Serialize(new { game.Status }), CreatedAt = now
            });
            db.GameEvents.Add(new GameEvent
            {
                GameId = game.Id, EventType = GameEventType.Started,
                Payload = JsonSerializer.Serialize(new { redPlayerId = red, blackPlayerId = black, timeControlInitialMs = game.TimeControlInitialMs, timeControlIncrementMs = game.TimeControlIncrementMs }),
                CreatedAt = now
            });
            room.Status = RoomStatus.InGame;
            room.UpdatedAt = now;
            room.Version++;
            try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { return new RoomActionResult(RoomActionStatus.Conflict); }
            return new RoomActionResult(RoomActionStatus.Success, await LoadRoom(roomId, cancellationToken));
        });
    }

    private async Task<RoomInfo> LoadRoom(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.SingleAsync(r => r.Id == roomId, cancellationToken);
        return await MapRoom(room, await ActiveMembers(roomId, cancellationToken), cancellationToken);
    }

    private async Task<Room?> LockRoomAsync(string? code, Guid? id, CancellationToken cancellationToken)
    {
        var query = id.HasValue
            ? db.Rooms.FromSqlInterpolated($"SELECT * FROM Rooms WITH (UPDLOCK, ROWLOCK) WHERE Id = {id.Value}")
            : db.Rooms.FromSqlInterpolated($"SELECT * FROM Rooms WITH (UPDLOCK, ROWLOCK) WHERE Code = {code}");
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<List<RoomMemberInfo>> ActiveMembers(Guid roomId, CancellationToken cancellationToken) =>
        await (from member in db.RoomMembers.AsNoTracking()
               join user in db.Users.AsNoTracking() on member.UserId equals user.Id
               where member.RoomId == roomId && member.LeftAt == null
               orderby member.JoinedAt
               select new RoomMemberInfo(member.UserId, user.Username, user.DisplayName, member.Side, member.Role, member.IsReady))
            .ToListAsync(cancellationToken);

    private async Task<RoomInfo> MapRoom(Room room, IReadOnlyList<RoomMemberInfo> members, CancellationToken cancellationToken)
    {
        var gameId = await db.Games.AsNoTracking().Where(game => game.RoomId == room.Id).OrderByDescending(game => game.CreatedAt)
            .Select(game => (Guid?)game.Id).FirstOrDefaultAsync(cancellationToken);
        return new RoomInfo(room.Id, room.Code, room.Name, room.Visibility, room.Status, room.OwnerId, room.MaxPlayers, room.Version,
            room.CreatedAt, room.UpdatedAt, room.ClosedAt, gameId, members);
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is SqlException sql) return sql;
        return null;
    }
}
