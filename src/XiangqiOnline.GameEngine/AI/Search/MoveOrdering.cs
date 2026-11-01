using XiangqiOnline.GameEngine.AI.Evaluation;

namespace XiangqiOnline.GameEngine.AI.Search;

public sealed class MoveOrdering(IXiangqiRules? rules = null)
{
    public IReadOnlyList<Move> Order(BoardState board, IReadOnlyList<Move> moves, Move? preferred = null)
    {
        return moves.OrderByDescending(move => move == preferred ? 10_000_000 : Score(board, move)).ToArray();
    }

    private int Score(BoardState board, Move move)
    {
        var capture = CaptureScore(board, move);
        if (capture > 0) return capture;
        if (rules is null) return 0;
        var next = MinimaxSearch.Apply(board, move);
        return rules.IsInCheck(next, next.SideToMove) ? 50_000 : 0;
    }

    private static int CaptureScore(BoardState board, Move move)
    {
        var victim = board.GetPieceAt(move.To);
        if (victim is null) return 0;
        var attacker = board.GetPieceAt(move.From);
        return 100_000 + BoardEvaluator.MaterialValue(victim.Type) * 10 - (attacker is null ? 0 : BoardEvaluator.MaterialValue(attacker.Type));
    }
}
