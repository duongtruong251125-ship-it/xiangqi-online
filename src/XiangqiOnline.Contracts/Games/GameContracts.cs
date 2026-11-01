namespace XiangqiOnline.Contracts.Games;

public sealed record GameResponse(Guid Id, Guid? RoomId, string Mode, string Status, string CurrentFen, string CurrentTurn, string PlayerSide, bool IsInCheck, Guid? WinnerId, string? WinnerSide, string? ResultReason, long Version, int Ply, long RedRemainingMs, long BlackRemainingMs, DateTime? TurnStartedAtUtc, long TimeControlInitialMs, long TimeControlIncrementMs, DateTime ServerTimeUtc);
public sealed record ApplyGameMoveRequest(int FromFile, int FromRank, int ToFile, int ToRank, long ExpectedVersion, Guid ClientMoveId);
public sealed record ApplyLocalGameMoveRequest(Guid GameId, int FromFile, int FromRank, int ToFile, int ToRank, long ExpectedVersion, Guid ClientMoveId);
public sealed record GameMoveResponse(int Ply, string FromSquare, string ToSquare, string Piece, string? CapturedPiece, DateTime CreatedAt);
public sealed record RestartGameRequest(long ExpectedVersion);
public sealed record GameSnapshotResponse(GameResponse Game, string RedPlayerName, string BlackPlayerName, IReadOnlyList<GameMoveResponse> Moves);
public sealed record GameStatusResponse(Guid GameId, string Status, string CurrentTurn, Guid? WinnerId, string? ResultReason, long Version, DateTime? EndedAt, long RedRemainingMs, long BlackRemainingMs, DateTime? TurnStartedAtUtc, DateTime ServerTimeUtc);
