using System.Diagnostics;
using XiangqiOnline.GameEngine.AI.Evaluation;
using XiangqiOnline.GameEngine.AI.Models;
using XiangqiOnline.GameEngine.AI.Search;
using Xunit.Abstractions;

namespace XiangqiOnline.GameEngine.Tests;

public sealed class AiSearchTests
{
    private readonly XiangqiRules _rules = new();

    [Theory]
    [InlineData("4k4/9/9/9/9/9/9/9/4R4/4K4 w - - 0 1")]
    [InlineData("r3k3r/9/2n6/4p4/9/9/4P4/2N6/9/R3K3R w - - 0 1")]
    public void AlphaBetaMatchesMinimaxAtDepthOneAndTwo(string fen)
    {
        var board = BoardState.FromFen(fen);
        var evaluator = new BoardEvaluator(_rules);
        var minimax = new MinimaxSearch(_rules, evaluator);
        for (var depth = 1; depth <= 2; depth++)
        {
            var expected = minimax.Search(board, depth);
            var actual = new AlphaBetaSearch(_rules, evaluator, new MoveOrdering(), new ZobristHasher())
                .Search(board, new SearchOptions(depth, 5_000, 500_000, UseMoveOrdering: false, UseTranspositionTable: false, UseQuiescence: false));
            Assert.Equal(expected.Score, actual.Score);
            Assert.NotNull(actual.BestMove);
            Assert.Contains(actual.BestMove, _rules.GenerateLegalMovesForSide(board, board.SideToMove));
            Assert.Equal(depth, actual.Statistics.CompletedDepth);
        }
    }

    [Fact]
    public void SearchReturnsLegalFallbackWhenNodeBudgetEndsBeforeDepthOne()
    {
        var board = BoardState.CreateInitialBoard();
        var legal = _rules.GenerateLegalMovesForSide(board, board.SideToMove);
        var result = new AlphaBetaSearch(_rules, new BoardEvaluator(_rules), new MoveOrdering(), new ZobristHasher())
            .Search(board, new SearchOptions(6, 5_000, 1));
        Assert.Equal(0, result.Statistics.CompletedDepth);
        Assert.True(result.UsedFallback);
        Assert.Contains(result.BestMove, legal);
    }

    [Fact]
    public void SearchHonorsCancellation()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var search = new AlphaBetaSearch(_rules, new BoardEvaluator(_rules), new MoveOrdering(), new ZobristHasher());
        Assert.Throws<OperationCanceledException>(() => search.Search(BoardState.CreateInitialBoard(), SearchOptions.For(AiDifficulty.Hard), source.Token));
    }

    [Fact]
    public void QuiescenceSearchFromCheckReturnsALegalEvasionAtItsDepthLimit()
    {
        var board = BoardState.FromFen("4k4/9/9/9/9/9/9/4r4/9/4K4 w - - 0 1");
        Assert.True(_rules.IsInCheck(board, PieceColor.Red));
        var result = new AlphaBetaSearch(_rules, new BoardEvaluator(_rules), new MoveOrdering(), new ZobristHasher())
            .Search(board, new SearchOptions(1, 2_000, 20_000, QuiescenceDepth: 0));
        Assert.Contains(result.BestMove, _rules.GenerateLegalMovesForSide(board, PieceColor.Red));
        Assert.False(_rules.IsInCheck(_rules.ApplyMove(board, result.BestMove), PieceColor.Red));
    }

    [Fact]
    public void SearchTimeBudgetStopsWithALegalMove()
    {
        var board = BoardState.CreateInitialBoard();
        var result = new AlphaBetaSearch(_rules, new BoardEvaluator(_rules), new MoveOrdering(), new ZobristHasher())
            .Search(board, new SearchOptions(6, 1, 500_000));
        Assert.Contains(result.BestMove, _rules.GenerateLegalMovesForSide(board, board.SideToMove));
        Assert.InRange(result.Statistics.CompletedDepth, 0, 6);
    }

    [Fact]
    public void ZobristIncludesSideToMove()
    {
        var board = BoardState.CreateInitialBoard();
        var hasher = new ZobristHasher();
        var red = hasher.Hash(board);
        board.SideToMove = PieceColor.Black;
        Assert.NotEqual(red, hasher.Hash(board));
    }

    [Fact]
    public void EvaluationIsColorSymmetricAndUsesRequestedMaterialValues()
    {
        var evaluator = new BoardEvaluator(_rules);
        var red = BoardState.FromFen("4k4/9/4P4/9/9/9/9/9/9/4K4 w - - 0 1");
        var black = BoardState.FromFen("4k4/9/9/9/9/9/9/4p4/9/4K4 b - - 0 1");
        Assert.Equal(evaluator.Evaluate(red), evaluator.Evaluate(black));
        Assert.Equal(900, BoardEvaluator.MaterialValue(PieceType.Chariot));
        Assert.Equal(450, BoardEvaluator.MaterialValue(PieceType.Cannon));
        Assert.Equal(400, BoardEvaluator.MaterialValue(PieceType.Horse));
    }
}

public sealed class AiPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void BenchmarkRepresentativePositionsAndSearchVariants()
    {
        var rules = new XiangqiRules();
        var positions = new Dictionary<string, string>
        {
            ["initial"] = "rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w - - 0 1",
            ["middlegame"] = "r3k3r/9/2n6/4p4/9/9/4P4/2N6/9/R3K3R w - - 0 1",
            ["endgame"] = "4k4/9/9/9/9/9/9/9/4R4/4K4 w - - 0 1",
            ["in-check"] = "4k4/9/9/9/9/9/9/4r4/9/4K4 w - - 0 1",
            ["captures"] = "4k4/9/9/9/4p4/4R4/9/9/9/4K4 w - - 0 1"
        };
        foreach (var (name, fen) in positions)
        {
            var board = BoardState.FromFen(fen);
            foreach (var variant in new[] { "minimax", "alpha-beta", "ordering", "transposition" })
            {
                var evaluator = new BoardEvaluator(rules);
                var timer = Stopwatch.StartNew();
                Move best; long nodes; long cutoffs = 0; long ttHits = 0;
                if (variant == "minimax")
                {
                    var result = new MinimaxSearch(rules, evaluator).Search(board, 2);
                    best = result.BestMove!; nodes = result.Nodes;
                }
                else
                {
                    var search = new AlphaBetaSearch(rules, evaluator, new MoveOrdering(), new ZobristHasher());
                    var result = search.Search(board, new SearchOptions(2, 10_000, 1_000_000,
                        UseMoveOrdering: variant is "ordering" or "transposition",
                        UseTranspositionTable: variant == "transposition", UseQuiescence: false));
                    best = result.BestMove; nodes = result.Statistics.NodesSearched;
                    cutoffs = result.Statistics.AlphaBetaCutoffs; ttHits = result.Statistics.TranspositionHits;
                }
                timer.Stop();
                Assert.Contains(best, new LegalMoveGenerator(rules).Generate(board, board.SideToMove));
                var nps = timer.ElapsedMilliseconds == 0 ? nodes * 1000 : nodes * 1000 / timer.ElapsedMilliseconds;
                output.WriteLine($"position={name};variant={variant};depth=2;nodes={nodes};elapsedMs={timer.ElapsedMilliseconds};nodesPerSecond={nps};cutoffs={cutoffs};ttHits={ttHits};workingSetBytes={Process.GetCurrentProcess().WorkingSet64};best={best.From.File},{best.From.Rank}-{best.To.File},{best.To.Rank}");
            }
        }
    }
}
