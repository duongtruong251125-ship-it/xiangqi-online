namespace XiangqiOnline.Contracts.Interactions;

public sealed record SendChatMessageRequest(Guid GameId, Guid ClientMessageId, string Content);
public sealed record ChatMessageResponse(Guid Id, Guid GameId, Guid SenderUserId, Guid ClientMessageId,
    string SenderDisplayName, string Content, DateTime CreatedAtUtc);
public sealed record ChatMessagesPageResponse(IReadOnlyList<ChatMessageResponse> Items, string? NextCursor, bool HasMore);
public sealed record ChatMessageReceivedEvent(ChatMessageResponse Message);
public sealed record ChatMessageRejectedEvent(Guid GameId, Guid ClientMessageId, string ErrorCode, string Message);
public sealed record CallSessionResponse(Guid Id, Guid GameId, Guid CallerUserId, Guid CalleeUserId,
    string Status, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, DateTime? AcceptedAtUtc);
public sealed record IncomingCallEvent(CallSessionResponse Call, string CallerDisplayName);
public sealed record CallSignalEvent(Guid CallId, Guid GameId, string Payload);
public sealed record CallStateEvent(CallSessionResponse Call, string? Message = null);
