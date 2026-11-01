using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Games;
using XiangqiOnline.Contracts;
using XiangqiOnline.Application.Interactions;
using XiangqiOnline.Contracts.Interactions;
using XiangqiOnline.API.Controllers;

namespace XiangqiOnline.API.Hubs;

[Authorize]
public sealed class GameHub(IGameplayService games, IGameEventNotifier events, GamePresence presence,
    IChatService chat, ICallSignalingService calls, GameCallEndpoints callEndpoints) : Hub
{
    public static string GameGroup(Guid gameId) => $"game:{gameId:N}";

    public async Task JoinInteraction(Guid gameId)
    {
        if (!await chat.IsParticipantAsync(gameId, CurrentUserId(), Context.ConnectionAborted))
            throw new HubException("GAME_NOT_FOUND: Bạn không có quyền vào tương tác của ván này.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroup(gameId), Context.ConnectionAborted);
        callEndpoints.Select(gameId, CurrentUserId(), Context.ConnectionId);
        var active = await calls.GetActiveCallAsync(gameId, CurrentUserId(), Context.ConnectionAborted);
        if (active?.Call is { } call)
            await Clients.Caller.SendAsync("CallStateChanged", new CallStateEvent(ToCallResponse(call)), Context.ConnectionAborted);
    }

    public async Task<object> SendChatMessage(SendChatMessageRequest request)
    {
        var result = await chat.SendMessageAsync(request.GameId, CurrentUserId(), request.ClientMessageId, request.Content, Context.ConnectionAborted);
        if (result.Message is not null)
        {
            var response = GameMessagesController.ToResponse(result.Message);
            if (result.Status == SendChatStatus.Sent)
                await Clients.Group(GameGroup(request.GameId)).SendAsync("ChatMessageReceived", new ChatMessageReceivedEvent(response), Context.ConnectionAborted);
            return new { accepted = true, duplicate = result.Status == SendChatStatus.Duplicate, message = response };
        }
        var error = result.Status switch
        {
            SendChatStatus.NotParticipant => ("GAME_FORBIDDEN", "Bạn không có quyền gửi tin trong ván này."),
            SendChatStatus.TooLong => ("CHAT_TOO_LONG", "Tin nhắn tối đa 1000 ký tự."),
            SendChatStatus.IdempotencyConflict => ("CHAT_IDEMPOTENCY_CONFLICT", "Mã tin nhắn đã được dùng cho nội dung khác."),
            SendChatStatus.Unavailable => ("CHAT_UNAVAILABLE", "Chưa lưu được tin nhắn. Vui lòng thử lại."),
            _ => ("CHAT_EMPTY", "Hãy nhập nội dung tin nhắn.")
        };
        await Clients.Caller.SendAsync("ChatMessageRejected", new ChatMessageRejectedEvent(request.GameId, request.ClientMessageId, error.Item1, error.Item2), Context.ConnectionAborted);
        return new { accepted = false, errorCode = error.Item1 };
    }

    public async Task<object> StartCall(Guid gameId)
    {
        var userId = CurrentUserId();
        if (!callEndpoints.IsSelected(gameId, userId, Context.ConnectionId)) throw new HubException("CALL_ENDPOINT_NOT_SELECTED: Kết nối này chưa được chọn cho cuộc gọi.");
        var result = await calls.StartCallAsync(gameId, userId, Context.ConnectionAborted);
        if (result.Status != CallOperationStatus.Accepted || result.Call is null || result.OtherPlayerId is null)
            return new { accepted = false, errorCode = CallError(result.Status) };
        if (!callEndpoints.TryBind(gameId, userId, result.OtherPlayerId.Value, result.Call.Id))
        {
            var failed = await calls.FailCallAsync(result.Call.Id, "peer-offline", Context.ConnectionAborted);
            await NotifyCallState(failed.Call, "Người chơi kia đang ngoại tuyến.");
            return new { accepted = false, errorCode = "CALL_UNAVAILABLE" };
        }
        if (!callEndpoints.TryGet(result.Call.Id, out var endpoints)) return new { accepted = false, errorCode = "CALL_UNAVAILABLE" };
        await Clients.Client(endpoints.Callee).SendAsync("IncomingCall", new IncomingCallEvent(ToCallResponse(result.Call), result.Call.CallerDisplayName), Context.ConnectionAborted);
        return new { accepted = true, call = ToCallResponse(result.Call) };
    }

    public async Task<object> AcceptCall(Guid callId) => await ChangeCall(callId, calls.AcceptCallAsync, notify: true);
    public async Task<object> RejectCall(Guid callId) { var result = await ChangeCall(callId, calls.RejectCallAsync, notify: true); callEndpoints.RemoveCall(callId); return result; }
    public async Task<object> EndCall(Guid callId) { var result = await ChangeCall(callId, calls.EndCallAsync, notify: true); callEndpoints.RemoveCall(callId); return result; }

    public async Task<object> SendCallSignal(CallSignalEvent signal, string kind)
    {
        if (!Enum.TryParse<CallSignalKind>(kind, true, out var signalKind) || string.IsNullOrWhiteSpace(signal.Payload))
            return new { accepted = false, errorCode = "CALL_SIGNAL_INVALID" };
        if (!callEndpoints.TryGet(signal.CallId, out var endpoints) || !callEndpoints.IsBoundEndpoint(signal.CallId, CurrentUserId(), Context.ConnectionId))
            return new { accepted = false, errorCode = "CALL_ENDPOINT_NOT_SELECTED" };
        var validation = await calls.ValidateSignalAsync(signal.CallId, CurrentUserId(), signalKind, signal.Payload.Length, Context.ConnectionAborted);
        if (validation.Status != CallOperationStatus.Accepted || validation.Call?.GameId != signal.GameId || validation.TargetUserId is null)
            return new { accepted = false, errorCode = CallError(validation.Status) };
        var target = validation.TargetUserId == endpoints.CallerId ? endpoints.Caller : validation.TargetUserId == endpoints.CalleeId ? endpoints.Callee : null;
        if (target is null || target == Context.ConnectionId) return new { accepted = false, errorCode = "CALL_ENDPOINT_INVALID" };
        await Clients.Client(target).SendAsync("CallSignalReceived", new { callId = signal.CallId, gameId = signal.GameId, kind = signalKind.ToString(), payload = signal.Payload }, Context.ConnectionAborted);
        return new { accepted = true };
    }

    public async Task<object> CallConnected(Guid callId)
    {
        if (!callEndpoints.TryGet(callId, out var endpoints) || !callEndpoints.IsBoundEndpoint(callId, CurrentUserId(), Context.ConnectionId)) return new { accepted = false, errorCode = "CALL_ENDPOINT_INVALID" };
        var result = await calls.MarkConnectedAsync(callId, CurrentUserId(), Context.ConnectionAborted);
        if (result.Status == CallOperationStatus.Accepted) await NotifyCallState(result.Call, null);
        return new { accepted = result.Status == CallOperationStatus.Accepted, errorCode = result.Status == CallOperationStatus.Accepted ? null : CallError(result.Status) };
    }

    public async Task JoinGame(Guid gameId)
    {
        var state = await AuthorizedState(gameId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroup(gameId));
        if (presence.Add(gameId, CurrentUserId(), Context.ConnectionId))
            await events.PlayerConnectionChangedAsync(gameId, CurrentUserId(), "Connected", Context.ConnectionAborted);
        await SendSyncAsync(state);
    }

    public async Task LeaveGame(Guid gameId)
    {
        await AuthorizedState(gameId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GameGroup(gameId), Context.ConnectionAborted);
        if (presence.Remove(gameId, CurrentUserId(), Context.ConnectionId))
            await events.PlayerConnectionChangedAsync(gameId, CurrentUserId(), "Disconnected", Context.ConnectionAborted);
    }

    public async Task RequestGameSync(Guid gameId)
    {
        var state = await AuthorizedState(gameId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroup(gameId));
        if (presence.Add(gameId, CurrentUserId(), Context.ConnectionId))
            await events.PlayerConnectionChangedAsync(gameId, CurrentUserId(), "Connected", Context.ConnectionAborted);
        await SendSyncAsync(state);
    }

    public async Task<object> SubmitMove(SubmitMoveRequest request)
    {
        if (request.GameId == Guid.Empty || request.ClientMoveId == Guid.Empty || request.ExpectedGameVersion < 0
            || !TryParseSquare(request.FromSquare, out var fromFile, out var fromRank)
            || !TryParseSquare(request.ToSquare, out var toFile, out var toRank))
            return await RejectAsync(request.GameId, request.ClientMoveId, "MOVE_INVALID", "Thông tin nước đi chưa hợp lệ.", 0);

        var result = await games.ApplyMoveAsync(request.GameId, CurrentUserId(), fromFile, fromRank, toFile, toRank,
            request.ExpectedGameVersion, request.ClientMoveId, Context.ConnectionAborted);
        if (result.Status == ApplyMoveStatus.Applied && result.Move is not null)
        {
            await events.MoveAppliedAsync(result.Move, Context.ConnectionAborted);
            if (result.Finished is not null) await events.GameFinishedAsync(result.Finished, Context.ConnectionAborted);
            return new { accepted = true, version = result.Move.Version, ply = result.Move.Ply };
        }
        if (result.Status == ApplyMoveStatus.IdempotentRetry)
        {
            await RequestGameSync(request.GameId);
            return new { accepted = true, idempotentRetry = true, version = result.Game?.Version ?? 0 };
        }
        if (result.Status == ApplyMoveStatus.TimedOut && result.Finished is not null)
            await events.GameFinishedAsync(result.Finished, Context.ConnectionAborted);
        var error = result.Status switch
        {
            ApplyMoveStatus.WrongTurn => ("MOVE_WRONG_TURN", "Chưa đến lượt của bạn."),
            ApplyMoveStatus.Finished => ("GAME_FINISHED", "Ván đấu đã kết thúc."),
            ApplyMoveStatus.VersionConflict => ("VERSION_CONFLICT", "Ván đấu đã thay đổi. Đang đồng bộ lại."),
            ApplyMoveStatus.IdempotencyConflict => ("MOVE_IDEMPOTENCY_CONFLICT", "ClientMoveId đã được dùng cho nước khác."),
            ApplyMoveStatus.TimedOut => ("GAME_TIMEOUT", "Đã hết giờ."),
            ApplyMoveStatus.InvalidMove => ("MOVE_INVALID", "Nước đi không hợp lệ theo luật cờ Tướng."),
            ApplyMoveStatus.NotFound => ("GAME_NOT_FOUND", "Không tìm thấy ván đấu hoặc bạn không phải người chơi."),
            _ => ("MOVE_REJECTED", "Nước đi chưa được áp dụng.")
        };
        var version = result.Game?.Version ?? 0;
        await Clients.Caller.SendAsync("MoveRejected", new MoveRejectedEvent(request.GameId, request.ClientMoveId, error.Item1, error.Item2, version), Context.ConnectionAborted);
        if (result.Status == ApplyMoveStatus.VersionConflict) await RequestGameSync(request.GameId);
        return new { accepted = false, errorCode = error.Item1, version };
    }

    public async Task<object> ResignGame(Guid gameId)
    {
        var finished = await games.ResignAsync(gameId, CurrentUserId(), Context.ConnectionAborted);
        if (finished is null)
        {
            var state = await AuthorizedState(gameId);
            return new { accepted = false, errorCode = state.Game.Status == XiangqiOnline.Domain.Enums.GameStatus.InProgress ? "RESIGN_NOT_ALLOWED" : "GAME_FINISHED" };
        }
        await events.GameFinishedAsync(finished, Context.ConnectionAborted);
        return new { accepted = true, version = finished.FinalVersion };
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var callId in callEndpoints.Remove(Context.ConnectionId))
        {
            var result = await calls.FailCallAsync(callId, "signaling-disconnected", CancellationToken.None);
            if (result.Call is { } call)
                await Clients.Group(GameGroup(call.GameId)).SendAsync("CallStateChanged",
                    new CallStateEvent(ToCallResponse(call), "Kết nối cuộc gọi bị gián đoạn."), CancellationToken.None);
        }
        foreach (var (gameId, userId) in presence.RemoveConnection(Context.ConnectionId))
            await events.PlayerConnectionChangedAsync(gameId, userId, "Disconnected", CancellationToken.None);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<object> ChangeCall(Guid callId, Func<Guid, Guid, CancellationToken, Task<CallOperationResult>> operation, bool notify)
    {
        if (!callEndpoints.TryGet(callId, out var endpoints) || !callEndpoints.IsBoundEndpoint(callId, CurrentUserId(), Context.ConnectionId))
            return new { accepted = false, errorCode = "CALL_ENDPOINT_INVALID" };
        var result = await operation(callId, CurrentUserId(), Context.ConnectionAborted);
        if (notify && result.Status == CallOperationStatus.Accepted) await NotifyCallState(result.Call, null);
        return new { accepted = result.Status == CallOperationStatus.Accepted, errorCode = result.Status == CallOperationStatus.Accepted ? null : CallError(result.Status), call = result.Call is null ? null : ToCallResponse(result.Call) };
    }

    private async Task NotifyCallState(CallSessionInfo? call, string? message)
    {
        if (call is null) return;
        var evt = new CallStateEvent(ToCallResponse(call), message);
        if (callEndpoints.TryGet(call.Id, out var endpoints))
        {
            await Clients.Clients(endpoints.Caller, endpoints.Callee).SendAsync("CallStateChanged", evt, Context.ConnectionAborted);
        }
    }

    private static CallSessionResponse ToCallResponse(CallSessionInfo c) => new(c.Id, c.GameId, c.CallerUserId, c.CalleeUserId, c.Status.ToString(), c.CreatedAtUtc, c.ExpiresAtUtc, c.AcceptedAtUtc);
    private static string CallError(CallOperationStatus status) => status switch
    {
        CallOperationStatus.NotParticipant or CallOperationStatus.WrongRecipient => "CALL_FORBIDDEN",
        CallOperationStatus.Busy => "CALL_BUSY",
        CallOperationStatus.InvalidState or CallOperationStatus.Conflict => "CALL_STATE_CONFLICT",
        CallOperationStatus.InvalidSignal => "CALL_SIGNAL_INVALID",
        CallOperationStatus.NotFound => "CALL_NOT_FOUND",
        _ => "CALL_UNAVAILABLE"
    };

    private async Task<GameSyncInfo> AuthorizedState(Guid gameId)
    {
        var userId = CurrentUserId();
        var state = await games.GetSyncAsync(gameId, userId, Context.ConnectionAborted);
        return state ?? throw new HubException("GAME_NOT_FOUND: Bạn không có quyền truy cập ván này.");
    }

    private async Task SendSyncAsync(GameSyncInfo state)
    {
        var game = state.Game;
        var gameEvent = new GameStateSyncedEvent(game.Id, game.Version, game.CurrentFen, game.CurrentTurn.ToString(), game.Status.ToString(), state.IsInCheck,
            game.RedPlayerId, game.BlackPlayerId, state.RedPlayerName, state.BlackPlayerName,
            new GameClockEvent(game.RedRemainingMs, game.BlackRemainingMs, game.CurrentTurn.ToString(), game.TurnStartedAtUtc),
            state.Moves.Select(move => new XiangqiOnline.Contracts.Games.GameMoveResponse(move.Ply, move.FromSquare, move.ToSquare,
                move.Piece, move.CapturedPiece, move.CreatedAt)).ToArray(), game.WinnerId, game.ResultReason?.ToString(), game.EndedAtUtc, state.ServerTimeUtc);
        await Clients.Caller.SendAsync("GameStateSynced", gameEvent, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("ClockSynced", new { gameId = game.Id, version = game.Version, serverTimeUtc = state.ServerTimeUtc,
            redRemainingMs = game.RedRemainingMs, blackRemainingMs = game.BlackRemainingMs, activeSide = game.CurrentTurn.ToString(),
            turnStartedAtUtc = game.TurnStartedAtUtc }, Context.ConnectionAborted);
    }

    private Guid CurrentUserId() => Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
        ? userId : throw new HubException("AUTH_UNAUTHORIZED: Phiên đăng nhập không hợp lệ.");

    private static bool TryParseSquare(string? square, out int file, out int rank)
    {
        file = rank = -1;
        if (square is not { Length: 2 } || square[0] is < 'a' or > 'i' || square[1] is < '0' or > '9') return false;
        file = square[0] - 'a';
        rank = square[1] - '0';
        return true;
    }

    private async Task<object> RejectAsync(Guid gameId, Guid clientMoveId, string code, string message, long version)
    {
        await Clients.Caller.SendAsync("MoveRejected", new MoveRejectedEvent(gameId, clientMoveId, code, message, version), Context.ConnectionAborted);
        return new { accepted = false, errorCode = code, version };
    }
}

public sealed class GamePresence
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid GameId, Guid UserId), HashSet<string>> _connections = [];
    private readonly Dictionary<string, HashSet<(Guid GameId, Guid UserId)>> _gamesByConnection = new(StringComparer.Ordinal);

    public bool Add(Guid gameId, Guid userId, string connectionId)
    {
        lock (_gate)
        {
            var key = (gameId, userId);
            if (!_connections.TryGetValue(key, out var connections)) _connections[key] = connections = new HashSet<string>(StringComparer.Ordinal);
            var wasOffline = connections.Count == 0;
            connections.Add(connectionId);
            if (!_gamesByConnection.TryGetValue(connectionId, out var games)) _gamesByConnection[connectionId] = games = [];
            games.Add(key);
            return wasOffline;
        }
    }

    public bool Remove(Guid gameId, Guid userId, string connectionId)
    {
        lock (_gate)
        {
            var key = (gameId, userId);
            if (!_connections.TryGetValue(key, out var connections)) return false;
            connections.Remove(connectionId);
            RemoveConnectionIndex(connectionId, key);
            if (connections.Count != 0) return false;
            _connections.Remove(key);
            return true;
        }
    }

    public IReadOnlyList<(Guid GameId, Guid UserId)> RemoveConnection(string connectionId)
    {
        lock (_gate)
        {
            if (!_gamesByConnection.Remove(connectionId, out var games)) return [];
            var disconnected = new List<(Guid GameId, Guid UserId)>();
            foreach (var key in games)
            {
                if (!_connections.TryGetValue(key, out var connections)) continue;
                connections.Remove(connectionId);
                if (connections.Count != 0) continue;
                _connections.Remove(key);
                disconnected.Add(key);
            }
            return disconnected;
        }
    }

    private void RemoveConnectionIndex(string connectionId, (Guid GameId, Guid UserId) game)
    {
        if (!_gamesByConnection.TryGetValue(connectionId, out var games)) return;
        games.Remove(game);
        if (games.Count == 0) _gamesByConnection.Remove(connectionId);
    }
}
