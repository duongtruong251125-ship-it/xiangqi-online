using Microsoft.EntityFrameworkCore;
using XiangqiOnline.Application.Players;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.Infrastructure.Players;

public sealed class EfPlayerStatsRepository(AppDbContext db) : IPlayerStatsRepository
{
    public async Task<PlayerStats> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var finished = db.Games.Where(game => game.Status == GameStatus.Finished && game.Mode != GameMode.Local &&
            (game.RedPlayerId == userId || game.BlackPlayerId == userId));
        var played = await finished.CountAsync(cancellationToken);
        var wins = await finished.CountAsync(game => game.WinnerId == userId, cancellationToken);
        var draws = await finished.CountAsync(game => game.ResultReason == ResultReason.Draw, cancellationToken);
        return new PlayerStats(played, wins, played - wins - draws, draws);
    }
}
