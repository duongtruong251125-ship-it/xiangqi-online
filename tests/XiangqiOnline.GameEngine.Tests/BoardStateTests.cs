using XiangqiOnline.GameEngine;

namespace XiangqiOnline.GameEngine.Tests;

public sealed class BoardStateTests
{
    [Fact]
    public void InitialBoardContainsThirtyTwoPiecesEvenlySplit()
    {
        var board = BoardState.CreateInitialBoard();
        Assert.Equal(32, board.Squares.Count(piece => piece is not null));
        Assert.Equal(16, board.Squares.Count(piece => piece?.Color == PieceColor.Red));
        Assert.Equal(16, board.Squares.Count(piece => piece?.Color == PieceColor.Black));
    }

    [Fact]
    public void InitialPiecesOccupyExpectedSquares()
    {
        var board = BoardState.CreateInitialBoard();
        Assert.Equal(new Piece(PieceType.General, PieceColor.Black), board.GetPieceAt(new Position(4, 0)));
        Assert.Equal(new Piece(PieceType.General, PieceColor.Red), board.GetPieceAt(new Position(4, 9)));
        Assert.Equal(new Piece(PieceType.Cannon, PieceColor.Black), board.GetPieceAt(new Position(1, 2)));
        Assert.Equal(new Piece(PieceType.Cannon, PieceColor.Red), board.GetPieceAt(new Position(7, 7)));
        Assert.Equal(new Piece(PieceType.Soldier, PieceColor.Black), board.GetPieceAt(new Position(0, 3)));
        Assert.Equal(new Piece(PieceType.Soldier, PieceColor.Red), board.GetPieceAt(new Position(8, 6)));
    }

    [Fact]
    public void FenAndJsonRoundTripPreserveBoardAndTurn()
    {
        var board = BoardState.CreateInitialBoard();
        board.SideToMove = PieceColor.Black;
        Assert.Equal(board.ToFen(), BoardState.FromFen(board.ToFen()).ToFen());
        Assert.Equal(board.ToFen(), BoardState.DeserializeBoard(board.SerializeBoard()).ToFen());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 0)]
    [InlineData(0, 10)]
    public void InvalidCoordinatesAreRejected(int file, int rank) => Assert.Throws<ArgumentOutOfRangeException>(() => BoardState.CreateInitialBoard().GetPieceAt(new Position(file, rank)));

    [Fact]
    public void FenRejectsInvalidRankWidth() => Assert.Throws<FormatException>(() => BoardState.FromFen("9/9/9/9/9/9/9/9/9/8 w - - 0 1"));
}
