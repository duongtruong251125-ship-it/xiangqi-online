using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using XiangqiOnline.Application.Games;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.Infrastructure.Games;

public sealed class EfGameplayRepository(AppDbContext db) : IGameplayRepository
{
    public async Task<OnlineGameInfo?> GetForPlayerAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var game = await db.Games.SingleOrDefaultAsync(item => item.Id == gameId && (item.RedPlayerId == userId || item.BlackPlayerId == userId), cancellationToken);
        if (game is null) return null;
        var ply = await db.GameMoves.CountAsync(move => move.GameId == gameId, cancellationToken);
        return ToInfo(game, ply);
    }

    public async Task<GameSyncInfo?> GetSyncForPlayerAsync(Guid gameId, Guid userId, DateTime serverTimeUtc, CancellationToken cancellationToken)
    {
        var game = await db.Games.AsNoTracking().SingleOrDefaultAsync(item => item.Id == gameId && (item.RedPlayerId == userId || item.BlackPlayerId == userId), cancellationToken);
        if (game is null) return null;
        var redName = game.RedPlayerId.HasValue
            ? await db.Users.Where(user => user.Id == game.RedPlayerId).Select(user => user.DisplayName).SingleOrDefaultAsync(cancellationToken) ?? "Đỏ"
            : "Đỏ";
        var blackName = game.BlackPlayerId.HasValue
            ? await db.Users.Where(user => user.Id == game.BlackPlayerId).Select(user => user.DisplayName).SingleOrDefaultAsync(cancellationToken) ?? "Đen"
            : "Đen";
        var moves = await db.GameMoves.AsNoTracking().Where(move => move.GameId == gameId).OrderBy(move => move.Ply)
            .Select(move => new GameMoveHistoryInfo(move.Ply, move.FromSquare, move.ToSquare, move.Piece, move.CapturedPiece, move.CreatedAt)).ToListAsync(cancellationToken);
        return new(ToInfo(game, moves.Count), redName, blackName, false, moves, serverTimeUtc);
    }

    public async Task<OnlineGameInfo> CreateLocalAsync(Guid ownerId, string initialFen, DateTime now, CancellationToken cancellationToken)
    {
        var game = new Game
        {
            RedPlayerId = ownerId, Mode = GameMode.Local, Status = GameStatus.InProgress, CurrentFen = initialFen,
            CurrentTurn = Side.Red, StartedAt = now, CreatedAt = now, UpdatedAt = now
        };
        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(game, 0);
    }

    public async Task<IReadOnlyList<GameMoveHistoryInfo>?> GetMoveHistoryAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var allowed = await db.Games.AnyAsync(game => game.Id == gameId && (game.RedPlayerId == userId || game.BlackPlayerId == userId), cancellationToken);
        if (!allowed) return null;
        return await db.GameMoves.AsNoTracking().Where(move => move.GameId == gameId).OrderBy(move => move.Ply)
            .Select(move => new GameMoveHistoryInfo(move.Ply, move.FromSquare, move.ToSquare, move.Piece, move.CapturedPiece, move.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<RestartGameResult> RestartLocalAsync(Guid gameId, Guid ownerId, long expectedVersion, string initialFen, DateTime now, CancellationToken cancellationToken)
    {
        var current = await db.Games.SingleOrDefaultAsync(game => game.Id == gameId && game.RedPlayerId == ownerId && game.Mode == GameMode.Local, cancellationToken);
        if (current is null) return new(RestartGameStatus.NotFound);
        if (current.Version != expectedVersion) return new(RestartGameStatus.VersionConflict);

        if (current.Status == GameStatus.InProgress)
        {
            current.Status = GameStatus.Abandoned;
            current.ResultReason = ResultReason.Abandoned;
            current.EndedAt = now;
        }
        current.Version++;
        current.UpdatedAt = now;
        var restarted = new Game
        {
            RedPlayerId = ownerId, Mode = GameMode.Local, Status = GameStatus.InProgress, CurrentFen = initialFen,
            CurrentTurn = Side.Red, StartedAt = now, CreatedAt = now, UpdatedAt = now
        };
        db.Games.Add(restarted);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(RestartGameStatus.Restarted, ToInfo(restarted, 0));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(restarted).State = EntityState.Detached;
            return new(RestartGameStatus.VersionConflict);
        }
    }

    public async Task<GameMoveIdempotencyInfo?> GetMoveByClientMoveIdAsync(Guid gameId, Guid clientMoveId, CancellationToken cancellationToken)
    {
        return await db.GameMoves.AsNoTracking().Where(move => move.GameId == gameId && move.ClientMoveId == clientMoveId)
            .Select(move => new GameMoveIdempotencyInfo(move.PlayerId, move.FromSquare, move.ToSquare, move.Ply))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameClockCandidate>> GetInProgressOnlineGamesAsync(CancellationToken cancellationToken) =>
        await db.Games.AsNoTracking().Where(game => game.Mode == GameMode.Online && game.Status == GameStatus.InProgress && game.TurnStartedAtUtc != null)
            .Select(game => new GameClockCandidate(game.Id, game.Version, game.CurrentTurn, game.RedRemainingMs, game.BlackRemainingMs,
                game.TurnStartedAtUtc, game.RedPlayerId, game.BlackPlayerId))
            .ToListAsync(cancellationToken);

    public async Task<PersistMoveStatus> SaveMoveAsync(Guid gameId, Guid userId, long expectedVersion, GameMove move, string fenBefore, string fenAfter,
        Side currentTurn, GameStatus status, Guid? winnerId, ResultReason? resultReason, DateTime? endedAt, long redRemainingMs,
        long blackRemainingMs, DateTime? turnStartedAtUtc, DateTime now, CancellationToken cancellationToken)
    {
        var game = await db.Games.SingleOrDefaultAsync(item => item.Id == gameId && (item.RedPlayerId == userId || item.BlackPlayerId == userId), cancellationToken);
        if (game is null) return PersistMoveStatus.NotFound;
        if (game.Version != expectedVersion) return PersistMoveStatus.VersionConflict;
        if (game.Status != GameStatus.InProgress) return PersistMoveStatus.VersionConflict;

        move.FenBefore = fenBefore;
        move.FenAfter = fenAfter;
        game.CurrentFen = fenAfter;
        game.CurrentTurn = currentTurn;
        game.Status = status;
        game.WinnerId = winnerId;
        game.ResultReason = resultReason;
        game.EndedAt = endedAt;
        game.RedRemainingMs = redRemainingMs;
        game.BlackRemainingMs = blackRemainingMs;
        game.TurnStartedAtUtc = turnStartedAtUtc;
        game.Version++;
        game.UpdatedAt = now;
        db.GameMoves.Add(move);
        db.GameEvents.Add(NewEvent(gameId, GameEventType.MoveApplied, new { move.Ply, move.ClientMoveId, move.PlayerId, move.FromSquare, move.ToSquare, fenAfter, game.Version, redRemainingMs, blackRemainingMs }, now));
        if (status == GameStatus.Finished || move.Ply % 10 == 0)
            await SaveSnapshotAsync(game, move.Ply, now, cancellationToken);
        if (status == GameStatus.Finished)
            db.GameEvents.Add(NewEvent(gameId, GameEventType.Finished, new { winnerId, resultReason, fenAfter, game.Version }, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return PersistMoveStatus.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            DetachPending(move, gameId);
            return PersistMoveStatus.VersionConflict;
        }
        catch (DbUpdateException exception) when (FindSqlException(exception) is { Number: 2601 or 2627 })
        {
            DetachPending(move, gameId);
            return PersistMoveStatus.DuplicateMove;
        }
    }

    public async Task<PersistFinishResult> FinishGameAsync(Guid gameId, long expectedVersion, Guid? winnerId, ResultReason reason,
        long redRemainingMs, long blackRemainingMs, DateTime? turnStartedAtUtc, DateTime now, CancellationToken cancellationToken)
    {
        var game = await db.Games.SingleOrDefaultAsync(item => item.Id == gameId && item.Mode == GameMode.Online, cancellationToken);
        if (game is null) return new(PersistFinishStatus.NotFound);
        if (game.Status != GameStatus.InProgress) return new(PersistFinishStatus.Finished, ToInfo(game, await PlyAsync(gameId, cancellationToken)));
        if (game.Version != expectedVersion) return new(PersistFinishStatus.VersionConflict);

        game.Status = GameStatus.Finished;
        game.WinnerId = winnerId;
        game.ResultReason = reason;
        game.RedRemainingMs = redRemainingMs;
        game.BlackRemainingMs = blackRemainingMs;
        game.TurnStartedAtUtc = null;
        game.EndedAt = now;
        game.Version++;
        game.UpdatedAt = now;
        if (reason == ResultReason.Resignation)
            db.GameEvents.Add(NewEvent(gameId, GameEventType.Resigned, new { winnerId, reason, game.Version }, now));
        db.GameEvents.Add(NewEvent(gameId, GameEventType.Finished, new { winnerId, reason, game.Version }, now));
        await SaveSnapshotAsync(game, await PlyAsync(gameId, cancellationToken), now, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(PersistFinishStatus.Saved, ToInfo(game, await PlyAsync(gameId, cancellationToken)));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(game).State = EntityState.Detached;
            DetachGameEvents(gameId);
            foreach (var snapshot in db.ChangeTracker.Entries<GameSnapshot>().Where(entry => entry.Entity.GameId == gameId && entry.State == EntityState.Added).ToArray())
                snapshot.State = EntityState.Detached;
            return new(PersistFinishStatus.VersionConflict);
        }
    }

    private Task<int> PlyAsync(Guid gameId, CancellationToken cancellationToken) =>
        db.GameMoves.CountAsync(move => move.GameId == gameId, cancellationToken);

    private async Task SaveSnapshotAsync(Game game, int ply, DateTime now, CancellationToken cancellationToken)
    {
        var snapshot = await db.GameSnapshots.SingleOrDefaultAsync(item => item.GameId == game.Id && item.Ply == ply, cancellationToken);
        if (snapshot is null)
        {
            snapshot = new GameSnapshot { GameId = game.Id, Ply = ply };
            db.GameSnapshots.Add(snapshot);
        }
        snapshot.Fen = game.CurrentFen;
        snapshot.CurrentTurn = game.CurrentTurn;
        snapshot.RedRemainingMs = game.RedRemainingMs;
        snapshot.BlackRemainingMs = game.BlackRemainingMs;
        snapshot.Version = game.Version;
        snapshot.Metadata = JsonSerializer.Serialize(new { game.Status, game.WinnerId, game.ResultReason });
        snapshot.CreatedAt = now;
    }

    private void DetachPending(GameMove move, Guid gameId)
    {
        db.Entry(move).State = EntityState.Detached;
        DetachGameEvents(gameId);
        foreach (var snapshot in db.ChangeTracker.Entries<GameSnapshot>().Where(entry => entry.Entity.GameId == gameId && entry.State == EntityState.Added).ToArray())
            snapshot.State = EntityState.Detached;
        var game = db.ChangeTracker.Entries<Game>().SingleOrDefault(entry => entry.Entity.Id == gameId);
        if (game is not null) game.State = EntityState.Detached;
    }

    private void DetachGameEvents(Guid gameId)
    {
        foreach (var entry in db.ChangeTracker.Entries<GameEvent>().Where(entry => entry.Entity.GameId == gameId && entry.State == EntityState.Added).ToArray())
            entry.State = EntityState.Detached;
    }

    private static GameEvent NewEvent(Guid gameId, GameEventType type, object payload, DateTime now) =>
        new() { GameId = gameId, EventType = type, Payload = JsonSerializer.Serialize(payload), CreatedAt = now };

    private static GameSnapshot ToSnapshot(Game game, int ply, DateTime now) => new()
    {
        GameId = game.Id, Ply = ply, Fen = game.CurrentFen, CurrentTurn = game.CurrentTurn,
        RedRemainingMs = game.RedRemainingMs, BlackRemainingMs = game.BlackRemainingMs, Version = game.Version,
        Metadata = JsonSerializer.Serialize(new { game.Status, game.WinnerId, game.ResultReason }), CreatedAt = now
    };

    private static OnlineGameInfo ToInfo(Game game, int ply) => new(game.Id, game.RoomId, game.RedPlayerId, game.BlackPlayerId, game.Mode,
        game.Status, game.CurrentFen, game.CurrentTurn, game.WinnerId, game.ResultReason, game.Version, ply,
        game.RedRemainingMs, game.BlackRemainingMs, game.TurnStartedAtUtc, game.TimeControlInitialMs, game.TimeControlIncrementMs, game.EndedAt);

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is SqlException sql) return sql;
        return null;
    }
}
