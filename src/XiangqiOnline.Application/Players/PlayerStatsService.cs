namespace XiangqiOnline.Application.Players;

public sealed record PlayerStats(int Played, int Wins, int Losses, int Draws);

public interface IPlayerStatsRepository
{
    Task<PlayerStats> GetAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IPlayerStatsService
{
    Task<PlayerStats> GetAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class PlayerStatsService(IPlayerStatsRepository repository) : IPlayerStatsService
{
    public Task<PlayerStats> GetAsync(Guid userId, CancellationToken cancellationToken) => repository.GetAsync(userId, cancellationToken);
}
