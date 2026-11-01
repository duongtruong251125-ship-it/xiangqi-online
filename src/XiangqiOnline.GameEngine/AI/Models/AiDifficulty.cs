namespace XiangqiOnline.GameEngine.AI.Models;

public enum AiDifficulty { Easy, Medium, Hard }

public sealed record SearchOptions(int TargetDepth, int TimeBudgetMs, long NodeBudget,
    bool UseMoveOrdering = true, bool UseTranspositionTable = true, bool UseQuiescence = true,
    bool AllowWeakening = false, int QuiescenceDepth = 6)
{
    public static SearchOptions For(AiDifficulty difficulty) => difficulty switch
    {
        AiDifficulty.Easy => new(2, 300, 12_000, AllowWeakening: true),
        AiDifficulty.Medium => new(4, 1_500, 120_000),
        AiDifficulty.Hard => new(6, 5_000, 500_000),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };
}

public sealed record SearchStatistics(long NodesSearched, long AlphaBetaCutoffs, long TranspositionHits,
    int CompletedDepth, long ElapsedMs, long NodesPerSecond);
public sealed record ScoredMove(Move Move, int Score);
public sealed record SearchResult(Move BestMove, int Score, SearchStatistics Statistics,
    IReadOnlyList<ScoredMove> RootMoves, bool UsedFallback);
