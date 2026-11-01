using System.Diagnostics;
using XiangqiOnline.GameEngine.AI.Evaluation;
using XiangqiOnline.GameEngine.AI.Models;

namespace XiangqiOnline.GameEngine.AI.Search;

public sealed class AlphaBetaSearch(IXiangqiRules rules, BoardEvaluator evaluator,
    MoveOrdering ordering, ZobristHasher hasher)
{
    private const int MateThreshold = BoardEvaluator.MateScore - 10_000;
    private readonly TranspositionTable _table = new();
    private Stopwatch _clock = new();
    private SearchOptions _options = new(1, 1000, 10_000);
    private CancellationToken _token;
    private long _nodes, _cutoffs, _ttHits;

    public SearchResult Search(BoardState position, SearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (options.TargetDepth < 1 || options.TimeBudgetMs < 1 || options.NodeBudget < 1) throw new ArgumentOutOfRangeException(nameof(options));
        var initialMoves = rules.GenerateLegalMovesForSide(position, position.SideToMove);
        if (initialMoves.Count == 0) throw new InvalidOperationException("Không thể tìm nước đi khi vị trí đã kết thúc.");
        _options = options; _token = cancellationToken; _clock = Stopwatch.StartNew();
        _nodes = _cutoffs = _ttHits = 0;
        _table.NextGeneration();
        var fallback = initialMoves[0];
        var bestMove = fallback;
        var bestScore = evaluator.Evaluate(MinimaxSearch.Apply(position, fallback));
        var completedDepth = 0;
        IReadOnlyList<ScoredMove> rootMoves = [new(fallback, bestScore)];

        for (var depth = 1; depth <= options.TargetDepth; depth++)
        {
            _table.NextGeneration();
            try
            {
                var (move, score, candidates) = SearchRoot(position, depth, bestMove);
                bestMove = move; bestScore = score; rootMoves = candidates; completedDepth = depth;
            }
            catch (SearchBudgetExceededException) { break; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
        }
        _clock.Stop();
        var elapsed = _clock.ElapsedMilliseconds;
        var stats = new SearchStatistics(_nodes, _cutoffs, _ttHits, completedDepth, elapsed,
            elapsed == 0 ? _nodes * 1000 : _nodes * 1000 / elapsed);
        return new(bestMove, bestScore, stats, rootMoves, completedDepth == 0);
    }

    private (Move Move, int Score, IReadOnlyList<ScoredMove> Candidates) SearchRoot(BoardState board, int depth, Move preferred)
    {
        CheckBudget();
        var alpha = -BoardEvaluator.MateScore;
        const int beta = BoardEvaluator.MateScore;
        var moves = rules.GenerateLegalMovesForSide(board, board.SideToMove);
        var ordered = _options.UseMoveOrdering ? ordering.Order(board, moves, preferred) : moves;
        var candidates = new List<ScoredMove>(ordered.Count);
        var bestMove = ordered[0];
        var best = int.MinValue;
        foreach (var move in ordered)
        {
            CheckBudget();
            var score = -SearchNode(MinimaxSearch.Apply(board, move), depth - 1, -beta, -alpha, 1);
            candidates.Add(new(move, score));
            if (score > best) { best = score; bestMove = move; }
            if (score > alpha) alpha = score;
        }
        return (bestMove, best, candidates.OrderByDescending(item => item.Score).ToArray());
    }

    private int SearchNode(BoardState board, int depth, int alpha, int beta, int ply)
    {
        CheckBudget(); _nodes++;
        var key = hasher.Hash(board);
        Move? ttMove = null;
        var alphaOriginal = alpha;
        if (_options.UseTranspositionTable && _table.TryGet(key, out var entry))
        {
            _ttHits++; ttMove = entry.BestMove;
            if (entry.Depth >= depth)
            {
                var score = UnpackMate(entry.Score, ply);
                if (entry.Bound == BoundType.Exact) return score;
                if (entry.Bound == BoundType.LowerBound) alpha = Math.Max(alpha, score);
                else if (entry.Bound == BoundType.UpperBound) beta = Math.Min(beta, score);
                if (alpha >= beta) return score;
            }
        }

        if (depth <= 0) return _options.UseQuiescence ? Quiescence(board, alpha, beta, ply, 0) : evaluator.Evaluate(board);
        var moves = rules.GenerateLegalMovesForSide(board, board.SideToMove);
        if (moves.Count == 0) return -BoardEvaluator.MateScore + ply;
        var ordered = _options.UseMoveOrdering ? ordering.Order(board, moves, ttMove) : moves;
        var best = int.MinValue;
        Move? bestMove = null;
        foreach (var move in ordered)
        {
            var score = -SearchNode(MinimaxSearch.Apply(board, move), depth - 1, -beta, -alpha, ply + 1);
            if (score > best) { best = score; bestMove = move; }
            if (best > alpha) alpha = best;
            if (alpha >= beta) { _cutoffs++; break; }
        }
        if (_options.UseTranspositionTable)
        {
            var bound = best <= alphaOriginal ? BoundType.UpperBound : best >= beta ? BoundType.LowerBound : BoundType.Exact;
            _table.Store(new(key, depth, PackMate(best, ply), bound, bestMove, _table.Generation));
        }
        return best;
    }

    private int Quiescence(BoardState board, int alpha, int beta, int ply, int qPly)
    {
        CheckBudget(); _nodes++;
        var inCheck = rules.IsInCheck(board, board.SideToMove);
        var moves = rules.GenerateLegalMovesForSide(board, board.SideToMove);
        if (moves.Count == 0) return -BoardEvaluator.MateScore + ply;
        var standPat = evaluator.Evaluate(board);
        if (!inCheck)
        {
            if (standPat >= beta) return standPat;
            if (standPat > alpha) alpha = standPat;
            if (qPly >= _options.QuiescenceDepth) return standPat;
        }
        else if (qPly >= _options.QuiescenceDepth)
        {
            // A checked side may never stand pat. At the safety horizon, examine every
            // legal evasion once, then use a static score for the resulting positions.
            var bestEvasion = int.MinValue;
            foreach (var evasion in moves)
            {
                CheckBudget();
                var child = MinimaxSearch.Apply(board, evasion);
                var replies = rules.GenerateLegalMovesForSide(child, child.SideToMove);
                var score = replies.Count == 0 ? BoardEvaluator.MateScore - (ply + 1) : -evaluator.Evaluate(child);
                bestEvasion = Math.Max(bestEvasion, score);
            }
            return bestEvasion;
        }

        var tactical = inCheck ? moves : moves.Where(move => board.GetPieceAt(move.To) is not null
            || rules.IsInCheck(MinimaxSearch.Apply(board, move), board.SideToMove == PieceColor.Red ? PieceColor.Black : PieceColor.Red)).ToArray();
        var ordered = _options.UseMoveOrdering ? ordering.Order(board, tactical) : tactical;
        foreach (var move in ordered)
        {
            var score = -Quiescence(MinimaxSearch.Apply(board, move), -beta, -alpha, ply + 1, qPly + 1);
            if (score >= beta) return score;
            if (score > alpha) alpha = score;
        }
        return alpha;
    }

    private void CheckBudget()
    {
        if (_token.IsCancellationRequested) throw new OperationCanceledException(_token);
        if (_nodes >= _options.NodeBudget || ((_nodes & 63) == 0 && _clock.ElapsedMilliseconds >= _options.TimeBudgetMs))
            throw new SearchBudgetExceededException();
    }

    private static int PackMate(int score, int ply) => score > MateThreshold ? score + ply : score < -MateThreshold ? score - ply : score;
    private static int UnpackMate(int score, int ply) => score > MateThreshold ? score - ply : score < -MateThreshold ? score + ply : score;
    private sealed class SearchBudgetExceededException : Exception;
}
