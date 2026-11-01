namespace XiangqiOnline.Contracts;

public sealed record RealtimeEnvelope(Guid? RoomId, Guid? GameId, long Sequence, Guid CorrelationId, string EventType);
public sealed record JoinRoomRequest(Guid RoomId);
public sealed record RoomStateChangedPayload(Guid RoomId, string Status, IReadOnlyList<RoomPlayerPayload> Players, long Sequence);
public sealed record RoomPlayerPayload(Guid UserId, string DisplayName, string? Side, bool IsReady);
public sealed record SubmitMoveRequest(Guid GameId, Guid ClientMoveId, string FromSquare, string ToSquare, long ExpectedGameVersion);
public sealed record GameStateSyncedEvent(Guid GameId, long Version, string Fen, string CurrentTurn, string Status, bool IsInCheck, Guid? RedPlayerId, Guid? BlackPlayerId, string RedPlayerName, string BlackPlayerName, GameClockEvent Clock, IReadOnlyList<XiangqiOnline.Contracts.Games.GameMoveResponse> MoveHistory, Guid? WinnerId, string? ResultReason, DateTime? EndedAtUtc, DateTime ServerTimeUtc);
public sealed record MoveAppliedEvent(Guid GameId, Guid ClientMoveId, int Ply, string FromSquare, string ToSquare, string? CapturedPiece, string FenAfter, string NextTurn, long Version, GameClockEvent Clock, string GameStatus, bool IsInCheck, Guid? WinnerId, string? ResultReason, DateTime ServerTimeUtc);
public sealed record MoveRejectedEvent(Guid GameId, Guid ClientMoveId, string ErrorCode, string Message, long CurrentVersion);
public sealed record GameClockEvent(long RedRemainingMs, long BlackRemainingMs, string ActiveSide, DateTime? TurnStartedAtUtc);
public sealed record PlayerConnectionChangedEvent(Guid GameId, Guid PlayerId, string ConnectionStatus);
public sealed record GameFinishedEvent(Guid GameId, Guid? WinnerId, string ResultReason, string FinalFen, long FinalVersion, DateTime EndedAtUtc, GameClockEvent Clock, DateTime ServerTimeUtc);
public sealed record MoveAppliedPayload(Guid GameId, Guid ClientMoveId, string FromSquare, string ToSquare, string Fen, long GameVersion, long Sequence);
public sealed record MoveRejectedPayload(Guid GameId, Guid ClientMoveId, string ErrorCode, string Message, long CurrentGameVersion);
public sealed record GameStateSyncedPayload(Guid GameId, string Fen, long GameVersion, long Sequence);
public sealed record MessageReceivedPayload(Guid RoomId, Guid MessageId, Guid SenderId, string Content, DateTime CreatedAtUtc, long Sequence);
public sealed record CallSignalPayload(Guid RoomId, string CallId, string SdpOrCandidate);
