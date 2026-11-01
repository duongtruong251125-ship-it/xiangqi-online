using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using XiangqiOnline.Application.AiGames;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.Infrastructure.Persistence;
using PersistedStatus = XiangqiOnline.Domain.Enums.GameStatus;

namespace XiangqiOnline.Infrastructure.AiGames;

public sealed class EfAiGameRepository(AppDbContext db) : IAiGameRepository
{
    public Task<Game?> GetForHumanAsync(Guid gameId, Guid userId, CancellationToken ct) =>
        db.Games.AsNoTracking().SingleOrDefaultAsync(g => g.Id == gameId && g.Mode == GameMode.Ai && g.HumanPlayerId == userId, ct);

    public Task<Game?> GetAsync(Guid gameId, CancellationToken ct) =>
        db.Games.AsNoTracking().SingleOrDefaultAsync(g => g.Id == gameId && g.Mode == GameMode.Ai, ct);

    public Task<GameMove?> GetMoveByClientMoveIdAsync(Guid gameId, Guid clientMoveId, CancellationToken ct) =>
        db.GameMoves.AsNoTracking().SingleOrDefaultAsync(m => m.GameId == gameId && m.ClientMoveId == clientMoveId, ct);

    public async Task<IReadOnlyList<AiMoveInfo>?> GetMovesForHumanAsync(Guid gameId, Guid userId, CancellationToken ct)
    {
        var authorized = await db.Games.AsNoTracking().AnyAsync(g => g.Id == gameId && g.Mode == GameMode.Ai && g.HumanPlayerId == userId, ct);
        if (!authorized) return null;
        return await db.GameMoves.AsNoTracking().Where(m => m.GameId == gameId).OrderBy(m => m.Ply)
            .Select(m => new AiMoveInfo(m.Ply, m.FromSquare, m.ToSquare, m.Piece, m.CapturedPiece, m.IsAiMove, m.CreatedAt)).ToListAsync(ct);
    }

    public async Task CreateAsync(Game game, CancellationToken ct)
    {
        db.Games.Add(game);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AiPersistStatus> CommitHumanMoveAsync(Guid gameId, Guid userId, long expectedVersion, GameMove move, string fenAfter,
        Side nextTurn, PersistedStatus nextStatus, Side? winnerSide, ResultReason? resultReason, DateTime now, CancellationToken ct)
    {
        var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId && g.Mode == GameMode.Ai && g.HumanPlayerId == userId, ct);
        if (game is null) return AiPersistStatus.NotFound;
        if (game.Version != expectedVersion || game.Status != PersistedStatus.InProgress || game.CurrentTurn != game.HumanSide
            || game.AiTurnStatus != AiTurnStatus.WaitingForHuman) return AiPersistStatus.Conflict;
        ApplyMove(game, move, fenAfter, nextTurn, nextStatus, winnerSide, resultReason, now);
        game.AiTurnStatus = nextStatus == PersistedStatus.Finished ? AiTurnStatus.None : AiTurnStatus.Queued;
        game.AiTurnLeaseUntilUtc = null; game.AiRetryCount = 0; game.AiLastError = null;
        db.GameMoves.Add(move);
        try { await db.SaveChangesAsync(ct); return AiPersistStatus.Saved; }
        catch (DbUpdateConcurrencyException) { Detach(game, move); return AiPersistStatus.Conflict; }
        catch (DbUpdateException ex) when (IsDuplicate(ex)) { Detach(game, move); return AiPersistStatus.Duplicate; }
    }

    public async Task<AiPersistStatus> RestartAsync(Guid gameId, Guid userId, long expectedVersion, Game restarted, DateTime now, CancellationToken ct)
    {
        var current = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId && g.Mode == GameMode.Ai && g.HumanPlayerId == userId, ct);
        if (current is null) return AiPersistStatus.NotFound;
        if (current.Version != expectedVersion) return AiPersistStatus.Conflict;
        if (current.Status == PersistedStatus.InProgress)
        {
            current.Status = PersistedStatus.Abandoned; current.ResultReason = ResultReason.Abandoned; current.EndedAt = now;
        }
        current.AiTurnStatus = AiTurnStatus.None; current.AiTurnLeaseUntilUtc = null; current.Version++; current.UpdatedAt = now;
        db.Games.Add(restarted);
        try { await db.SaveChangesAsync(ct); return AiPersistStatus.Saved; }
        catch (DbUpdateConcurrencyException) { db.Entry(restarted).State = EntityState.Detached; return AiPersistStatus.Conflict; }
    }

    public async Task<AiPersistStatus> ResignAsync(Guid gameId, Guid userId, long expectedVersion, DateTime now, CancellationToken ct)
    {
        var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId && g.Mode == GameMode.Ai && g.HumanPlayerId == userId, ct);
        if (game is null) return AiPersistStatus.NotFound;
        if (game.Version != expectedVersion || game.Status != PersistedStatus.InProgress) return AiPersistStatus.Conflict;
        game.Status = PersistedStatus.Abandoned; game.WinnerSide = game.AiSide; game.WinnerId = null;
        game.ResultReason = ResultReason.Resignation; game.EndedAt = now; game.AiTurnStatus = AiTurnStatus.None;
        game.AiTurnLeaseUntilUtc = null; game.Version++; game.UpdatedAt = now;
        try { await db.SaveChangesAsync(ct); return AiPersistStatus.Saved; }
        catch (DbUpdateConcurrencyException) { return AiPersistStatus.Conflict; }
    }

    public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(DateTime now, int take, CancellationToken ct) =>
        await db.Games.AsNoTracking().Where(g => g.Mode == GameMode.Ai && g.Status == PersistedStatus.InProgress
            && g.CurrentTurn == g.AiSide && (g.AiTurnStatus == AiTurnStatus.Queued
                || (g.AiTurnStatus == AiTurnStatus.Thinking && g.AiTurnLeaseUntilUtc <= now)))
            .OrderBy(g => g.UpdatedAt).Select(g => g.Id).Take(take).ToListAsync(ct);

    public async Task<Game?> ClaimAsync(Guid gameId, DateTime now, DateTime leaseUntilUtc, CancellationToken ct)
    {
        var claimed = await db.Games.Where(g => g.Id == gameId && g.Mode == GameMode.Ai && g.Status == PersistedStatus.InProgress
            && g.CurrentTurn == g.AiSide && (g.AiTurnStatus == AiTurnStatus.Queued
                || (g.AiTurnStatus == AiTurnStatus.Thinking && g.AiTurnLeaseUntilUtc <= now)))
            .ExecuteUpdateAsync(update => update.SetProperty(g => g.AiTurnStatus, AiTurnStatus.Thinking)
                .SetProperty(g => g.AiTurnLeaseUntilUtc, leaseUntilUtc).SetProperty(g => g.AiLastError, (string?)null), ct);
        return claimed == 1 ? await GetAsync(gameId, ct) : null;
    }

    public async Task<AiCommitResult> CommitAiMoveAsync(Guid gameId, long expectedVersion, GameMove move, string fenAfter, Side nextTurn,
        PersistedStatus nextStatus, Side? winnerSide, ResultReason? resultReason, int completedDepth, long elapsedMs, long nodesSearched, DateTime now, CancellationToken ct)
    {
        var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId && g.Mode == GameMode.Ai, ct);
        if (game is null) return new(AiPersistStatus.NotFound);
        if (game.Version != expectedVersion || game.Status != PersistedStatus.InProgress || game.CurrentTurn != game.AiSide
            || game.AiTurnStatus != AiTurnStatus.Thinking) return new(AiPersistStatus.Conflict);
        ApplyMove(game, move, fenAfter, nextTurn, nextStatus, winnerSide, resultReason, now);
        game.AiTurnStatus = nextStatus == PersistedStatus.Finished ? AiTurnStatus.None : AiTurnStatus.WaitingForHuman;
        game.AiTurnLeaseUntilUtc = null; game.AiRetryCount = 0; game.AiLastError = null;
        game.AiCompletedDepth = completedDepth; game.AiElapsedMs = elapsedMs; game.AiNodesSearched = nodesSearched;
        db.GameMoves.Add(move);
        try { await db.SaveChangesAsync(ct); return new(AiPersistStatus.Saved, game); }
        catch (DbUpdateConcurrencyException) { Detach(game, move); return new(AiPersistStatus.Conflict); }
        catch (DbUpdateException ex) when (IsDuplicate(ex)) { Detach(game, move); return new(AiPersistStatus.Duplicate); }
    }

    public async Task<AiPersistStatus> FailOrRetryAsync(Guid gameId, long expectedVersion, string safeError, DateTime now, CancellationToken ct)
    {
        var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId && g.Mode == GameMode.Ai, ct);
        if (game is null) return AiPersistStatus.NotFound;
        if (game.Version != expectedVersion || game.Status != PersistedStatus.InProgress || game.AiTurnStatus != AiTurnStatus.Thinking) return AiPersistStatus.Conflict;
        game.AiLastError = safeError; game.AiTurnLeaseUntilUtc = null; game.UpdatedAt = now; game.AiRetryCount++;
        game.AiTurnStatus = game.AiRetryCount <= 2 ? AiTurnStatus.Queued : AiTurnStatus.Failed;
        try { await db.SaveChangesAsync(ct); return AiPersistStatus.Saved; }
        catch (DbUpdateConcurrencyException) { return AiPersistStatus.Conflict; }
    }

    private static void ApplyMove(Game game, GameMove move, string fenAfter, Side nextTurn, PersistedStatus status,
        Side? winnerSide, ResultReason? reason, DateTime now)
    {
        game.CurrentFen = fenAfter; game.CurrentTurn = nextTurn; game.Status = status; game.WinnerSide = winnerSide;
        game.WinnerId = winnerSide is { } side && side == game.HumanSide ? game.HumanPlayerId : null;
        game.ResultReason = reason; game.EndedAt = status == PersistedStatus.Finished ? now : null;
        game.Version++; game.UpdatedAt = now; move.FenAfter = fenAfter;
    }

    private void Detach(Game game, GameMove move) { db.Entry(game).State = EntityState.Detached; db.Entry(move).State = EntityState.Detached; }
    private static bool IsDuplicate(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 }
        || ex.InnerException?.InnerException is SqlException { Number: 2601 or 2627 };
}
