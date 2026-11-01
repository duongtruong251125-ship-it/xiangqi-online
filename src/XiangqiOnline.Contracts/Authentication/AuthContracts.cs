using System.ComponentModel.DataAnnotations;

namespace XiangqiOnline.Contracts.Authentication;

public sealed record ApiEnvelope<T>(bool Success, T? Data, string RequestId);
public sealed record ApiError(string Code, string Message);
public sealed record ApiErrorEnvelope(bool Success, object? Data, ApiError Error, string RequestId);
public sealed record UserResponse(Guid Id, string Username, string Email, string DisplayName, string? AvatarUrl, DateTime CreatedAt);
public sealed record RegisterResponse(Guid Id, string Username, string DisplayName);
public sealed record AuthSessionResponse(string AccessToken, int ExpiresIn, UserResponse User);
public sealed record CreateRoomRequest(string Name = "Phòng mới", bool IsPrivate = false);
public sealed record JoinRoomRequest(string Code);
public sealed record JoinPublicRoomRequest(Guid RoomId);
public sealed record RoomMemberResponse(Guid UserId, string Username, string DisplayName, string? Side, string Role, bool IsReady);
public sealed record RoomResponse(Guid Id, string Code, string Name, bool IsPrivate, string Status, Guid OwnerId, int MaxPlayers, long Version, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ClosedAt, Guid? GameId, IReadOnlyList<RoomMemberResponse> Members);
public sealed record PublicRoomsResponse(IReadOnlyList<RoomResponse> Items, int Page, int PageSize, int TotalCount);
public sealed record ChangeRoomSideRequest(string Side, long ExpectedVersion);
public sealed record SetRoomReadyRequest(bool IsReady, long ExpectedVersion);
public sealed record StartRoomRequest(long ExpectedVersion);
public sealed record CreateInvitationRequest(string Username);
public sealed record InvitationResponse(Guid Id, Guid RoomId, string RoomCode, string RoomName, Guid InviterId, string InviterUsername, Guid InviteeId, string InviteeUsername, string Status, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RespondedAt);

public sealed class RegisterRequest
{
    [Required, StringLength(64, MinimumLength = 3), RegularExpression("^[\\p{L}\\p{N}._-]+$")]
    public string Username { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;
    [Required, StringLength(256, MinimumLength = 10)]
    public string Password { get; init; } = string.Empty;
    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, StringLength(254)] public string Identifier { get; init; } = string.Empty;
    [Required, StringLength(256)] public string Password { get; init; } = string.Empty;
    public bool RememberMe { get; init; }
}

public sealed class UpdateProfileRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string DisplayName { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required, StringLength(256)] public string CurrentPassword { get; init; } = string.Empty;
    [Required, StringLength(256, MinimumLength = 10)] public string NewPassword { get; init; } = string.Empty;
    [Required, Compare(nameof(NewPassword))] public string ConfirmNewPassword { get; init; } = string.Empty;
}
