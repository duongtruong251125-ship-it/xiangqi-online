using XiangqiOnline.GameEngine.AI.Evaluation;

namespace XiangqiOnline.GameEngine.AI.Search;

public sealed class MinimaxSearch(IXiangqiRules rules, BoardEvaluator evaluator)
{
    private long _nodes;
    public (Move? BestMove, int Score, long Nodes) Search(BoardState board, int depth, CancellationToken cancellationToken = default)
    {
        if (depth < 1) throw new ArgumentOutOfRangeException(nameof(depth));
        _nodes = 0;
        Move? best = null;
        var bestScore = int.MinValue;
        foreach (var move in rules.GenerateLegalMovesForSide(board, board.SideToMove))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var score = -Node(Apply(board, move), depth - 1, 1, cancellationToken);
            if (score > bestScore) { bestScore = score; best = move; }
        }
        return (best, best is null ? -BoardEvaluator.MateScore : bestScore, _nodes);
    }

    private int Node(BoardState board, int depth, int ply, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); _nodes++;
        var moves = rules.GenerateLegalMovesForSide(board, board.SideToMove);
        if (moves.Count == 0) return -BoardEvaluator.MateScore + ply;
        if (depth == 0) return evaluator.Evaluate(board);
        var best = int.MinValue;
        foreach (var move in moves) best = Math.Max(best, -Node(Apply(board, move), depth - 1, ply + 1, token));
        return best;
    }

    internal static BoardState Apply(BoardState board, Move move)
    {
        var next = board.Clone();
        next.SetPieceAt(move.To, next.GetPieceAt(move.From));
        next.SetPieceAt(move.From, null);
        next.SideToMove = board.SideToMove == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
        return next;
    }
}
