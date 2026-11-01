using XiangqiOnline.GameEngine.AI.Evaluation;
using XiangqiOnline.GameEngine.AI.Models;

namespace XiangqiOnline.GameEngine.AI.Search;

public sealed class IterativeDeepeningSearch(IXiangqiRules rules)
{
    private readonly AlphaBetaSearch _search = new(rules, new BoardEvaluator(rules), new MoveOrdering(rules), new ZobristHasher());
    public SearchResult Search(BoardState board, SearchOptions options, CancellationToken cancellationToken = default) =>
        _search.Search(board, options, cancellationToken);
}
