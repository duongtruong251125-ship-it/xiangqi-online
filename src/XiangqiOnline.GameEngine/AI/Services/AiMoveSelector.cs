using XiangqiOnline.GameEngine.AI.Models;
using XiangqiOnline.GameEngine.AI.Search;

namespace XiangqiOnline.GameEngine.AI.Services;

public sealed class AiMoveSelector(IXiangqiRules rules, Random? random = null)
{
    private readonly IterativeDeepeningSearch _search = new(rules);
    private readonly Random _random = random ?? Random.Shared;

    public SearchResult Select(BoardState board, AiDifficulty difficulty, CancellationToken cancellationToken = default)
    {
        var options = SearchOptions.For(difficulty);
        var result = _search.Search(board, options, cancellationToken);
        if (!options.AllowWeakening || result.RootMoves.Count < 2) return result;
        var best = result.RootMoves.Max(move => move.Score);
        var nearBest = result.RootMoves.Where(move => best - move.Score <= 100).Take(3).ToArray();
        var selected = nearBest[_random.Next(nearBest.Length)];
        return result with { BestMove = selected.Move, Score = selected.Score };
    }
}
