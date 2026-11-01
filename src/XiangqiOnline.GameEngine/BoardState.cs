using System.Text.Json;

namespace XiangqiOnline.GameEngine;

public enum PieceType { General, Advisor, Elephant, Horse, Chariot, Cannon, Soldier }
public enum PieceColor { Red, Black }
public enum GameStatus { Ready, InProgress, Finished }
public enum GameResult { RedWins, BlackWins, Draw }
public enum MoveErrorCode { None, InvalidCoordinates, SourceEmpty, WrongTurn, OwnPieceAtDestination, IllegalMovement, LeavesGeneralInCheck }

public sealed record Piece(PieceType Type, PieceColor Color);
public readonly record struct Position(int File, int Rank)
{
    public bool IsValid => File is >= 0 and < 9 && Rank is >= 0 and < 10;
}
public sealed record Move(Position From, Position To);
public sealed record MoveResult(bool Success, BoardState? Board, MoveErrorCode ErrorCode, GameResult? GameResult);

public sealed class BoardState
{
    private readonly Piece?[] _squares;
    public PieceColor SideToMove { get; set; } = PieceColor.Red;
    public IReadOnlyList<Piece?> Squares => _squares;

    public BoardState() : this(new Piece?[90]) { }
    private BoardState(Piece?[] squares) => _squares = squares;

    public static BoardState CreateInitialBoard()
    {
        var board = new BoardState();
        PieceType[] backRank = [PieceType.Chariot, PieceType.Horse, PieceType.Elephant, PieceType.Advisor, PieceType.General, PieceType.Advisor, PieceType.Elephant, PieceType.Horse, PieceType.Chariot];
        for (var file = 0; file < 9; file++)
        {
            board.SetPieceAt(new Position(file, 0), new Piece(backRank[file], PieceColor.Black));
            board.SetPieceAt(new Position(file, 9), new Piece(backRank[file], PieceColor.Red));
        }
        foreach (var file in new[] { 1, 7 })
        {
            board.SetPieceAt(new Position(file, 2), new Piece(PieceType.Cannon, PieceColor.Black));
            board.SetPieceAt(new Position(file, 7), new Piece(PieceType.Cannon, PieceColor.Red));
        }
        foreach (var file in new[] { 0, 2, 4, 6, 8 })
        {
            board.SetPieceAt(new Position(file, 3), new Piece(PieceType.Soldier, PieceColor.Black));
            board.SetPieceAt(new Position(file, 6), new Piece(PieceType.Soldier, PieceColor.Red));
        }
        return board;
    }

    public Piece? GetPieceAt(Position position) => position.IsValid ? _squares[position.Rank * 9 + position.File] : throw new ArgumentOutOfRangeException(nameof(position));
    public BoardState Clone() => new((Piece?[])_squares.Clone()) { SideToMove = SideToMove };
    public void SetPieceAt(Position position, Piece? piece)
    {
        if (!position.IsValid) throw new ArgumentOutOfRangeException(nameof(position));
        _squares[position.Rank * 9 + position.File] = piece;
    }
    public bool ValidateBoardStructure()
    {
        if (_squares.Length != 90 || !Enum.IsDefined(SideToMove)) return false;
        foreach (var piece in _squares)
            if (piece is not null && (!Enum.IsDefined(piece.Type) || !Enum.IsDefined(piece.Color))) return false;
        return _squares.Count(piece => piece?.Type == PieceType.General && piece.Color == PieceColor.Red) == 1
            && _squares.Count(piece => piece?.Type == PieceType.General && piece.Color == PieceColor.Black) == 1;
    }

    public string SerializeBoard() => JsonSerializer.Serialize(new BoardDocument(_squares, SideToMove));
    public static BoardState DeserializeBoard(string json)
    {
        var document = JsonSerializer.Deserialize<BoardDocument>(json) ?? throw new FormatException("Invalid board JSON.");
        var state = new BoardState(document.Squares) { SideToMove = document.SideToMove };
        if (state._squares.Length != 90 || !state.ValidateBoardStructure()) throw new FormatException("Invalid board structure.");
        return state;
    }

    private sealed record BoardDocument(Piece?[] Squares, PieceColor SideToMove);

    public string ToFen()
    {
        var ranks = new List<string>(10);
        for (var rank = 0; rank < 10; rank++)
        {
            var text = "";
            var empty = 0;
            for (var file = 0; file < 9; file++)
            {
                var piece = _squares[rank * 9 + file];
                if (piece is null) { empty++; continue; }
                if (empty > 0) { text += empty; empty = 0; }
                var symbol = piece.Type switch { PieceType.General => 'k', PieceType.Advisor => 'a', PieceType.Elephant => 'b', PieceType.Horse => 'n', PieceType.Chariot => 'r', PieceType.Cannon => 'c', PieceType.Soldier => 'p', _ => throw new FormatException("Unknown piece type.") };
                text += piece.Color == PieceColor.Red ? char.ToUpperInvariant(symbol) : symbol;
            }
            if (empty > 0) text += empty;
            ranks.Add(text);
        }
        return string.Join('/', ranks) + (SideToMove == PieceColor.Red ? " w - - 0 1" : " b - - 0 1");
    }

    public static BoardState FromFen(string fen)
    {
        var fields = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 2) throw new FormatException("Xiangqi FEN must include board and side to move.");
        var ranks = fields[0].Split('/');
        if (ranks.Length != 10) throw new FormatException("Xiangqi FEN must contain 10 ranks.");
        var board = new BoardState { SideToMove = fields[1] switch { "w" => PieceColor.Red, "b" => PieceColor.Black, _ => throw new FormatException("Invalid side to move.") } };
        for (var rank = 0; rank < 10; rank++)
        {
            var file = 0;
            foreach (var symbol in ranks[rank])
            {
                if (char.IsAsciiDigit(symbol)) { file += symbol - '0'; continue; }
                if (file >= 9) throw new FormatException("FEN rank exceeds 9 files.");
                var type = char.ToLowerInvariant(symbol) switch { 'k' => PieceType.General, 'a' => PieceType.Advisor, 'b' => PieceType.Elephant, 'n' => PieceType.Horse, 'r' => PieceType.Chariot, 'c' => PieceType.Cannon, 'p' => PieceType.Soldier, _ => throw new FormatException("Unknown FEN piece.") };
                board.SetPieceAt(new Position(file++, rank), new Piece(type, char.IsUpper(symbol) ? PieceColor.Red : PieceColor.Black));
            }
            if (file != 9) throw new FormatException("FEN rank must contain exactly 9 files.");
        }
        if (!board.ValidateBoardStructure()) throw new FormatException("FEN must contain one general of each color.");
        return board;
    }
}

public interface IXiangqiRules
{
    IReadOnlyList<Move> GenerateLegalMoves(BoardState board, Position from);
    bool ValidateMove(BoardState board, Move move);
    MoveResult TryApplyMove(BoardState board, Move move);
    BoardState ApplyMove(BoardState board, Move move);
    bool IsInCheck(BoardState board, PieceColor color);
    GameResult? GetGameResult(BoardState board);
}
