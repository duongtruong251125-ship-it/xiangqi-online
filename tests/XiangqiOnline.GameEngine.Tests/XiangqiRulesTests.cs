using XiangqiOnline.GameEngine;

namespace XiangqiOnline.GameEngine.Tests;

public sealed class XiangqiRulesTests
{
    private readonly XiangqiRules _rules = new();

    [Fact]
    public void InitialPositionHasFifteenLegalRedMoves()
    {
        var board = BoardState.CreateInitialBoard();
        var moves = Enumerable.Range(0, 9).SelectMany(file => _rules.GenerateLegalMoves(board, new Position(file, 6))).Count()
            + _rules.GenerateLegalMoves(board, new Position(1, 9)).Count()
            + _rules.GenerateLegalMoves(board, new Position(7, 9)).Count()
            + _rules.GenerateLegalMoves(board, new Position(2, 9)).Count()
            + _rules.GenerateLegalMoves(board, new Position(6, 9)).Count()
            + _rules.GenerateLegalMoves(board, new Position(3, 9)).Count()
            + _rules.GenerateLegalMoves(board, new Position(5, 9)).Count();

        Assert.Equal(15, moves);
    }

    [Fact]
    public void HorseLegAndElephantEyeBlockMoves()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(4, 7), new Piece(PieceType.Horse, PieceColor.Red));
        board.SetPieceAt(new Position(4, 8), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(2, 9), new Piece(PieceType.Elephant, PieceColor.Red));
        board.SetPieceAt(new Position(1, 8), new Piece(PieceType.Soldier, PieceColor.Red));

        Assert.DoesNotContain(new Move(new Position(4, 7), new Position(3, 9)), _rules.GenerateLegalMoves(board, new Position(4, 7)));
        Assert.DoesNotContain(new Move(new Position(2, 9), new Position(0, 7)), _rules.GenerateLegalMoves(board, new Position(2, 9)));
    }

    [Fact]
    public void GeneralAndAdvisorStayInsideTheirPalaceAndUseCorrectSteps()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(3, 9), new Piece(PieceType.Advisor, PieceColor.Red));
        var advisorMoves = _rules.GenerateLegalMoves(board, new Position(3, 9));
        Assert.Contains(new Move(new Position(3, 9), new Position(4, 8)), advisorMoves);
        Assert.DoesNotContain(new Move(new Position(3, 9), new Position(3, 8)), advisorMoves);
        var generalMoves = _rules.GenerateLegalMoves(board, new Position(4, 9));
        Assert.Contains(new Move(new Position(4, 9), new Position(4, 8)), generalMoves);
        Assert.DoesNotContain(new Move(new Position(4, 9), new Position(4, 6)), generalMoves);
    }

    [Fact]
    public void ElephantCannotCrossRiver()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(2, 5), new Piece(PieceType.Elephant, PieceColor.Red));
        var moves = _rules.GenerateLegalMoves(board, new Position(2, 5));
        Assert.DoesNotContain(new Move(new Position(2, 5), new Position(0, 3)), moves);
        Assert.DoesNotContain(new Move(new Position(2, 5), new Position(4, 3)), moves);
    }

    [Fact]
    public void ChariotMovesUntilBlockedAndCanCaptureAnOpponent()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(0, 5), new Piece(PieceType.Chariot, PieceColor.Red));
        board.SetPieceAt(new Position(2, 5), new Piece(PieceType.Soldier, PieceColor.Black));
        board.SetPieceAt(new Position(0, 7), new Piece(PieceType.Soldier, PieceColor.Red));
        var moves = _rules.GenerateLegalMoves(board, new Position(0, 5));
        Assert.Contains(new Move(new Position(0, 5), new Position(2, 5)), moves);
        Assert.DoesNotContain(new Move(new Position(0, 5), new Position(0, 7)), moves);
        Assert.DoesNotContain(new Move(new Position(0, 5), new Position(3, 5)), moves);
        Assert.DoesNotContain(new Move(new Position(0, 5), new Position(0, 8)), moves);
    }

    [Fact]
    public void CannonRequiresExactlyOneScreenToCapture()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(0, 5), new Piece(PieceType.Cannon, PieceColor.Red));
        board.SetPieceAt(new Position(0, 7), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(0, 8), new Piece(PieceType.Soldier, PieceColor.Black));
        var moves = _rules.GenerateLegalMoves(board, new Position(0, 5));
        Assert.Contains(new Move(new Position(0, 5), new Position(0, 8)), moves);
        Assert.DoesNotContain(new Move(new Position(0, 5), new Position(0, 7)), moves);
        board.SetPieceAt(new Position(0, 6), new Piece(PieceType.Soldier, PieceColor.Black));
        Assert.DoesNotContain(new Move(new Position(0, 5), new Position(0, 8)), _rules.GenerateLegalMoves(board, new Position(0, 5)));
    }

    [Fact]
    public void SoldierCanMoveSidewaysOnlyAfterCrossingRiver()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(2, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        var beforeRiver = _rules.GenerateLegalMoves(board, new Position(2, 5));
        Assert.DoesNotContain(new Move(new Position(2, 5), new Position(1, 5)), beforeRiver);
        Assert.Contains(new Move(new Position(2, 5), new Position(2, 4)), beforeRiver);

        board.SetPieceAt(new Position(2, 4), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(2, 5), null);
        var afterRiver = _rules.GenerateLegalMoves(board, new Position(2, 4));
        Assert.Contains(new Move(new Position(2, 4), new Position(1, 4)), afterRiver);

        board.SetPieceAt(new Position(6, 4), new Piece(PieceType.Soldier, PieceColor.Black));
        board.SideToMove = PieceColor.Black;
        var blackBeforeRiver = _rules.GenerateLegalMoves(board, new Position(6, 4));
        Assert.Contains(new Move(new Position(6, 4), new Position(6, 5)), blackBeforeRiver);
        Assert.DoesNotContain(new Move(new Position(6, 4), new Position(5, 4)), blackBeforeRiver);
    }

    [Fact]
    public void CheckDetectorFindsAnAttackingChariot()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Chariot, PieceColor.Black));
        Assert.True(_rules.IsInCheck(board, PieceColor.Red));
    }

    [Fact]
    public void GeneralCannotMoveIntoAnAttackedSquare()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(3, 8), new Piece(PieceType.Chariot, PieceColor.Black));
        var moves = _rules.GenerateLegalMoves(board, new Position(4, 9));
        Assert.DoesNotContain(new Move(new Position(4, 9), new Position(3, 9)), moves);
        Assert.Contains(new Move(new Position(4, 9), new Position(5, 9)), moves);
    }

    [Fact]
    public void HorseCanCaptureAnOpponentButCannotCaptureOwnPiece()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Red));
        board.SetPieceAt(new Position(2, 7), new Piece(PieceType.Horse, PieceColor.Red));
        board.SetPieceAt(new Position(4, 6), new Piece(PieceType.Soldier, PieceColor.Black));
        board.SetPieceAt(new Position(0, 6), new Piece(PieceType.Soldier, PieceColor.Red));
        var moves = _rules.GenerateLegalMoves(board, new Position(2, 7));
        Assert.Contains(new Move(new Position(2, 7), new Position(4, 6)), moves);
        Assert.DoesNotContain(new Move(new Position(2, 7), new Position(0, 6)), moves);
    }

    [Fact]
    public void GeneralMayNotExposeFacingGenerals()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0));
        board.SetPieceAt(new Position(3, 9), new Piece(PieceType.Chariot, PieceColor.Red));
        var moves = _rules.GenerateLegalMoves(board, new Position(3, 9));
        Assert.DoesNotContain(new Move(new Position(3, 9), new Position(3, 8)), moves);
    }

    [Fact]
    public void ApplyMoveCreatesNextPositionWithoutMutatingOriginal()
    {
        var board = BoardState.CreateInitialBoard();
        var next = _rules.ApplyMove(board, new Move(new Position(1, 9), new Position(2, 7)));
        Assert.Equal(PieceColor.Red, board.SideToMove);
        Assert.Equal(PieceColor.Black, next.SideToMove);
        Assert.Equal(new Piece(PieceType.Horse, PieceColor.Red), next.GetPieceAt(new Position(2, 7)));
        Assert.Null(board.GetPieceAt(new Position(2, 7)));
    }

    [Fact]
    public void NoLegalMoveLosesEvenWhenTheGeneralIsNotInCheck()
    {
        var board = PositionWithGenerals(new Position(4, 9), new Position(4, 0), PieceColor.Red);
        board.SetPieceAt(new Position(4, 8), new Piece(PieceType.Chariot, PieceColor.Black));
        board.SetPieceAt(new Position(3, 8), new Piece(PieceType.Chariot, PieceColor.Black));
        board.SetPieceAt(new Position(5, 8), new Piece(PieceType.Chariot, PieceColor.Black));
        Assert.Equal(GameResult.BlackWins, _rules.GetGameResult(board));

        var stalemate = PositionWithGenerals(new Position(4, 9), new Position(4, 0), PieceColor.Red);
        stalemate.SetPieceAt(new Position(4, 5), new Piece(PieceType.Soldier, PieceColor.Black));
        stalemate.SetPieceAt(new Position(3, 8), new Piece(PieceType.Chariot, PieceColor.Black));
        stalemate.SetPieceAt(new Position(5, 8), new Piece(PieceType.Chariot, PieceColor.Black));
        stalemate.SetPieceAt(new Position(3, 6), new Piece(PieceType.Horse, PieceColor.Black));
        Assert.False(_rules.IsInCheck(stalemate, PieceColor.Red));
        Assert.Equal(GameResult.BlackWins, _rules.GetGameResult(stalemate));
    }

    private static BoardState PositionWithGenerals(Position red, Position black, PieceColor sideToMove = PieceColor.Red)
    {
        var board = new BoardState { SideToMove = sideToMove };
        board.SetPieceAt(red, new Piece(PieceType.General, PieceColor.Red));
        board.SetPieceAt(black, new Piece(PieceType.General, PieceColor.Black));
        return board;
    }
}
