using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.GameEngine;
using EngineMove = XiangqiOnline.GameEngine.Move;
using EngineResult = XiangqiOnline.GameEngine.GameResult;
using PersistedStatus = XiangqiOnline.Domain.Enums.GameStatus;

namespace XiangqiOnline.Application.AiGames;

public sealed record AiGameInfo(Guid GameId, Guid HumanPlayerId, Side HumanSide, Side AiSide, AiDifficulty Difficulty,
    string Fen, Side CurrentTurn, PersistedStatus Status, AiTurnStatus AiTurnStatus, long Version,
    Side? WinnerSide, ResultReason? ResultReason, DateTime? EndedAtUtc, string? Error, int CompletedDepth, long ElapsedMs, long NodesSearched, bool IsCurrentTurnInCheck);
public sealed record AiMoveInfo(int Ply, string FromSquare, string ToSquare, string Piece, string? CapturedPiece,
    bool IsAiMove, DateTime CreatedAtUtc);
public sealed record AiGameHistoryInfo(AiGameInfo Game, IReadOnlyList<AiMoveInfo> Moves);
public enum AiGameOperationStatus { Ok, NotFound, Forbidden, InvalidDifficulty, InvalidSide, InvalidMove, WrongTurn, Finished, VersionConflict, IdempotencyConflict, SearchConflict }
public sealed record AiGameOperationResult(AiGameOperationStatus Status, AiGameInfo? Game = null, Guid? MoveId = null, bool Duplicate = false);
public sealed record AiAnalysisInfo(Guid GameId, AiTurnStatus Status, long CompletedDepth, long ElapsedMs, long NodesSearched, long Version);
public enum AiPersistStatus { Saved, NotFound, Conflict, Duplicate }
public sealed record AiCommitResult(AiPersistStatus Status, Game? Game = null);

public interface IAiGameRepository
{
    Task<Game?> GetForHumanAsync(Guid gameId, Guid userId, CancellationToken ct);
    Task<Game?> GetAsync(Guid gameId, CancellationToken ct);
    Task<GameMove?> GetMoveByClientMoveIdAsync(Guid gameId, Guid clientMoveId, CancellationToken ct);
    Task<IReadOnlyList<AiMoveInfo>?> GetMovesForHumanAsync(Guid gameId, Guid userId, CancellationToken ct);
    Task CreateAsync(Game game, CancellationToken ct);
    Task<AiPersistStatus> CommitHumanMoveAsync(Guid gameId, Guid userId, long expectedVersion, GameMove move, string fenAfter,
        Side nextTurn, PersistedStatus nextStatus, Side? winnerSide, ResultReason? resultReason, DateTime now, CancellationToken ct);
    Task<AiPersistStatus> RestartAsync(Guid gameId, Guid userId, long expectedVersion, Game restarted, DateTime now, CancellationToken ct);
    Task<AiPersistStatus> ResignAsync(Guid gameId, Guid userId, long expectedVersion, DateTime now, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(DateTime now, int take, CancellationToken ct);
    Task<Game?> ClaimAsync(Guid gameId, DateTime now, DateTime leaseUntilUtc, CancellationToken ct);
    Task<AiCommitResult> CommitAiMoveAsync(Guid gameId, long expectedVersion, GameMove move, string fenAfter, Side nextTurn,
        PersistedStatus nextStatus, Side? winnerSide, ResultReason? resultReason, int completedDepth, long elapsedMs, long nodesSearched, DateTime now, CancellationToken ct);
    Task<AiPersistStatus> FailOrRetryAsync(Guid gameId, long expectedVersion, string safeError, DateTime now, CancellationToken ct);
}

public interface IAiTurnQueue { bool TryEnqueue(Guid gameId); }
public interface IAiSearchCoordinator
{
    AiSearchSession Begin(Guid gameId, CancellationToken applicationStopping);
    void Cancel(Guid gameId);
}

public interface IAiTurnProcessor { Task ProcessAsync(Guid gameId, CancellationToken cancellationToken); }

public interface IAiGameService
{
    Task<AiGameOperationResult> CreateAiGameAsync(Guid userId, AiDifficulty difficulty, Side humanSide, CancellationToken ct);
    Task<AiGameOperationResult> GetAiGameAsync(Guid gameId, Guid userId, CancellationToken ct);
    Task<AiGameOperationResult> SubmitHumanMoveAsync(Guid gameId, Guid userId, int fromFile, int fromRank, int toFile, int toRank,
        long expectedVersion, Guid clientMoveId, CancellationToken ct);
    Task<AiGameHistoryInfo?> GetAiMoveHistoryAsync(Guid gameId, Guid userId, CancellationToken ct);
    Task<AiGameOperationResult> RestartAiGameAsync(Guid gameId, Guid userId, long expectedVersion, CancellationToken ct);
    Task<AiGameOperationResult> ResignAiGameAsync(Guid gameId, Guid userId, long expectedVersion, CancellationToken ct);
    Task<AiAnalysisInfo?> GetAnalysisStatusAsync(Guid gameId, Guid userId, CancellationToken ct);
}

public sealed class AiGameService(IAiGameRepository repository, IXiangqiRules rules, IAiTurnQueue queue,
    IAiSearchCoordinator coordinator, TimeProvider timeProvider) : IAiGameService
{
    public async Task<AiGameOperationResult> CreateAiGameAsync(Guid userId, AiDifficulty difficulty, Side humanSide, CancellationToken ct)
    {
        if (userId == Guid.Empty) return new(AiGameOperationStatus.Forbidden);
        if (!Enum.IsDefined(difficulty)) return new(AiGameOperationStatus.InvalidDifficulty);
        if (!Enum.IsDefined(humanSide)) return new(AiGameOperationStatus.InvalidSide);
        var now = Now();
        var aiSide = Opponent(humanSide);
        var game = new Game
        {
            Mode = GameMode.Ai, Status = PersistedStatus.InProgress, CurrentFen = BoardState.CreateInitialBoard().ToFen(),
            CurrentTurn = Side.Red, HumanPlayerId = userId, HumanSide = humanSide, AiSide = aiSide, AiDifficulty = difficulty,
            RedPlayerId = humanSide == Side.Red ? userId : null, BlackPlayerId = humanSide == Side.Black ? userId : null,
            AiTurnStatus = aiSide == Side.Red ? AiTurnStatus.Queued : AiTurnStatus.WaitingForHuman,
            StartedAt = now, CreatedAt = now, UpdatedAt = now
        };
        await repository.CreateAsync(game, ct);
        if (game.AiTurnStatus == AiTurnStatus.Queued) queue.TryEnqueue(game.Id);
        return new(AiGameOperationStatus.Ok, ToInfo(game));
    }

    public async Task<AiGameOperationResult> GetAiGameAsync(Guid gameId, Guid userId, CancellationToken ct)
    {
        var game = await repository.GetForHumanAsync(gameId, userId, ct);
        return game is null ? new(AiGameOperationStatus.NotFound) : new(AiGameOperationStatus.Ok, ToInfo(game));
    }

    public async Task<AiGameOperationResult> SubmitHumanMoveAsync(Guid gameId, Guid userId, int fromFile, int fromRank, int toFile, int toRank,
        long expectedVersion, Guid clientMoveId, CancellationToken ct)
    {
        if (clientMoveId == Guid.Empty || expectedVersion < 0) return new(AiGameOperationStatus.InvalidMove);
        var game = await repository.GetForHumanAsync(gameId, userId, ct);
        if (game is null) return new(AiGameOperationStatus.NotFound);
        var from = new Position(fromFile, fromRank); var to = new Position(toFile, toRank);
        if (!from.IsValid || !to.IsValid) return new(AiGameOperationStatus.InvalidMove, ToInfo(game));
        var squareFrom = Square(from); var squareTo = Square(to);
        var prior = await repository.GetMoveByClientMoveIdAsync(gameId, clientMoveId, ct);
        if (prior is not null)
        {
            var same = !prior.IsAiMove && prior.PlayerId == userId && prior.FromSquare == squareFrom && prior.ToSquare == squareTo && prior.Ply == expectedVersion + 1;
            return same ? new(AiGameOperationStatus.Ok, ToInfo(game), prior.Id, true) : new(AiGameOperationStatus.IdempotencyConflict, ToInfo(game));
        }
        if (game.Status != PersistedStatus.InProgress) return new(AiGameOperationStatus.Finished, ToInfo(game));
        if (game.Version != expectedVersion) return new(AiGameOperationStatus.VersionConflict, ToInfo(game));
        if (game.HumanSide != game.CurrentTurn || game.AiTurnStatus != AiTurnStatus.WaitingForHuman) return new(AiGameOperationStatus.WrongTurn, ToInfo(game));
        BoardState board;
        try { board = BoardState.FromFen(game.CurrentFen); } catch (FormatException) { return new(AiGameOperationStatus.InvalidMove, ToInfo(game)); }
        var piece = board.GetPieceAt(from);
        if (piece?.Color != ToColor(game.HumanSide!.Value)) return new(AiGameOperationStatus.InvalidMove, ToInfo(game));
        var captured = board.GetPieceAt(to);
        var applied = rules.TryApplyMove(board, new EngineMove(from, to));
        if (!applied.Success || applied.Board is null) return new(AiGameOperationStatus.InvalidMove, ToInfo(game));
        var next = applied.Board;
        var ended = applied.GameResult is not null;
        Side? winner = ended ? game.CurrentTurn : null;
        var nextStatus = ended ? PersistedStatus.Finished : PersistedStatus.InProgress;
        ResultReason? reason = ended ? rules.IsInCheck(next, next.SideToMove) ? ResultReason.Checkmate : ResultReason.Stalemate : null;
        var now = Now();
        var move = new GameMove { GameId = gameId, Ply = checked((int)game.Version + 1), FromSquare = squareFrom, ToSquare = squareTo,
            Piece = PieceCode(piece), CapturedPiece = captured is null ? null : PieceCode(captured), PlayerId = userId,
            FenBefore = game.CurrentFen, FenAfter = next.ToFen(), ClientMoveId = clientMoveId, CreatedAt = now, IsAiMove = false };
        var saved = await repository.CommitHumanMoveAsync(gameId, userId, expectedVersion, move, next.ToFen(), next.SideToMove == PieceColor.Red ? Side.Red : Side.Black,
            nextStatus, winner, reason, now, ct);
        if (saved != AiPersistStatus.Saved)
        {
            var latest = await repository.GetForHumanAsync(gameId, userId, ct);
            return new(saved == AiPersistStatus.Duplicate ? AiGameOperationStatus.IdempotencyConflict : AiGameOperationStatus.VersionConflict, latest is null ? null : ToInfo(latest));
        }
        if (!ended) queue.TryEnqueue(gameId);
        var updated = await repository.GetForHumanAsync(gameId, userId, ct);
        return new(AiGameOperationStatus.Ok, updated is null ? null : ToInfo(updated), move.Id);
    }

    public async Task<AiGameHistoryInfo?> GetAiMoveHistoryAsync(Guid gameId, Guid userId, CancellationToken ct)
    {
        var game = await repository.GetForHumanAsync(gameId, userId, ct);
        if (game is null) return null;
        return new(ToInfo(game), await repository.GetMovesForHumanAsync(gameId, userId, ct) ?? []);
    }

    public async Task<AiGameOperationResult> RestartAiGameAsync(Guid gameId, Guid userId, long expectedVersion, CancellationToken ct)
    {
        if (expectedVersion < 0) return new(AiGameOperationStatus.VersionConflict);
        var current = await repository.GetForHumanAsync(gameId, userId, ct);
        if (current is null) return new(AiGameOperationStatus.NotFound);
        var now = Now();
        var next = NewAiGame(userId, current.AiDifficulty!.Value, current.HumanSide!.Value, now);
        var saved = await repository.RestartAsync(gameId, userId, expectedVersion, next, now, ct);
        if (saved != AiPersistStatus.Saved) return new(AiGameOperationStatus.VersionConflict);
        coordinator.Cancel(gameId);
        if (next.AiTurnStatus == AiTurnStatus.Queued) queue.TryEnqueue(next.Id);
        return new(AiGameOperationStatus.Ok, ToInfo(next));
    }

    public async Task<AiGameOperationResult> ResignAiGameAsync(Guid gameId, Guid userId, long expectedVersion, CancellationToken ct)
    {
        var saved = await repository.ResignAsync(gameId, userId, expectedVersion, Now(), ct);
        if (saved != AiPersistStatus.Saved)
        {
            var latest = await repository.GetForHumanAsync(gameId, userId, ct);
            return new(latest is null ? AiGameOperationStatus.NotFound : AiGameOperationStatus.VersionConflict, latest is null ? null : ToInfo(latest));
        }
        coordinator.Cancel(gameId);
        var game = await repository.GetForHumanAsync(gameId, userId, ct);
        return new(AiGameOperationStatus.Ok, game is null ? null : ToInfo(game));
    }

    public async Task<AiAnalysisInfo?> GetAnalysisStatusAsync(Guid gameId, Guid userId, CancellationToken ct)
    {
        var game = await repository.GetForHumanAsync(gameId, userId, ct);
        return game is null ? null : new(gameId, game.AiTurnStatus, game.AiCompletedDepth, game.AiElapsedMs, game.AiNodesSearched, game.Version);
    }

    private Game NewAiGame(Guid userId, AiDifficulty difficulty, Side humanSide, DateTime now)
    {
        var aiSide = Opponent(humanSide);
        return new Game { Mode = GameMode.Ai, Status = PersistedStatus.InProgress, CurrentFen = BoardState.CreateInitialBoard().ToFen(),
            CurrentTurn = Side.Red, HumanPlayerId = userId, HumanSide = humanSide, AiSide = aiSide, AiDifficulty = difficulty,
            RedPlayerId = humanSide == Side.Red ? userId : null, BlackPlayerId = humanSide == Side.Black ? userId : null,
            AiTurnStatus = aiSide == Side.Red ? AiTurnStatus.Queued : AiTurnStatus.WaitingForHuman, StartedAt = now, CreatedAt = now, UpdatedAt = now };
    }
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
    private AiGameInfo ToInfo(Game g)
    {
        var board = BoardState.FromFen(g.CurrentFen);
        return new(g.Id, g.HumanPlayerId!.Value, g.HumanSide!.Value, g.AiSide!.Value, g.AiDifficulty!.Value,
        g.CurrentFen, g.CurrentTurn, g.Status, g.AiTurnStatus, g.Version, g.WinnerSide, g.ResultReason, g.EndedAt, g.AiLastError,
        g.AiCompletedDepth, g.AiElapsedMs, g.AiNodesSearched, g.Status == PersistedStatus.InProgress && rules.IsInCheck(board, board.SideToMove));
    }
    private static Side Opponent(Side side) => side == Side.Red ? Side.Black : Side.Red;
    private static PieceColor ToColor(Side side) => side == Side.Red ? PieceColor.Red : PieceColor.Black;
    private static string Square(Position p) => $"{(char)('a' + p.File)}{p.Rank}";
    private static string PieceCode(Piece p) => $"{p.Color}:{p.Type}";
}
