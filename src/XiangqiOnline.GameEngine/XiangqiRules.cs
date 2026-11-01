namespace XiangqiOnline.GameEngine;

public sealed class XiangqiRules : IXiangqiRules
{
    private static readonly (int File, int Rank)[] Orthogonal = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int File, int Rank)[] Diagonal = [(1, 1), (1, -1), (-1, 1), (-1, -1)];

    public IReadOnlyList<Move> GenerateLegalMoves(BoardState board, Position from)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (!from.IsValid || board.GetPieceAt(from) is not { } piece || piece.Color != board.SideToMove) return [];
        var moves = new List<Move>();
        for (var rank = 0; rank < 10; rank++)
        for (var file = 0; file < 9; file++)
        {
            var to = new Position(file, rank);
            if (!CanMoveGeometry(board, from, to, piece, allowGeneralCapture: false)) continue;
            if (!LeavesGeneralInCheck(board, new Move(from, to), piece.Color)) moves.Add(new Move(from, to));
        }
        return moves;
    }

    public bool ValidateMove(BoardState board, Move move)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(move);
        if (!move.From.IsValid || !move.To.IsValid || move.From == move.To) return false;
        return GenerateLegalMoves(board, move.From).Contains(move);
    }

    public MoveResult TryApplyMove(BoardState board, Move move)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(move);
        if (!move.From.IsValid || !move.To.IsValid || move.From == move.To)
            return new(false, null, MoveErrorCode.InvalidCoordinates, null);
        var piece = board.GetPieceAt(move.From);
        if (piece is null) return new(false, null, MoveErrorCode.SourceEmpty, null);
        if (piece.Color != board.SideToMove) return new(false, null, MoveErrorCode.WrongTurn, null);
        if (board.GetPieceAt(move.To)?.Color == piece.Color)
            return new(false, null, MoveErrorCode.OwnPieceAtDestination, null);
        if (!CanMoveGeometry(board, move.From, move.To, piece, allowGeneralCapture: false))
            return new(false, null, MoveErrorCode.IllegalMovement, null);
        if (LeavesGeneralInCheck(board, move, piece.Color))
            return new(false, null, MoveErrorCode.LeavesGeneralInCheck, null);

        var next = board.Clone();
        next.SetPieceAt(move.To, next.GetPieceAt(move.From));
        next.SetPieceAt(move.From, null);
        next.SideToMove = Opponent(board.SideToMove);
        return new(true, next, MoveErrorCode.None, GetGameResult(next));
    }

    public BoardState ApplyMove(BoardState board, Move move)
    {
        var result = TryApplyMove(board, move);
        return result.Board ?? throw new InvalidOperationException("Nước đi không hợp lệ.");
    }

    public bool IsInCheck(BoardState board, PieceColor color)
    {
        var general = FindGeneral(board, color);
        if (general is null) return true;
        var attacker = Opponent(color);
        for (var rank = 0; rank < 10; rank++)
        for (var file = 0; file < 9; file++)
        {
            var from = new Position(file, rank);
            if (board.GetPieceAt(from) is { Color: var pieceColor } piece && pieceColor == attacker
                && CanMoveGeometry(board, from, general.Value, piece, allowGeneralCapture: true)) return true;
        }
        return false;
    }

    public GameResult? GetGameResult(BoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (HasLegalMove(board, board.SideToMove)) return null;
        return board.SideToMove == PieceColor.Red ? GameResult.BlackWins : GameResult.RedWins;
    }

    public bool IsCheckmate(BoardState board, PieceColor color) => IsInCheck(board, color) && !HasLegalMove(board, color);

    public bool IsStalemate(BoardState board, PieceColor color) => !IsInCheck(board, color) && !HasLegalMove(board, color);

    public GameResult? EvaluateGameResult(BoardState board) => GetGameResult(board);

    private bool HasLegalMove(BoardState board, PieceColor color)
    {
        var position = board.Clone();
        position.SideToMove = color;
        for (var rank = 0; rank < 10; rank++)
        for (var file = 0; file < 9; file++)
            if (position.GetPieceAt(new Position(file, rank)) is { Color: var pieceColor } && pieceColor == color
                && GenerateLegalMoves(position, new Position(file, rank)).Count != 0) return true;
        return false;
    }

    private static bool LeavesGeneralInCheck(BoardState board, Move move, PieceColor movingColor)
    {
        var next = board.Clone();
        next.SetPieceAt(move.To, next.GetPieceAt(move.From));
        next.SetPieceAt(move.From, null);
        return IsAttacked(next, movingColor);
    }

    private static bool IsAttacked(BoardState board, PieceColor color)
    {
        var general = FindGeneral(board, color);
        if (general is null) return true;
        var attacker = Opponent(color);
        for (var rank = 0; rank < 10; rank++)
        for (var file = 0; file < 9; file++)
        {
            var from = new Position(file, rank);
            var piece = board.GetPieceAt(from);
            if (piece?.Color == attacker && CanMoveGeometry(board, from, general.Value, piece, allowGeneralCapture: true)) return true;
        }
        return false;
    }

    private static bool CanMoveGeometry(BoardState board, Position from, Position to, Piece piece, bool allowGeneralCapture)
    {
        if (!to.IsValid || from == to) return false;
        var target = board.GetPieceAt(to);
        if (target?.Color == piece.Color || (!allowGeneralCapture && target?.Type == PieceType.General)) return false;
        var dx = to.File - from.File;
        var dy = to.Rank - from.Rank;
        var ax = Math.Abs(dx);
        var ay = Math.Abs(dy);

        return piece.Type switch
        {
            PieceType.General => GeneralCanMove(board, from, to, piece.Color, ax, ay),
            PieceType.Advisor => ax == 1 && ay == 1 && InPalace(to, piece.Color),
            PieceType.Elephant => ax == 2 && ay == 2 && OnOwnRiverBank(to, piece.Color)
                && board.GetPieceAt(new Position(from.File + dx / 2, from.Rank + dy / 2)) is null,
            PieceType.Horse => HorseCanMove(board, from, to, dx, dy),
            PieceType.Chariot => (dx == 0 || dy == 0) && CountScreens(board, from, to) == 0,
            PieceType.Cannon => (dx == 0 || dy == 0) && (target is null ? CountScreens(board, from, to) == 0 : CountScreens(board, from, to) == 1),
            PieceType.Soldier => SoldierCanMove(from, to, piece.Color, dx, dy),
            _ => false
        };
    }

    private static bool GeneralCanMove(BoardState board, Position from, Position to, PieceColor color, int ax, int ay)
    {
        if (InPalace(to, color) && ax + ay == 1) return true;
        return from.File == to.File && board.GetPieceAt(to) is { Type: PieceType.General, Color: var targetColor }
            && targetColor != color && CountScreens(board, from, to) == 0;
    }

    private static bool HorseCanMove(BoardState board, Position from, Position to, int dx, int dy)
    {
        if (!((Math.Abs(dx) == 2 && Math.Abs(dy) == 1) || (Math.Abs(dx) == 1 && Math.Abs(dy) == 2))) return false;
        var leg = Math.Abs(dx) == 2 ? new Position(from.File + Math.Sign(dx), from.Rank) : new Position(from.File, from.Rank + Math.Sign(dy));
        return board.GetPieceAt(leg) is null;
    }

    private static bool SoldierCanMove(Position from, Position to, PieceColor color, int dx, int dy)
    {
        var forward = color == PieceColor.Red ? -1 : 1;
        if (dx == 0 && dy == forward) return true;
        var crossedRiver = color == PieceColor.Red ? from.Rank <= 4 : from.Rank >= 5;
        return crossedRiver && Math.Abs(dx) == 1 && dy == 0;
    }

    private static bool InPalace(Position position, PieceColor color) => position.File is >= 3 and <= 5
        && (color == PieceColor.Red ? position.Rank is >= 7 and <= 9 : position.Rank is >= 0 and <= 2);

    private static bool OnOwnRiverBank(Position position, PieceColor color) => color == PieceColor.Red ? position.Rank >= 5 : position.Rank <= 4;

    private static int CountScreens(BoardState board, Position from, Position to)
    {
        if (from.File != to.File && from.Rank != to.Rank) return int.MaxValue;
        var stepFile = Math.Sign(to.File - from.File);
        var stepRank = Math.Sign(to.Rank - from.Rank);
        var screens = 0;
        for (int file = from.File + stepFile, rank = from.Rank + stepRank; file != to.File || rank != to.Rank; file += stepFile, rank += stepRank)
            if (board.GetPieceAt(new Position(file, rank)) is not null) screens++;
        return screens;
    }

    private static Position? FindGeneral(BoardState board, PieceColor color)
    {
        for (var rank = 0; rank < 10; rank++)
        for (var file = 0; file < 9; file++)
            if (board.GetPieceAt(new Position(file, rank)) is { Type: PieceType.General, Color: var pieceColor } && pieceColor == color)
                return new Position(file, rank);
        return null;
    }

    private static PieceColor Opponent(PieceColor color) => color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
}
