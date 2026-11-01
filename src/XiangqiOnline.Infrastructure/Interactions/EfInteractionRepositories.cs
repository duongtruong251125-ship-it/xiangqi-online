using Microsoft.EntityFrameworkCore;
using XiangqiOnline.Application.Interactions;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.Infrastructure.Interactions;

public sealed class EfChatRepository(AppDbContext db) : IChatRepository
{
    public Task<bool> IsParticipantAsync(Guid gameId, Guid userId, CancellationToken ct) => db.Games.AnyAsync(g => g.Id == gameId && g.Mode == GameMode.Online && (g.RedPlayerId == userId || g.BlackPlayerId == userId), ct);

    public Task<ChatMessageInfo?> GetByClientMessageIdAsync(Guid gameId, Guid senderUserId, Guid clientMessageId, CancellationToken ct) =>
        db.ChatMessages.AsNoTracking().Where(m => m.GameId == gameId && m.SenderUserId == senderUserId && m.ClientMessageId == clientMessageId)
            .Join(db.Users, m => m.SenderUserId, u => u.Id, (m, u) => new ChatMessageInfo(m.Id, m.GameId, m.SenderUserId, m.ClientMessageId, u.DisplayName, m.Content, m.CreatedAtUtc)).SingleOrDefaultAsync(ct);

    public async Task<bool> SaveAsync(ChatMessage message, CancellationToken ct)
    {
        db.ChatMessages.Add(message);
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException) { db.Entry(message).State = EntityState.Detached; return false; }
    }

    public async Task<ChatMessagesPageInfo?> GetPageAsync(Guid gameId, Guid? beforeMessageId, int limit, CancellationToken ct)
    {
        DateTime? beforeAt = null;
        if (beforeMessageId is { } cursor)
        {
            beforeAt = await db.ChatMessages.Where(m => m.GameId == gameId && m.Id == cursor).Select(m => (DateTime?)m.CreatedAtUtc).SingleOrDefaultAsync(ct);
            if (beforeAt is null) return null;
        }
        var query = db.ChatMessages.AsNoTracking().Where(m => m.GameId == gameId);
        if (beforeAt is { } timestamp)
            query = query.Where(m => m.CreatedAtUtc < timestamp || (m.CreatedAtUtc == timestamp && m.Id.CompareTo(beforeMessageId!.Value) < 0));
        var rows = await query.OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id).Take(limit + 1)
            .Join(db.Users, m => m.SenderUserId, u => u.Id, (m, u) => new ChatMessageInfo(m.Id, m.GameId, m.SenderUserId, m.ClientMessageId, u.DisplayName, m.Content, m.CreatedAtUtc)).ToListAsync(ct);
        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        rows.Reverse();
        return new(rows, hasMore && rows.Count > 0 ? rows[0].Id : null, hasMore);
    }
}

public sealed class EfCallSessionRepository(AppDbContext db) : ICallSessionRepository
{
    private static readonly CallStatus[] Active = [CallStatus.Ringing, CallStatus.Accepted, CallStatus.Connecting, CallStatus.Connected];
    public Task<InteractionGameInfo?> GetEligibleGameAsync(Guid gameId, CancellationToken ct) =>
        db.Games.AsNoTracking().Where(g => g.Id == gameId).Join(db.Users, g => g.RedPlayerId, red => (Guid?)red.Id,
            (g, red) => new { g, red }).Join(db.Users, x => x.g.BlackPlayerId, black => (Guid?)black.Id,
            (x, black) => new InteractionGameInfo(x.g.Id, x.g.Mode, x.g.Status, x.g.RedPlayerId, x.g.BlackPlayerId, x.red.DisplayName, black.DisplayName)).SingleOrDefaultAsync(ct);

    public Task<CallSessionInfo?> GetAsync(Guid callId, CancellationToken ct) => Map(db.CallSessions.AsNoTracking().Where(c => c.Id == callId)).SingleOrDefaultAsync(ct);

    public Task<CallSessionInfo?> GetActiveForParticipantAsync(Guid gameId, Guid userId, CancellationToken ct) =>
        Map(db.CallSessions.AsNoTracking().Where(c => c.GameId == gameId && Active.Contains(c.Status) && (c.CallerUserId == userId || c.CalleeUserId == userId))).FirstOrDefaultAsync(ct);

    public async Task<PersistCallStatus> CreateAsync(CallSession session, CancellationToken ct)
    {
        if (await db.CallSessions.AnyAsync(c => c.GameId == session.GameId && Active.Contains(c.Status), ct)) return PersistCallStatus.Busy;
        db.CallSessions.Add(session);
        try { await db.SaveChangesAsync(ct); return PersistCallStatus.Saved; }
        catch (DbUpdateException)
        {
            db.Entry(session).State = EntityState.Detached;
            return await db.CallSessions.AnyAsync(c => c.GameId == session.GameId && Active.Contains(c.Status), ct) ? PersistCallStatus.Busy : PersistCallStatus.Conflict;
        }
    }

    public async Task<PersistCallStatus> TransitionAsync(Guid callId, long expectedVersion, CallStatus expectedStatus, CallStatus nextStatus, DateTime nowUtc, string? endReason, CancellationToken ct)
    {
        var call = await db.CallSessions.SingleOrDefaultAsync(c => c.Id == callId, ct);
        if (call is null) return PersistCallStatus.NotFound;
        if (call.Version != expectedVersion || call.Status != expectedStatus) return PersistCallStatus.Conflict;
        call.Status = nextStatus;
        call.Version++;
        if (nextStatus == CallStatus.Accepted) call.AcceptedAtUtc = nowUtc;
        if (nextStatus == CallStatus.Connected) call.StartedAtUtc = nowUtc;
        if (nextStatus is CallStatus.Rejected or CallStatus.Missed or CallStatus.Ended or CallStatus.Failed) { call.EndedAtUtc = nowUtc; call.EndReason = endReason; }
        try { await db.SaveChangesAsync(ct); return PersistCallStatus.Saved; }
        catch (DbUpdateConcurrencyException) { return PersistCallStatus.Conflict; }
    }

    public async Task<IReadOnlyList<CallSessionInfo>> GetExpiredRingingAsync(DateTime nowUtc, CancellationToken ct) =>
        await Map(db.CallSessions.AsNoTracking().Where(c => c.Status == CallStatus.Ringing && c.ExpiresAtUtc <= nowUtc)).ToListAsync(ct);

    private IQueryable<CallSessionInfo> Map(IQueryable<CallSession> calls) =>
        from c in calls
        join caller in db.Users on c.CallerUserId equals caller.Id
        join callee in db.Users on c.CalleeUserId equals callee.Id
        select new CallSessionInfo(c.Id, c.GameId, c.CallerUserId, c.CalleeUserId, c.Status, c.CreatedAtUtc, c.ExpiresAtUtc,
            c.AcceptedAtUtc, c.StartedAtUtc, c.EndedAtUtc, c.EndReason, c.Version, caller.DisplayName, callee.DisplayName);
}
