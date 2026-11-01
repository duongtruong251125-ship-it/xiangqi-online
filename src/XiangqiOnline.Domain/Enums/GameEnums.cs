namespace XiangqiOnline.Domain.Enums;

public enum Visibility { Public, Private }
public enum RoomStatus { Waiting, InGame, Closed }
public enum RoomRole { Owner, Player, Spectator }
public enum Side { Red, Black }
public enum GameMode { Online, Ai, Local }
public enum GameStatus { Waiting, InProgress, Finished, Abandoned }
public enum ResultReason { Checkmate, Resignation, Draw, Timeout, Abandoned, Stalemate }
public enum InvitationStatus { Pending, Accepted, Declined, Revoked, Expired }
public enum GameEventType { Started, MoveApplied, Resigned, Finished, DrawOffered, DrawAccepted }
public enum CallStatus { Ringing, Accepted, Connecting, Connected, Rejected, Missed, Ended, Failed }
public enum AiDifficulty { Easy, Medium, Hard }
public enum AiTurnStatus { None, WaitingForHuman, Queued, Thinking, Failed }
