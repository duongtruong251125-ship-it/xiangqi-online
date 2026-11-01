using System.Collections.Concurrent;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.GameEngine;
using XiangqiOnline.GameEngine.AI.Models;
using XiangqiOnline.GameEngine.AI.Services;
using EngineDifficulty = XiangqiOnline.GameEngine.AI.Models.AiDifficulty;
using PersistedDifficulty = XiangqiOnline.Domain.Enums.AiDifficulty;
using PersistedStatus = XiangqiOnline.Domain.Enums.GameStatus;

namespace XiangqiOnline.Application.AiGames;

public sealed class AiSearchSession(Guid gameId, CancellationTokenSource source, Action<Guid, CancellationTokenSource> release) : IDisposable
{
    public CancellationToken Token => source.Token;
    public void Dispose() { release(gameId, source); source.Dispose(); }
}

public sealed class AiSearchCoordinator : IAiSearchCoordinator
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _active = new();
    public AiSearchSession Begin(Guid gameId, CancellationToken applicationStopping)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
        if (!_active.TryAdd(gameId, source)) { source.Dispose(); throw new InvalidOperationException("AI search already active for this game."); }
        return new(gameId, source, Release);
    }
    public void Cancel(Guid gameId)
    {
        if (!_active.TryGetValue(gameId, out var source)) return;
        try { source.Cancel(); }
        catch (ObjectDisposedException) { }
    }
    private void Release(Guid gameId, CancellationTokenSource source) => _active.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(gameId, source));
}

public sealed class AiTurnProcessor(IAiGameRepository repository, IXiangqiRules rules, IAiTurnQueue queue,
    IAiSearchCoordinator coordinator, TimeProvider timeProvider) : IAiTurnProcessor
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    public async Task ProcessAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var game = await repository.ClaimAsync(gameId, now, now.Add(Lease), cancellationToken);
        if (game is null) return;
        try
        {
            using var session = coordinator.Begin(gameId, cancellationToken);
            var board = BoardState.FromFen(game.CurrentFen);
            var difficulty = game.AiDifficulty switch
            {
                PersistedDifficulty.Easy => EngineDifficulty.Easy,
                PersistedDifficulty.Medium => EngineDifficulty.Medium,
                PersistedDifficulty.Hard => EngineDifficulty.Hard,
                _ => throw new InvalidOperationException("AI game difficulty is missing.")
            };
            var selector = new AiMoveSelector(rules);
            var result = selector.Select(board, difficulty, session.Token);
            var move = result.BestMove;
            if (!rules.ValidateMove(board, move)) throw new InvalidOperationException("Search returned a move rejected by the rules engine.");
            var applied = rules.TryApplyMove(board, move);
            if (!applied.Success || applied.Board is null) throw new InvalidOperationException("AI move failed final rule validation.");
            var next = applied.Board;
            var finished = applied.GameResult is not null;
            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            var winnerSide = finished ? game.AiSide : null;
            ResultReason? reason = finished ? rules.IsInCheck(next, next.SideToMove) ? ResultReason.Checkmate : ResultReason.Stalemate : null;
            var aiPiece = board.GetPieceAt(move.From)!;
            var captured = board.GetPieceAt(move.To);
            var storedMove = new GameMove
            {
                GameId = gameId, Ply = checked((int)game.Version + 1), FromSquare = Square(move.From), ToSquare = Square(move.To),
                Piece = PieceCode(aiPiece), CapturedPiece = captured is null ? null : PieceCode(captured), PlayerId = null,
                FenBefore = game.CurrentFen, FenAfter = next.ToFen(), ClientMoveId = Guid.NewGuid(), CreatedAt = nowUtc, IsAiMove = true
            };
            await repository.CommitAiMoveAsync(gameId, game.Version, storedMove, next.ToFen(), ToSide(next.SideToMove),
                finished ? PersistedStatus.Finished : PersistedStatus.InProgress, winnerSide, reason,
                result.Statistics.CompletedDepth, result.Statistics.ElapsedMs, result.Statistics.NodesSearched, nowUtc, session.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            var failed = await repository.FailOrRetryAsync(gameId, game.Version, "Không thể tính nước đi AI.", timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            if (failed == AiPersistStatus.Saved) queue.TryEnqueue(gameId);
        }
    }

    private static Side ToSide(PieceColor color) => color == PieceColor.Red ? Side.Red : Side.Black;
    private static string Square(Position p) => $"{(char)('a' + p.File)}{p.Rank}";
    private static string PieceCode(Piece p) => $"{p.Color}:{p.Type}";
}
