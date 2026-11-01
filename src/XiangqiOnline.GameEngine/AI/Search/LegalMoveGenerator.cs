namespace XiangqiOnline.GameEngine.AI.Search;

public sealed class LegalMoveGenerator(IXiangqiRules rules)
{
    public IReadOnlyList<Move> Generate(BoardState board, PieceColor color)
    {
        if (board.SideToMove == color) return AllFromSideToMove(board);
        var copy = board.Clone(); copy.SideToMove = color;
        return AllFromSideToMove(copy);
    }

    public IReadOnlyList<Move> AllFromSideToMove(BoardState board)
    {
        var moves = new List<Move>();
        for (var rank = 0; rank < 10; rank++)
        for (var file = 0; file < 9; file++)
        {
            var from = new Position(file, rank);
            if (board.GetPieceAt(from)?.Color == board.SideToMove) moves.AddRange(rules.GenerateLegalMoves(board, from));
        }
        return moves;
    }
}

public static class LegalMoveGeneratorExtensions
{
    public static IReadOnlyList<Move> GenerateLegalMovesForSide(this IXiangqiRules rules, BoardState board, PieceColor color)
    {
        var position = board.SideToMove == color ? board : board.Clone();
        position.SideToMove = color;
        var moves = new List<Move>();
        for (var rank = 0; rank < 10; rank++)
        for (var file = 0; file < 9; file++)
        {
            var from = new Position(file, rank);
            if (position.GetPieceAt(from)?.Color == color) moves.AddRange(rules.GenerateLegalMoves(position, from));
        }
        return moves;
    }
}
