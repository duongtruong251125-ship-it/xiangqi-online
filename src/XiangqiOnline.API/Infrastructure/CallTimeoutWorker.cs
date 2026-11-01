using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using XiangqiOnline.API.Hubs;
using XiangqiOnline.Application.Interactions;
using XiangqiOnline.Contracts.Interactions;

namespace XiangqiOnline.API.Infrastructure;

public sealed class CallTimeoutWorker(IServiceScopeFactory scopeFactory, IHubContext<GameHub> hub, GameCallEndpoints endpoints, ILogger<CallTimeoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var calls = scope.ServiceProvider.GetRequiredService<ICallSignalingService>();
                foreach (var call in await calls.ProcessTimeoutsAsync(stoppingToken))
                {
                    endpoints.RemoveCall(call.Id);
                    var response = new CallSessionResponse(call.Id, call.GameId, call.CallerUserId, call.CalleeUserId,
                        call.Status.ToString(), call.CreatedAtUtc, call.ExpiresAtUtc, call.AcceptedAtUtc);
                    await hub.Clients.Group(GameHub.GameGroup(call.GameId)).SendAsync("CallStateChanged", new CallStateEvent(response, "Không có phản hồi trong 30 giây."), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Call timeout scan failed; it will retry on the next interval."); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
