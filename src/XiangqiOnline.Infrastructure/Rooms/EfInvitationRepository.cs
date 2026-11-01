using System.Data;
using Microsoft.EntityFrameworkCore;
using XiangqiOnline.Application.Rooms;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.Infrastructure.Rooms;

public sealed class EfInvitationRepository(AppDbContext db) : IInvitationRepository
{
    public async Task<InvitationOperationResult> CreateAsync(Guid roomId, Guid senderId, string normalizedUsername, DateTime now, DateTime expiresAt, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => CreateTransactionAsync(roomId, senderId, normalizedUsername, now, expiresAt, cancellationToken));
    }

    private async Task<InvitationOperationResult> CreateTransactionAsync(Guid roomId, Guid senderId, string normalizedUsername, DateTime now, DateTime expiresAt, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var room = await LockRoomAsync(roomId, cancellationToken);
        if (room is null) return new(InvitationOperationStatus.NotFound);
        if (room.Status != RoomStatus.Waiting) return new(InvitationOperationStatus.RoomUnavailable);
        var member = await db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == senderId && m.LeftAt == null && m.Role != RoomRole.Spectator, cancellationToken);
        if (!member) return new(InvitationOperationStatus.NotAllowed);
        var invitee = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);
        if (invitee is null) return new(InvitationOperationStatus.NotFound);
        if (invitee.Id == senderId) return new(InvitationOperationStatus.SelfInvite);
        if (await db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == invitee.Id && m.LeftAt == null, cancellationToken))
            return new(InvitationOperationStatus.Duplicate);
        if (await db.RoomMembers.CountAsync(m => m.RoomId == roomId && m.LeftAt == null, cancellationToken) >= room.MaxPlayers)
            return new(InvitationOperationStatus.Full);

        var pending = await db.Invitations.SingleOrDefaultAsync(i => i.RoomId == roomId && i.InviteeId == invitee.Id && i.Status == InvitationStatus.Pending, cancellationToken);
        if (pending is not null)
        {
            if (pending.ExpiresAt > now) return new(InvitationOperationStatus.Duplicate);
            pending.Status = InvitationStatus.Expired;
            pending.RespondedAt = now;
        }
        var invitation = new Invitation { RoomId = roomId, InviterId = senderId, InviteeId = invitee.Id, Status = InvitationStatus.Pending, CreatedAt = now, ExpiresAt = expiresAt };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(InvitationOperationStatus.Success, await MapInvitation(invitation.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<InvitationInfo>> GetIncomingAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        await db.Invitations.Where(i => i.InviteeId == userId && i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Status, InvitationStatus.Expired).SetProperty(i => i.RespondedAt, now), cancellationToken);
        return await (from invitation in db.Invitations.AsNoTracking()
                      join room in db.Rooms.AsNoTracking() on invitation.RoomId equals room.Id
                      join inviter in db.Users.AsNoTracking() on invitation.InviterId equals inviter.Id
                      join invitee in db.Users.AsNoTracking() on invitation.InviteeId equals invitee.Id
                      where invitation.InviteeId == userId && invitation.Status == InvitationStatus.Pending
                      orderby invitation.CreatedAt descending
                      select new InvitationInfo(invitation.Id, invitation.RoomId, room.Code, room.Name, invitation.InviterId,
                          inviter.Username, invitee.Id, invitee.Username, invitation.Status, invitation.CreatedAt, invitation.ExpiresAt, invitation.RespondedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvitationOperationResult> RespondAsync(Guid invitationId, Guid userId, bool accept, string initialFen, DateTime now, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => RespondTransactionAsync(invitationId, userId, accept, initialFen, now, cancellationToken));
    }

    private async Task<InvitationOperationResult> RespondTransactionAsync(Guid invitationId, Guid userId, bool accept, string initialFen, DateTime now, CancellationToken cancellationToken)
    {
        var roomId = await db.Invitations.AsNoTracking().Where(i => i.Id == invitationId).Select(i => (Guid?)i.RoomId).SingleOrDefaultAsync(cancellationToken);
        if (!roomId.HasValue) return new(InvitationOperationStatus.NotFound);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var room = await LockRoomAsync(roomId.Value, cancellationToken);
        var invitation = await db.Invitations.FromSqlInterpolated($"SELECT * FROM Invitations WITH (UPDLOCK, ROWLOCK) WHERE Id = {invitationId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (invitation is null || invitation.InviteeId != userId) return new(InvitationOperationStatus.NotFound);
        if (invitation.Status != InvitationStatus.Pending) return new(InvitationOperationStatus.AlreadyHandled);
        if (invitation.ExpiresAt <= now)
        {
            invitation.Status = InvitationStatus.Expired;
            invitation.RespondedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InvitationOperationStatus.Expired, await MapInvitation(invitationId, cancellationToken));
        }

        if (!accept)
        {
            invitation.Status = InvitationStatus.Declined;
            invitation.RespondedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InvitationOperationStatus.Success, await MapInvitation(invitationId, cancellationToken));
        }
        if (room is null || room.Status != RoomStatus.Waiting) return new(InvitationOperationStatus.RoomUnavailable, await MapInvitation(invitationId, cancellationToken));

        var members = await db.RoomMembers.Where(m => m.RoomId == room.Id && m.LeftAt == null).ToListAsync(cancellationToken);
        if (!members.Any(m => m.UserId == userId))
        {
            if (members.Count >= room.MaxPlayers) return new(InvitationOperationStatus.Full, await MapInvitation(invitationId, cancellationToken));
            var side = members.Any(m => m.Side == Side.Red) ? Side.Black : Side.Red;
            db.RoomMembers.Add(new RoomMember { RoomId = room.Id, UserId = userId, Role = RoomRole.Player, Side = side, JoinedAt = now });
            room.Version++;
            room.UpdatedAt = now;
        }
        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return new(InvitationOperationStatus.Conflict); }
        catch (DbUpdateException) { return new(InvitationOperationStatus.Conflict); }
        return new(InvitationOperationStatus.Success, await MapInvitation(invitationId, cancellationToken), await MapRoom(room.Id, cancellationToken));
    }

    private async Task<InvitationInfo> MapInvitation(Guid id, CancellationToken cancellationToken) =>
        await (from invitation in db.Invitations.AsNoTracking()
               join room in db.Rooms.AsNoTracking() on invitation.RoomId equals room.Id
               join inviter in db.Users.AsNoTracking() on invitation.InviterId equals inviter.Id
               join invitee in db.Users.AsNoTracking() on invitation.InviteeId equals invitee.Id
               where invitation.Id == id
               select new InvitationInfo(invitation.Id, invitation.RoomId, room.Code, room.Name, invitation.InviterId,
                   inviter.Username, invitee.Id, invitee.Username, invitation.Status, invitation.CreatedAt, invitation.ExpiresAt, invitation.RespondedAt))
            .SingleAsync(cancellationToken);

    private async Task<RoomInfo> MapRoom(Guid id, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.SingleAsync(r => r.Id == id, cancellationToken);
        var members = await (from member in db.RoomMembers.AsNoTracking()
                             join user in db.Users.AsNoTracking() on member.UserId equals user.Id
                             where member.RoomId == id && member.LeftAt == null
                             orderby member.JoinedAt
                             select new RoomMemberInfo(member.UserId, user.Username, user.DisplayName, member.Side, member.Role, member.IsReady))
            .ToListAsync(cancellationToken);
        var gameId = await db.Games.AsNoTracking().Where(g => g.RoomId == id).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(cancellationToken);
        return new RoomInfo(room.Id, room.Code, room.Name, room.Visibility, room.Status, room.OwnerId, room.MaxPlayers, room.Version,
            room.CreatedAt, room.UpdatedAt, room.ClosedAt, gameId, members);
    }

    private Task<Room?> LockRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        db.Rooms.FromSqlInterpolated($"SELECT * FROM Rooms WITH (UPDLOCK, ROWLOCK) WHERE Id = {roomId}")
            .SingleOrDefaultAsync(cancellationToken);
}
