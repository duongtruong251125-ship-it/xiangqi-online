using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.GameEngine;
using XiangqiGameResult = XiangqiOnline.GameEngine.GameResult;
using PersistedGameStatus = XiangqiOnline.Domain.Enums.GameStatus;

namespace XiangqiOnline.Application.Games;

public sealed record OnlineGameInfo(Guid Id, Guid? RoomId, Guid? RedPlayerId, Guid? BlackPlayerId, GameMode Mode, PersistedGameStatus Status, string CurrentFen, Side CurrentTurn, Guid? WinnerId, ResultReason? ResultReason, long Version, int Ply, long RedRemainingMs, long BlackRemainingMs, DateTime? TurnStartedAtUtc, long TimeControlInitialMs, long TimeControlIncrementMs, DateTime? EndedAtUtc);
public sealed record GameMoveHistoryInfo(int Ply, string FromSquare, string ToSquare, string Piece, string? CapturedPiece, DateTime CreatedAt);
public sealed record GameSyncInfo(OnlineGameInfo Game, string RedPlayerName, string BlackPlayerName, bool IsInCheck, IReadOnlyList<GameMoveHistoryInfo> Moves, DateTime ServerTimeUtc);
public sealed record GameMoveIdempotencyInfo(Guid? PlayerId, string FromSquare, string ToSquare, int Ply);
public sealed record GameClockCandidate(Guid Id, long Version, Side CurrentTurn, long RedRemainingMs, long BlackRemainingMs, DateTime? TurnStartedAtUtc, Guid? RedPlayerId, Guid? BlackPlayerId);
public sealed record MoveAppliedInfo(Guid GameId, Guid ClientMoveId, int Ply, string FromSquare, string ToSquare, string? CapturedPiece, string FenAfter, Side NextTurn, long Version, long RedRemainingMs, long BlackRemainingMs, DateTime? TurnStartedAtUtc, PersistedGameStatus Status, Guid? WinnerId, ResultReason? ResultReason, bool IsInCheck, DateTime? EndedAtUtc, DateTime ServerTimeUtc);
public sealed record GameFinishedInfo(Guid GameId, Guid? WinnerId, ResultReason ResultReason, string FinalFen, long FinalVersion, DateTime EndedAtUtc, long RedRemainingMs, long BlackRemainingMs, DateTime? TurnStartedAtUtc, DateTime ServerTimeUtc);
public enum ApplyMoveStatus { Applied, IdempotentRetry, NotFound, WrongTurn, Finished, InvalidMove, VersionConflict, DuplicateMove, IdempotencyConflict, TimedOut }
public sealed record ApplyMoveResult(ApplyMoveStatus Status, OnlineGameInfo? Game = null, MoveAppliedInfo? Move = null, GameFinishedInfo? Finished = null);
public sealed record LegalMovesResult(ApplyMoveStatus Status, IReadOnlyList<Move> Moves);
public enum PersistMoveStatus { Saved, NotFound, VersionConflict, DuplicateMove }
public enum PersistFinishStatus { Saved, NotFound, VersionConflict, Finished }
public sealed record PersistFinishResult(PersistFinishStatus Status, OnlineGameInfo? Game = null);
public enum RestartGameStatus { Restarted, NotFound, Forbidden, VersionConflict }
public sealed record RestartGameResult(RestartGameStatus Status, OnlineGameInfo? Game = null);

public interface IGameplayRepository
{
    Task<OnlineGameInfo?> GetForPlayerAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<GameSyncInfo?> GetSyncForPlayerAsync(Guid gameId, Guid userId, DateTime serverTimeUtc, CancellationToken cancellationToken);
    Task<OnlineGameInfo> CreateLocalAsync(Guid ownerId, string initialFen, DateTime now, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameMoveHistoryInfo>?> GetMoveHistoryAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<RestartGameResult> RestartLocalAsync(Guid gameId, Guid ownerId, long expectedVersion, string initialFen, DateTime now, CancellationToken cancellationToken);
    Task<GameMoveIdempotencyInfo?> GetMoveByClientMoveIdAsync(Guid gameId, Guid clientMoveId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameClockCandidate>> GetInProgressOnlineGamesAsync(CancellationToken cancellationToken);
    Task<PersistMoveStatus> SaveMoveAsync(Guid gameId, Guid userId, long expectedVersion, GameMove move, string fenBefore, string fenAfter, Side currentTurn, PersistedGameStatus status, Guid? winnerId, ResultReason? resultReason, DateTime? endedAt, long redRemainingMs, long blackRemainingMs, DateTime? turnStartedAtUtc, DateTime now, CancellationToken cancellationToken);
    Task<PersistFinishResult> FinishGameAsync(Guid gameId, long expectedVersion, Guid? winnerId, ResultReason reason, long redRemainingMs, long blackRemainingMs, DateTime? turnStartedAtUtc, DateTime now, CancellationToken cancellationToken);
}

public interface IGameplayService
{
    Task<OnlineGameInfo?> GetAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<GameSyncInfo?> GetSyncAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<OnlineGameInfo> CreateLocalAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameMoveHistoryInfo>?> GetMoveHistoryAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<RestartGameResult> RestartLocalAsync(Guid gameId, Guid userId, long expectedVersion, CancellationToken cancellationToken);
    Task<LegalMovesResult> GetLegalMovesAsync(Guid gameId, Guid userId, int fromFile, int fromRank, CancellationToken cancellationToken);
    Task<ApplyMoveResult> ApplyMoveAsync(Guid gameId, Guid userId, int fromFile, int fromRank, int toFile, int toRank, long expectedVersion, Guid clientMoveId, CancellationToken cancellationToken);
    Task<GameFinishedInfo?> ResignAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameFinishedInfo>> ProcessTimeoutsAsync(CancellationToken cancellationToken);
}

public sealed class GameplayService(IGameplayRepository repository, IXiangqiRules rules, TimeProvider timeProvider) : IGameplayService
{
    public async Task<OnlineGameInfo?> GetAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var game = await repository.GetForPlayerAsync(gameId, userId, cancellationToken);
        return game is null ? null : WithCurrentClock(game, UtcNow());
    }

    public async Task<GameSyncInfo?> GetSyncAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var state = await repository.GetSyncForPlayerAsync(gameId, userId, now, cancellationToken);
        if (state is null) return null;
        var board = BoardState.FromFen(state.Game.CurrentFen);
        return state with { Game = WithCurrentClock(state.Game, now), IsInCheck = rules.IsInCheck(board, board.SideToMove), ServerTimeUtc = now };
    }

    public Task<OnlineGameInfo> CreateLocalAsync(Guid userId, CancellationToken cancellationToken) =>
        repository.CreateLocalAsync(userId, BoardState.CreateInitialBoard().ToFen(), UtcNow(), cancellationToken);

    public Task<IReadOnlyList<GameMoveHistoryInfo>?> GetMoveHistoryAsync(Guid gameId, Guid userId, CancellationToken cancellationToken) =>
        repository.GetMoveHistoryAsync(gameId, userId, cancellationToken);

    public async Task<RestartGameResult> RestartLocalAsync(Guid gameId, Guid userId, long expectedVersion, CancellationToken cancellationToken)
    {
        var current = await repository.GetForPlayerAsync(gameId, userId, cancellationToken);
        if (current is null) return new(RestartGameStatus.NotFound);
        if (current.Mode != GameMode.Local || current.RedPlayerId != userId) return new(RestartGameStatus.Forbidden);
        if (current.Version != expectedVersion) return new(RestartGameStatus.VersionConflict);
        return await repository.RestartLocalAsync(gameId, userId, expectedVersion, BoardState.CreateInitialBoard().ToFen(), UtcNow(), cancellationToken);
    }

    public async Task<LegalMovesResult> GetLegalMovesAsync(Guid gameId, Guid userId, int fromFile, int fromRank, CancellationToken cancellationToken)
    {
        var game = await repository.GetForPlayerAsync(gameId, userId, cancellationToken);
        if (game is null) return new(ApplyMoveStatus.NotFound, []);
        if (game.Status != PersistedGameStatus.InProgress) return new(ApplyMoveStatus.Finished, []);
        var side = PlayerSide(game, userId);
        if (side is null) return new(ApplyMoveStatus.NotFound, []);
        if (game.Mode != GameMode.Local && game.CurrentTurn != side) return new(ApplyMoveStatus.WrongTurn, []);
        var from = new Position(fromFile, fromRank);
        if (!from.IsValid) return new(ApplyMoveStatus.InvalidMove, []);
        try
        {
            var board = BoardState.FromFen(game.CurrentFen);
            var piece = board.GetPieceAt(from);
            if (piece is null || piece.Color != ToPieceColor(game.CurrentTurn)) return new(ApplyMoveStatus.InvalidMove, []);
            return new(ApplyMoveStatus.Applied, rules.GenerateLegalMoves(board, from));
        }
        catch (FormatException) { return new(ApplyMoveStatus.InvalidMove, []); }
    }

    public async Task<ApplyMoveResult> ApplyMoveAsync(Guid gameId, Guid userId, int fromFile, int fromRank, int toFile, int toRank, long expectedVersion, Guid clientMoveId, CancellationToken cancellationToken)
    {
        if (clientMoveId == Guid.Empty || expectedVersion < 0) return new(ApplyMoveStatus.InvalidMove);
        var game = await repository.GetForPlayerAsync(gameId, userId, cancellationToken);
        if (game is null) return new(ApplyMoveStatus.NotFound);

        var from = new Position(fromFile, fromRank);
        var to = new Position(toFile, toRank);
        if (!from.IsValid || !to.IsValid) return new(ApplyMoveStatus.InvalidMove, game);
        var fromSquare = Square(from);
        var toSquare = Square(to);
        var prior = await repository.GetMoveByClientMoveIdAsync(gameId, clientMoveId, cancellationToken);
        if (prior is not null)
        {
            var matches = prior.PlayerId == userId && prior.FromSquare == fromSquare && prior.ToSquare == toSquare && prior.Ply == expectedVersion + 1;
            return matches
                ? new(ApplyMoveStatus.IdempotentRetry, WithCurrentClock(game, UtcNow()))
                : new(ApplyMoveStatus.IdempotencyConflict, WithCurrentClock(game, UtcNow()));
        }

        if (game.Status != PersistedGameStatus.InProgress) return new(ApplyMoveStatus.Finished, WithCurrentClock(game, UtcNow()));
        if (game.Version != expectedVersion) return new(ApplyMoveStatus.VersionConflict, WithCurrentClock(game, UtcNow()));
        var side = game.Mode == GameMode.Local ? game.CurrentTurn : PlayerSide(game, userId);
        if (side is null) return new(ApplyMoveStatus.NotFound);
        if (game.CurrentTurn != side) return new(ApplyMoveStatus.WrongTurn, WithCurrentClock(game, UtcNow()));

        var now = UtcNow();
        var clock = ClockAt(game, now);
        var remainingForMover = side == Side.Red ? clock.RedRemainingMs : clock.BlackRemainingMs;
        if (game.Mode == GameMode.Online && remainingForMover <= 0)
        {
            var timeoutFinish = await FinishPersistedAsync(game, side == Side.Red ? game.BlackPlayerId : game.RedPlayerId,
                ResultReason.Timeout, clock.RedRemainingMs, clock.BlackRemainingMs, null, now, cancellationToken);
            return timeoutFinish is null
                ? new(ApplyMoveStatus.VersionConflict, await GetAfterConflictAsync(gameId, userId, cancellationToken))
                : new(ApplyMoveStatus.TimedOut, timeoutFinish.Value.Game, Finished: timeoutFinish.Value.Event);
        }

        BoardState board;
        try { board = BoardState.FromFen(game.CurrentFen); }
        catch (FormatException) { return new(ApplyMoveStatus.InvalidMove, game); }
        var piece = board.GetPieceAt(from);
        if (piece is null || piece.Color != ToPieceColor(side.Value)) return new(ApplyMoveStatus.InvalidMove, game);
        var captured = board.GetPieceAt(to);
        var moveResult = rules.TryApplyMove(board, new Move(from, to));
        if (!moveResult.Success || moveResult.Board is null) return new(ApplyMoveStatus.InvalidMove, game);

        var next = moveResult.Board;
        var result = moveResult.GameResult;
        var nextStatus = result is null ? PersistedGameStatus.InProgress : PersistedGameStatus.Finished;
        var winner = result switch
        {
            XiangqiGameResult.RedWins => game.Mode == GameMode.Local ? null : game.RedPlayerId,
            XiangqiGameResult.BlackWins => game.Mode == GameMode.Local ? null : game.BlackPlayerId,
            _ => null
        };
        ResultReason? resultReason = result is null ? null : result == XiangqiGameResult.Draw ? ResultReason.Draw : rules.IsInCheck(next, next.SideToMove) ? ResultReason.Checkmate : ResultReason.Stalemate;
        DateTime? endedAt = nextStatus == PersistedGameStatus.Finished ? now : null;
        var redRemaining = clock.RedRemainingMs;
        var blackRemaining = clock.BlackRemainingMs;
        if (game.Mode == GameMode.Online)
        {
            if (side == Side.Red) redRemaining += game.TimeControlIncrementMs;
            else blackRemaining += game.TimeControlIncrementMs;
        }
        var nextTurn = next.SideToMove == PieceColor.Red ? Side.Red : Side.Black;
        DateTime? nextTurnStartedAt = game.Mode == GameMode.Online && nextStatus == PersistedGameStatus.InProgress ? now : null;
        var move = new GameMove
        {
            GameId = game.Id,
            Ply = game.Ply + 1,
            FromSquare = fromSquare,
            ToSquare = toSquare,
            Piece = PieceCode(piece),
            CapturedPiece = captured is null ? null : PieceCode(captured),
            PlayerId = userId,
            FenBefore = game.CurrentFen,
            FenAfter = next.ToFen(),
            ClockAfterMove = side == Side.Red ? redRemaining : blackRemaining,
            ClientMoveId = clientMoveId,
            CreatedAt = now
        };
        var persisted = await repository.SaveMoveAsync(gameId, userId, expectedVersion, move, game.CurrentFen, next.ToFen(), nextTurn,
            nextStatus, winner, resultReason, endedAt, redRemaining, blackRemaining, nextTurnStartedAt, now, cancellationToken);
        if (persisted == PersistMoveStatus.DuplicateMove)
        {
            var existing = await repository.GetMoveByClientMoveIdAsync(gameId, clientMoveId, cancellationToken);
            var current = await GetAfterConflictAsync(gameId, userId, cancellationToken);
            if (existing is not null && existing.PlayerId == userId && existing.FromSquare == fromSquare && existing.ToSquare == toSquare && existing.Ply == expectedVersion + 1)
                return new(ApplyMoveStatus.IdempotentRetry, current);
            return new(ApplyMoveStatus.IdempotencyConflict, current);
        }
        if (persisted != PersistMoveStatus.Saved)
            return new(persisted == PersistMoveStatus.VersionConflict ? ApplyMoveStatus.VersionConflict : ApplyMoveStatus.NotFound,
                persisted == PersistMoveStatus.VersionConflict ? await GetAfterConflictAsync(gameId, userId, cancellationToken) : null);

        var saved = game with
        {
            CurrentFen = next.ToFen(), CurrentTurn = nextTurn, Status = nextStatus, WinnerId = winner, ResultReason = resultReason,
            Version = game.Version + 1, Ply = game.Ply + 1, RedRemainingMs = redRemaining, BlackRemainingMs = blackRemaining,
            TurnStartedAtUtc = nextTurnStartedAt, EndedAtUtc = endedAt
        };
        var applied = new MoveAppliedInfo(gameId, clientMoveId, saved.Ply, fromSquare, toSquare, captured is null ? null : PieceCode(captured),
            saved.CurrentFen, saved.CurrentTurn, saved.Version, saved.RedRemainingMs, saved.BlackRemainingMs, saved.TurnStartedAtUtc,
            saved.Status, saved.WinnerId, saved.ResultReason, rules.IsInCheck(next, next.SideToMove), saved.EndedAtUtc, now);
        var finished = nextStatus == PersistedGameStatus.Finished
            ? new GameFinishedInfo(gameId, winner, resultReason!.Value, saved.CurrentFen, saved.Version, endedAt!.Value,
                saved.RedRemainingMs, saved.BlackRemainingMs, saved.TurnStartedAtUtc, now)
            : null;
        return new(ApplyMoveStatus.Applied, saved, applied, finished);
    }

    public async Task<GameFinishedInfo?> ResignAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var game = await repository.GetForPlayerAsync(gameId, userId, cancellationToken);
        if (game is null || game.Mode != GameMode.Online || game.Status != PersistedGameStatus.InProgress) return null;
        var side = PlayerSide(game, userId);
        if (side is null) return null;
        var now = UtcNow();
        var clock = ClockAt(game, now);
        if ((side == Side.Red ? clock.RedRemainingMs : clock.BlackRemainingMs) <= 0)
            return (await FinishPersistedAsync(game, side == Side.Red ? game.BlackPlayerId : game.RedPlayerId, ResultReason.Timeout,
                clock.RedRemainingMs, clock.BlackRemainingMs, null, now, cancellationToken))?.Event;
        return (await FinishPersistedAsync(game, side == Side.Red ? game.BlackPlayerId : game.RedPlayerId, ResultReason.Resignation,
            clock.RedRemainingMs, clock.BlackRemainingMs, game.TurnStartedAtUtc, now, cancellationToken))?.Event;
    }

    public async Task<IReadOnlyList<GameFinishedInfo>> ProcessTimeoutsAsync(CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var finished = new List<GameFinishedInfo>();
        foreach (var game in await repository.GetInProgressOnlineGamesAsync(cancellationToken))
        {
            var clock = ClockAt(game, now);
            if ((game.CurrentTurn == Side.Red ? clock.RedRemainingMs : clock.BlackRemainingMs) > 0) continue;
            var winner = game.CurrentTurn == Side.Red ? game.BlackPlayerId : game.RedPlayerId;
            var result = await repository.FinishGameAsync(game.Id, game.Version, winner, ResultReason.Timeout,
                clock.RedRemainingMs, clock.BlackRemainingMs, null, now, cancellationToken);
            if (result.Status == PersistFinishStatus.Saved && result.Game is not null)
                finished.Add(ToFinished(result.Game, now));
        }
        return finished;
    }

    private async Task<(OnlineGameInfo Game, GameFinishedInfo Event)?> FinishPersistedAsync(OnlineGameInfo game, Guid? winnerId,
        ResultReason reason, long redMs, long blackMs, DateTime? turnStarted, DateTime now, CancellationToken cancellationToken)
    {
        var result = await repository.FinishGameAsync(game.Id, game.Version, winnerId, reason, redMs, blackMs, turnStarted, now, cancellationToken);
        if (result.Status != PersistFinishStatus.Saved || result.Game is null) return null;
        return (result.Game, ToFinished(result.Game, now));
    }

    private async Task<OnlineGameInfo?> GetAfterConflictAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var latest = await repository.GetForPlayerAsync(gameId, userId, cancellationToken);
        return latest is null ? null : WithCurrentClock(latest, UtcNow());
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static OnlineGameInfo WithCurrentClock(OnlineGameInfo game, DateTime now)
    {
        var clock = ClockAt(game, now);
        return game with { RedRemainingMs = clock.RedRemainingMs, BlackRemainingMs = clock.BlackRemainingMs };
    }

    private static (long RedRemainingMs, long BlackRemainingMs) ClockAt(OnlineGameInfo game, DateTime now)
    {
        if (game.Mode != GameMode.Online || game.Status != PersistedGameStatus.InProgress || game.TurnStartedAtUtc is null)
            return (game.RedRemainingMs, game.BlackRemainingMs);
        var elapsed = Math.Max(0L, (long)(now - DateTime.SpecifyKind(game.TurnStartedAtUtc.Value, DateTimeKind.Utc)).TotalMilliseconds);
        return game.CurrentTurn == Side.Red
            ? (Math.Max(0, game.RedRemainingMs - elapsed), game.BlackRemainingMs)
            : (game.RedRemainingMs, Math.Max(0, game.BlackRemainingMs - elapsed));
    }

    private static (long RedRemainingMs, long BlackRemainingMs) ClockAt(GameClockCandidate game, DateTime now)
    {
        if (game.TurnStartedAtUtc is null) return (game.RedRemainingMs, game.BlackRemainingMs);
        var elapsed = Math.Max(0L, (long)(now - DateTime.SpecifyKind(game.TurnStartedAtUtc.Value, DateTimeKind.Utc)).TotalMilliseconds);
        return game.CurrentTurn == Side.Red
            ? (Math.Max(0, game.RedRemainingMs - elapsed), game.BlackRemainingMs)
            : (game.RedRemainingMs, Math.Max(0, game.BlackRemainingMs - elapsed));
    }

    private static GameFinishedInfo ToFinished(OnlineGameInfo game, DateTime now) => new(game.Id, game.WinnerId,
        game.ResultReason ?? ResultReason.Abandoned, game.CurrentFen, game.Version, game.EndedAtUtc ?? now,
        game.RedRemainingMs, game.BlackRemainingMs, game.TurnStartedAtUtc, now);

    private static Side? PlayerSide(OnlineGameInfo game, Guid userId) =>
        game.RedPlayerId == userId ? Side.Red : game.BlackPlayerId == userId ? Side.Black : null;

    private static PieceColor ToPieceColor(Side side) => side == Side.Red ? PieceColor.Red : PieceColor.Black;
    private static string Square(Position position) => $"{(char)('a' + position.File)}{position.Rank}";
    private static string PieceCode(Piece piece) => $"{piece.Color}:{piece.Type}";
}
