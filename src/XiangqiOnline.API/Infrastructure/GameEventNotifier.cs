using Microsoft.AspNetCore.SignalR;
using XiangqiOnline.API.Hubs;
using XiangqiOnline.Application.Games;
using XiangqiOnline.Contracts;

namespace XiangqiOnline.API.Infrastructure;

public interface IGameEventNotifier
{
    Task MoveAppliedAsync(MoveAppliedInfo move, CancellationToken cancellationToken);
    Task GameFinishedAsync(GameFinishedInfo game, CancellationToken cancellationToken);
    Task PlayerConnectionChangedAsync(Guid gameId, Guid playerId, string status, CancellationToken cancellationToken);
    Task ClockSyncedAsync(Guid gameId, long redRemainingMs, long blackRemainingMs, string activeSide, DateTime? turnStartedAtUtc,
        DateTime serverTimeUtc, CancellationToken cancellationToken);
}

public sealed class GameEventNotifier(IHubContext<GameHub> hub, ILogger<GameEventNotifier> logger) : IGameEventNotifier
{
    public Task MoveAppliedAsync(MoveAppliedInfo move, CancellationToken cancellationToken)
    {
        var group = GameHub.GameGroup(move.GameId);
        return SendSafelyAsync(group, "MoveApplied", new MoveAppliedEvent(move.GameId, move.ClientMoveId, move.Ply,
            move.FromSquare, move.ToSquare, move.CapturedPiece, move.FenAfter, move.NextTurn.ToString(), move.Version,
            Clock(move.RedRemainingMs, move.BlackRemainingMs, move.NextTurn, move.TurnStartedAtUtc), move.Status.ToString(), move.IsInCheck,
            move.WinnerId, move.ResultReason?.ToString(), move.ServerTimeUtc), cancellationToken);
    }

    public Task GameFinishedAsync(GameFinishedInfo game, CancellationToken cancellationToken) =>
        SendSafelyAsync(GameHub.GameGroup(game.GameId), "GameFinished", new GameFinishedEvent(game.GameId, game.WinnerId,
            game.ResultReason.ToString(), game.FinalFen, game.FinalVersion, game.EndedAtUtc,
            Clock(game.RedRemainingMs, game.BlackRemainingMs, SideFromFen(game.FinalFen), game.TurnStartedAtUtc), game.ServerTimeUtc), cancellationToken);

    public Task PlayerConnectionChangedAsync(Guid gameId, Guid playerId, string status, CancellationToken cancellationToken) =>
        SendSafelyAsync(GameHub.GameGroup(gameId), "PlayerConnectionChanged", new PlayerConnectionChangedEvent(gameId, playerId, status), cancellationToken);

    public Task ClockSyncedAsync(Guid gameId, long redRemainingMs, long blackRemainingMs, string activeSide, DateTime? turnStartedAtUtc,
        DateTime serverTimeUtc, CancellationToken cancellationToken) =>
        SendSafelyAsync(GameHub.GameGroup(gameId), "ClockSynced", new { gameId, serverTimeUtc, redRemainingMs, blackRemainingMs, activeSide, turnStartedAtUtc }, cancellationToken);

    private async Task SendSafelyAsync(string group, string eventName, object payload, CancellationToken cancellationToken)
    {
        try { await hub.Clients.Group(group).SendAsync(eventName, payload, cancellationToken); }
        catch (Exception exception) { logger.LogWarning(exception, "Game event delivery failed after commit. Event {EventName} for {Group}.", eventName, group); }
    }

    private static GameClockEvent Clock(long redMs, long blackMs, XiangqiOnline.Domain.Enums.Side side, DateTime? turnStarted) =>
        new(redMs, blackMs, side.ToString(), turnStarted);

    private static XiangqiOnline.Domain.Enums.Side SideFromFen(string fen) =>
        fen.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1) == "b"
            ? XiangqiOnline.Domain.Enums.Side.Black : XiangqiOnline.Domain.Enums.Side.Red;
}
