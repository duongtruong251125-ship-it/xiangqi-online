using XiangqiOnline.GameEngine.AI.Search;

namespace XiangqiOnline.GameEngine.AI.Evaluation;

public sealed class BoardEvaluator(IXiangqiRules rules)
{
    private readonly LegalMoveGenerator _moveGenerator = new(rules);
    public const int MateScore = 1_000_000;
    private static readonly int[] Material = [0, 200, 200, 400, 900, 450, 100];

    // Positive score is always from the side-to-move's point of view (Negamax contract).
    public int Evaluate(BoardState board)
    {
        var red = 0;
        var black = 0;
        var redGuards = 0;
        var blackGuards = 0;
        for (var rank = 0; rank < 10; rank++)
        for (var file = 0; file < 9; file++)
        {
            var piece = board.GetPieceAt(new Position(file, rank));
            if (piece is null || piece.Type == PieceType.General) continue;
            var score = Material[(int)piece.Type] + PositionBonus(piece, file, rank);
            if (piece.Color == PieceColor.Red) { red += score; if (piece.Type is PieceType.Advisor or PieceType.Elephant) redGuards++; }
            else { black += score; if (piece.Type is PieceType.Advisor or PieceType.Elephant) blackGuards++; }
        }
        var redGeneral = rules.IsInCheck(board, PieceColor.Red) ? 120 : 0;
        var blackGeneral = rules.IsInCheck(board, PieceColor.Black) ? 120 : 0;
        red += Math.Min(2, redGuards) * 18 - redGeneral;
        black += Math.Min(2, blackGuards) * 18 - blackGeneral;
        var redMoves = _moveGenerator.Generate(board, PieceColor.Red);
        var blackMoves = _moveGenerator.Generate(board, PieceColor.Black);
        red += Math.Min(redMoves.Count, 40) * 2 - ThreatenedMaterial(board, blackMoves, PieceColor.Red);
        black += Math.Min(blackMoves.Count, 40) * 2 - ThreatenedMaterial(board, redMoves, PieceColor.Black);
        var redPerspective = red - black;
        return board.SideToMove == PieceColor.Red ? redPerspective : -redPerspective;
    }

    public static int MaterialValue(PieceType type) => Material[(int)type];

    private static int ThreatenedMaterial(BoardState board, IReadOnlyList<Move> enemyMoves, PieceColor defendingSide)
    {
        var threatened = new HashSet<int>();
        foreach (var move in enemyMoves)
            if (board.GetPieceAt(move.To) is { Color: var color, Type: var type } && color == defendingSide)
                threatened.Add(move.To.Rank * 9 + move.To.File);
        return threatened.Sum(square => Material[(int)board.GetPieceAt(new Position(square % 9, square / 9))!.Type] / 12);
    }

    private static int PositionBonus(Piece piece, int file, int rank)
    {
        var forwardProgress = piece.Color == PieceColor.Red ? 9 - rank : rank;
        var center = 4 - Math.Abs(4 - file);
        return piece.Type switch
        {
            PieceType.Soldier => forwardProgress * 8 + (forwardProgress >= 5 ? 45 + center * 8 : 0),
            PieceType.Horse => center * 8 + (piece.Color == PieceColor.Red ? (9 - rank) : rank) * 2,
            PieceType.Chariot => center * 3,
            PieceType.Cannon => center * 4,
            PieceType.Advisor or PieceType.Elephant => center * 2,
            _ => 0
        };
    }
}
