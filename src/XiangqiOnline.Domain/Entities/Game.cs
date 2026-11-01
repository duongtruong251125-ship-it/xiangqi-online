using XiangqiOnline.Domain.Enums;

namespace XiangqiOnline.Domain.Entities;

public sealed class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? RoomId { get; set; }
    public Guid? RedPlayerId { get; set; }
    public Guid? BlackPlayerId { get; set; }
    public Guid? HumanPlayerId { get; set; }
    public Side? HumanSide { get; set; }
    public Side? AiSide { get; set; }
    public AiDifficulty? AiDifficulty { get; set; }
    public AiTurnStatus AiTurnStatus { get; set; }
    public DateTime? AiTurnLeaseUntilUtc { get; set; }
    public int AiRetryCount { get; set; }
    public string? AiLastError { get; set; }
    public int AiCompletedDepth { get; set; }
    public long AiElapsedMs { get; set; }
    public long AiNodesSearched { get; set; }
    public Side? WinnerSide { get; set; }
    public GameMode Mode { get; set; }
    public GameStatus Status { get; set; }
    public string CurrentFen { get; set; } = string.Empty;
    public Side CurrentTurn { get; set; } = Side.Red;
    public Guid? WinnerId { get; set; }
    public ResultReason? ResultReason { get; set; }
    public long Version { get; set; }
    public long TimeControlInitialMs { get; set; } = 600_000;
    public long TimeControlIncrementMs { get; set; }
    public long RedRemainingMs { get; set; } = 600_000;
    public long BlackRemainingMs { get; set; } = 600_000;
    public DateTime? TurnStartedAtUtc { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class GameMove
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public int Ply { get; set; }
    public string FromSquare { get; set; } = string.Empty;
    public string ToSquare { get; set; } = string.Empty;
    public string Piece { get; set; } = string.Empty;
    public string? CapturedPiece { get; set; }
    public Guid? PlayerId { get; set; }
    public string? FenBefore { get; set; }
    public string FenAfter { get; set; } = string.Empty;
    public long? ClockAfterMove { get; set; }
    public Guid ClientMoveId { get; set; }
    public bool IsAiMove { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class GameSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public int Ply { get; set; }
    public string Fen { get; set; } = string.Empty;
    public Side CurrentTurn { get; set; }
    public long RedRemainingMs { get; set; }
    public long BlackRemainingMs { get; set; }
    public long Version { get; set; }
    public string Metadata { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
