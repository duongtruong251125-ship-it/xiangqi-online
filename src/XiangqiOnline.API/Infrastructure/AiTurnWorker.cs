using Microsoft.Extensions.DependencyInjection;
using XiangqiOnline.Application.AiGames;

namespace XiangqiOnline.API.Infrastructure;

public sealed class AiTurnWorker(IServiceScopeFactory scopeFactory, AiTurnQueue queue,
    TimeProvider timeProvider, ILogger<AiTurnWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!queue.TryDequeue(out var gameId))
                {
                    await using var recoveryScope = scopeFactory.CreateAsyncScope();
                    var repository = recoveryScope.ServiceProvider.GetRequiredService<IAiGameRepository>();
                    foreach (var pendingId in await repository.GetPendingIdsAsync(timeProvider.GetUtcNow().UtcDateTime, 64, stoppingToken))
                        queue.TryEnqueue(pendingId);
                    if (!queue.TryDequeue(out gameId)) { await Task.Delay(400, stoppingToken); continue; }
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IAiTurnProcessor>();
                await processor.ProcessAsync(gameId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "AI turn worker encountered an error and will continue processing pending games.");
                try { await Task.Delay(500, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
