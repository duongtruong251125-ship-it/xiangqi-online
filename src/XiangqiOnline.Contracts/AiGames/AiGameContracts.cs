namespace XiangqiOnline.Contracts.AiGames;

public sealed record CreateAiGameRequest(string Difficulty, string HumanSide);
public sealed record AiGameResponse(Guid GameId, string Difficulty, string HumanSide, string AiSide,
    string Fen, string CurrentTurn, string Status, string AiTurnStatus, long Version, string? WinnerSide,
    string? ResultReason, DateTime? EndedAtUtc, string? Error, bool IsCurrentTurnInCheck);
public sealed record SubmitHumanMoveRequest(int FromFile, int FromRank, int ToFile, int ToRank, long ExpectedVersion, Guid ClientMoveId);
public sealed record SubmitHumanMoveResponse(bool Accepted, Guid? MoveId, string Fen, string NextTurn,
    long Version, string AiTurnStatus, string GameStatus, bool Duplicate = false);
public sealed record AiGameMoveResponse(int Ply, string FromSquare, string ToSquare, string Piece,
    string? CapturedPiece, bool IsAiMove, DateTime CreatedAtUtc);
public sealed record AiGameHistoryResponse(AiGameResponse Game, IReadOnlyList<AiGameMoveResponse> Moves);
public sealed record RestartAiGameRequest(long ExpectedVersion);
public sealed record ResignAiGameRequest(long ExpectedVersion);
public sealed record AiAnalysisStatusResponse(Guid GameId, string Status, int CompletedDepth, long ElapsedMs, long NodesSearched, long Version);
