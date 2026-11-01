using Microsoft.Extensions.DependencyInjection;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Games;

namespace XiangqiOnline.API.Infrastructure;

public sealed class GameTimeoutWorker(IServiceScopeFactory scopeFactory, IGameEventNotifier events, ILogger<GameTimeoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var games = scope.ServiceProvider.GetRequiredService<IGameplayService>();
                foreach (var finished in await games.ProcessTimeoutsAsync(stoppingToken))
                    await events.GameFinishedAsync(finished, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Online game timeout scan failed; it will retry on the next interval."); }

            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
