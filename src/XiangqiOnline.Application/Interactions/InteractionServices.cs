using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;

namespace XiangqiOnline.Application.Interactions;

public sealed record ChatMessageInfo(Guid Id, Guid GameId, Guid SenderUserId, Guid ClientMessageId,
    string SenderDisplayName, string Content, DateTime CreatedAtUtc);
public sealed record ChatMessagesPageInfo(IReadOnlyList<ChatMessageInfo> Items, Guid? NextCursor, bool HasMore);
public enum SendChatStatus { Sent, Duplicate, NotParticipant, Empty, TooLong, IdempotencyConflict, Unavailable }
public sealed record SendChatResult(SendChatStatus Status, ChatMessageInfo? Message = null);
public enum ChatPageStatus { Found, NotParticipant, InvalidCursor }
public sealed record ChatPageResult(ChatPageStatus Status, ChatMessagesPageInfo? Page = null);

public interface IChatRepository
{
    Task<bool> IsParticipantAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<ChatMessageInfo?> GetByClientMessageIdAsync(Guid gameId, Guid senderUserId, Guid clientMessageId, CancellationToken cancellationToken);
    Task<bool> SaveAsync(ChatMessage message, CancellationToken cancellationToken);
    Task<ChatMessagesPageInfo?> GetPageAsync(Guid gameId, Guid? beforeMessageId, int limit, CancellationToken cancellationToken);
}

public interface IChatService
{
    Task<bool> IsParticipantAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<SendChatResult> SendMessageAsync(Guid gameId, Guid userId, Guid clientMessageId, string? content, CancellationToken cancellationToken);
    Task<ChatPageResult> GetMessagesAsync(Guid gameId, Guid userId, Guid? beforeMessageId, int limit, CancellationToken cancellationToken);
}

public sealed class ChatService(IChatRepository repository, TimeProvider timeProvider) : IChatService
{
    public Task<bool> IsParticipantAsync(Guid gameId, Guid userId, CancellationToken cancellationToken) => repository.IsParticipantAsync(gameId, userId, cancellationToken);
    public async Task<SendChatResult> SendMessageAsync(Guid gameId, Guid userId, Guid clientMessageId, string? content, CancellationToken cancellationToken)
    {
        if (gameId == Guid.Empty || userId == Guid.Empty || clientMessageId == Guid.Empty)
            return new(SendChatStatus.Empty);
        if (!await repository.IsParticipantAsync(gameId, userId, cancellationToken)) return new(SendChatStatus.NotParticipant);
        var trimmed = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return new(SendChatStatus.Empty);
        if (trimmed.Length > 1000) return new(SendChatStatus.TooLong);

        var previous = await repository.GetByClientMessageIdAsync(gameId, userId, clientMessageId, cancellationToken);
        if (previous is not null)
            return string.Equals(previous.Content, trimmed, StringComparison.Ordinal)
                ? new(SendChatStatus.Duplicate, previous)
                : new(SendChatStatus.IdempotencyConflict);

        var message = new ChatMessage
        {
            GameId = gameId,
            SenderUserId = userId,
            ClientMessageId = clientMessageId,
            Content = trimmed,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        if (await repository.SaveAsync(message, cancellationToken))
            return new(SendChatStatus.Sent, await repository.GetByClientMessageIdAsync(gameId, userId, clientMessageId, cancellationToken));

        previous = await repository.GetByClientMessageIdAsync(gameId, userId, clientMessageId, cancellationToken);
        if (previous is null) return new(SendChatStatus.Unavailable);
        return string.Equals(previous.Content, trimmed, StringComparison.Ordinal)
            ? new(SendChatStatus.Duplicate, previous)
            : new(SendChatStatus.IdempotencyConflict);
    }

    public async Task<ChatPageResult> GetMessagesAsync(Guid gameId, Guid userId, Guid? beforeMessageId, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 50) return new(ChatPageStatus.InvalidCursor);
        if (!await repository.IsParticipantAsync(gameId, userId, cancellationToken)) return new(ChatPageStatus.NotParticipant);
        var page = await repository.GetPageAsync(gameId, beforeMessageId, limit, cancellationToken);
        return page is null ? new(ChatPageStatus.InvalidCursor) : new(ChatPageStatus.Found, page);
    }
}

public sealed record InteractionGameInfo(Guid Id, GameMode Mode, GameStatus Status, Guid? RedPlayerId,
    Guid? BlackPlayerId, string RedDisplayName, string BlackDisplayName);
public sealed record CallSessionInfo(Guid Id, Guid GameId, Guid CallerUserId, Guid CalleeUserId,
    CallStatus Status, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, DateTime? AcceptedAtUtc,
    DateTime? StartedAtUtc, DateTime? EndedAtUtc, string? EndReason, long Version, string CallerDisplayName, string CalleeDisplayName);
public enum CallOperationStatus { Accepted, NotFound, NotParticipant, WrongRecipient, InvalidState, Busy, Unavailable, Expired, InvalidSignal, Conflict }
public enum CallSignalKind { Offer, Answer, IceCandidate }
public sealed record CallOperationResult(CallOperationStatus Status, CallSessionInfo? Call = null, Guid? OtherPlayerId = null);
public sealed record CallSignalValidation(CallOperationStatus Status, CallSessionInfo? Call = null, Guid? TargetUserId = null);
public enum PersistCallStatus { Saved, Conflict, NotFound, Busy }

public interface ICallSessionRepository
{
    Task<InteractionGameInfo?> GetEligibleGameAsync(Guid gameId, CancellationToken cancellationToken);
    Task<CallSessionInfo?> GetAsync(Guid callId, CancellationToken cancellationToken);
    Task<CallSessionInfo?> GetActiveForParticipantAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<PersistCallStatus> CreateAsync(CallSession session, CancellationToken cancellationToken);
    Task<PersistCallStatus> TransitionAsync(Guid callId, long expectedVersion, CallStatus expectedStatus, CallStatus nextStatus,
        DateTime nowUtc, string? endReason, CancellationToken cancellationToken);
    Task<IReadOnlyList<CallSessionInfo>> GetExpiredRingingAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

public interface ICallSignalingService
{
    Task<CallOperationResult> StartCallAsync(Guid gameId, Guid callerUserId, CancellationToken cancellationToken);
    Task<CallOperationResult> AcceptCallAsync(Guid callId, Guid userId, CancellationToken cancellationToken);
    Task<CallOperationResult> RejectCallAsync(Guid callId, Guid userId, CancellationToken cancellationToken);
    Task<CallOperationResult> EndCallAsync(Guid callId, Guid userId, CancellationToken cancellationToken);
    Task<CallOperationResult> FailCallAsync(Guid callId, string reason, CancellationToken cancellationToken);
    Task<CallSignalValidation> ValidateSignalAsync(Guid callId, Guid userId, CallSignalKind kind, int payloadLength, CancellationToken cancellationToken);
    Task<CallOperationResult> MarkConnectedAsync(Guid callId, Guid userId, CancellationToken cancellationToken);
    Task<CallOperationResult?> GetActiveCallAsync(Guid gameId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CallSessionInfo>> ProcessTimeoutsAsync(CancellationToken cancellationToken);
}

public sealed class CallSignalingService(ICallSessionRepository repository, TimeProvider timeProvider) : ICallSignalingService
{
    private static readonly TimeSpan RingDuration = TimeSpan.FromSeconds(30);

    public async Task<CallOperationResult> StartCallAsync(Guid gameId, Guid callerUserId, CancellationToken cancellationToken)
    {
        var game = await repository.GetEligibleGameAsync(gameId, cancellationToken);
        if (game is null || game.Mode != GameMode.Online || game.Status != GameStatus.InProgress)
            return new(CallOperationStatus.NotFound);
        var calleeId = game.RedPlayerId == callerUserId ? game.BlackPlayerId : game.BlackPlayerId == callerUserId ? game.RedPlayerId : null;
        if (calleeId is null) return new(CallOperationStatus.NotParticipant);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = new CallSession { GameId = gameId, CallerUserId = callerUserId, CalleeUserId = calleeId.Value,
            Status = CallStatus.Ringing, CreatedAtUtc = now, ExpiresAtUtc = now.Add(RingDuration) };
        var saved = await repository.CreateAsync(session, cancellationToken);
        if (saved == PersistCallStatus.Busy) return new(CallOperationStatus.Busy);
        if (saved != PersistCallStatus.Saved) return new(CallOperationStatus.Unavailable);
        return new(CallOperationStatus.Accepted, await repository.GetAsync(session.Id, cancellationToken), calleeId);
    }

    public async Task<CallOperationResult> AcceptCallAsync(Guid callId, Guid userId, CancellationToken cancellationToken)
    {
        var call = await repository.GetAsync(callId, cancellationToken);
        if (call is null) return new(CallOperationStatus.NotFound);
        if (call.CalleeUserId != userId) return new(CallOperationStatus.WrongRecipient, call);
        if (call.Status != CallStatus.Ringing) return new(CallOperationStatus.InvalidState, call);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (call.ExpiresAtUtc <= now)
        {
            var expired = await repository.TransitionAsync(callId, call.Version, CallStatus.Ringing, CallStatus.Missed, now, "no-answer", cancellationToken);
            return new(expired == PersistCallStatus.Saved ? CallOperationStatus.Expired : CallOperationStatus.Conflict,
                await repository.GetAsync(callId, cancellationToken));
        }
        var saved = await repository.TransitionAsync(callId, call.Version, CallStatus.Ringing, CallStatus.Accepted, now, null, cancellationToken);
        return saved == PersistCallStatus.Saved
            ? new(CallOperationStatus.Accepted, await repository.GetAsync(callId, cancellationToken))
            : new(CallOperationStatus.Conflict, await repository.GetAsync(callId, cancellationToken));
    }

    public async Task<CallOperationResult> RejectCallAsync(Guid callId, Guid userId, CancellationToken cancellationToken)
    {
        var call = await repository.GetAsync(callId, cancellationToken);
        if (call is null) return new(CallOperationStatus.NotFound);
        if (call.CalleeUserId != userId) return new(CallOperationStatus.WrongRecipient, call);
        if (call.Status != CallStatus.Ringing) return new(CallOperationStatus.InvalidState, call);
        var saved = await repository.TransitionAsync(callId, call.Version, CallStatus.Ringing, CallStatus.Rejected,
            timeProvider.GetUtcNow().UtcDateTime, "rejected", cancellationToken);
        return saved == PersistCallStatus.Saved
            ? new(CallOperationStatus.Accepted, await repository.GetAsync(callId, cancellationToken))
            : new(CallOperationStatus.Conflict, await repository.GetAsync(callId, cancellationToken));
    }

    public async Task<CallOperationResult> EndCallAsync(Guid callId, Guid userId, CancellationToken cancellationToken)
    {
        var call = await repository.GetAsync(callId, cancellationToken);
        if (call is null) return new(CallOperationStatus.NotFound);
        if (call.CallerUserId != userId && call.CalleeUserId != userId) return new(CallOperationStatus.NotParticipant, call);
        if (!IsActive(call.Status)) return new(CallOperationStatus.InvalidState, call);
        var saved = await repository.TransitionAsync(callId, call.Version, call.Status, CallStatus.Ended,
            timeProvider.GetUtcNow().UtcDateTime, "ended-by-player", cancellationToken);
        return saved == PersistCallStatus.Saved
            ? new(CallOperationStatus.Accepted, await repository.GetAsync(callId, cancellationToken))
            : new(CallOperationStatus.Conflict, await repository.GetAsync(callId, cancellationToken));
    }

    public async Task<CallOperationResult> FailCallAsync(Guid callId, string reason, CancellationToken cancellationToken)
    {
        var call = await repository.GetAsync(callId, cancellationToken);
        if (call is null) return new(CallOperationStatus.NotFound);
        if (!IsActive(call.Status)) return new(CallOperationStatus.InvalidState, call);
        var saved = await repository.TransitionAsync(callId, call.Version, call.Status, CallStatus.Failed,
            timeProvider.GetUtcNow().UtcDateTime, reason, cancellationToken);
        return saved == PersistCallStatus.Saved
            ? new(CallOperationStatus.Accepted, await repository.GetAsync(callId, cancellationToken))
            : new(CallOperationStatus.Conflict, await repository.GetAsync(callId, cancellationToken));
    }

    public async Task<CallSignalValidation> ValidateSignalAsync(Guid callId, Guid userId, CallSignalKind kind,
        int payloadLength, CancellationToken cancellationToken)
    {
        if (payloadLength is <= 0 || payloadLength > (kind == CallSignalKind.IceCandidate ? 8_192 : 65_536))
            return new(CallOperationStatus.InvalidSignal);
        var call = await repository.GetAsync(callId, cancellationToken);
        if (call is null) return new(CallOperationStatus.NotFound);
        if (call.CallerUserId != userId && call.CalleeUserId != userId) return new(CallOperationStatus.NotParticipant, call);
        if (kind == CallSignalKind.Offer)
        {
            if (call.CallerUserId != userId) return new(CallOperationStatus.WrongRecipient, call);
            if (call.Status != CallStatus.Accepted) return new(CallOperationStatus.InvalidState, call);
            var saved = await repository.TransitionAsync(callId, call.Version, CallStatus.Accepted, CallStatus.Connecting,
                timeProvider.GetUtcNow().UtcDateTime, null, cancellationToken);
            return saved == PersistCallStatus.Saved
                ? new(CallOperationStatus.Accepted, await repository.GetAsync(callId, cancellationToken), call.CalleeUserId)
                : new(CallOperationStatus.Conflict, await repository.GetAsync(callId, cancellationToken));
        }
        if (kind == CallSignalKind.IceCandidate && call.Status is CallStatus.Accepted or CallStatus.Connecting or CallStatus.Connected)
            return new(CallOperationStatus.Accepted, call, call.CallerUserId == userId ? call.CalleeUserId : call.CallerUserId);
        if (call.Status is not (CallStatus.Connecting or CallStatus.Connected)) return new(CallOperationStatus.InvalidState, call);
        if (kind == CallSignalKind.Answer && call.CalleeUserId != userId) return new(CallOperationStatus.WrongRecipient, call);
        return new(CallOperationStatus.Accepted, call, call.CallerUserId == userId ? call.CalleeUserId : call.CallerUserId);
    }

    public async Task<CallOperationResult> MarkConnectedAsync(Guid callId, Guid userId, CancellationToken cancellationToken)
    {
        var call = await repository.GetAsync(callId, cancellationToken);
        if (call is null) return new(CallOperationStatus.NotFound);
        if (call.CallerUserId != userId && call.CalleeUserId != userId) return new(CallOperationStatus.NotParticipant, call);
        if (call.Status == CallStatus.Connected) return new(CallOperationStatus.Accepted, call);
        if (call.Status != CallStatus.Connecting) return new(CallOperationStatus.InvalidState, call);
        var saved = await repository.TransitionAsync(callId, call.Version, CallStatus.Connecting, CallStatus.Connected,
            timeProvider.GetUtcNow().UtcDateTime, null, cancellationToken);
        return saved == PersistCallStatus.Saved
            ? new(CallOperationStatus.Accepted, await repository.GetAsync(callId, cancellationToken))
            : new(CallOperationStatus.Conflict, await repository.GetAsync(callId, cancellationToken));
    }

    public async Task<CallOperationResult?> GetActiveCallAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var call = await repository.GetActiveForParticipantAsync(gameId, userId, cancellationToken);
        return call is null ? null : new(CallOperationStatus.Accepted, call);
    }

    public async Task<IReadOnlyList<CallSessionInfo>> ProcessTimeoutsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expired = await repository.GetExpiredRingingAsync(now, cancellationToken);
        var missed = new List<CallSessionInfo>();
        foreach (var call in expired)
        {
            if (await repository.TransitionAsync(call.Id, call.Version, CallStatus.Ringing, CallStatus.Missed, now,
                "no-answer", cancellationToken) == PersistCallStatus.Saved)
            {
                var updated = await repository.GetAsync(call.Id, cancellationToken);
                if (updated is not null) missed.Add(updated);
            }
        }
        return missed;
    }

    private static bool IsActive(CallStatus status) => status is CallStatus.Ringing or CallStatus.Accepted or CallStatus.Connecting or CallStatus.Connected;
}
