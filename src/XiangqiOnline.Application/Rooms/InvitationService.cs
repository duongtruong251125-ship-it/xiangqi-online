using XiangqiOnline.Domain.Enums;
using XiangqiOnline.GameEngine;

namespace XiangqiOnline.Application.Rooms;

public sealed record InvitationInfo(Guid Id, Guid RoomId, string RoomCode, string RoomName, Guid InviterId, string InviterUsername,
    Guid InviteeId, string InviteeUsername, InvitationStatus Status, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RespondedAt);
public enum InvitationOperationStatus { Success, NotFound, NotAllowed, SelfInvite, Duplicate, RoomUnavailable, Full, Expired, AlreadyHandled, Conflict }
public sealed record InvitationOperationResult(InvitationOperationStatus Status, InvitationInfo? Invitation = null, RoomInfo? Room = null);

public interface IInvitationRepository
{
    Task<InvitationOperationResult> CreateAsync(Guid roomId, Guid senderId, string normalizedUsername, DateTime now, DateTime expiresAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvitationInfo>> GetIncomingAsync(Guid userId, DateTime now, CancellationToken cancellationToken);
    Task<InvitationOperationResult> RespondAsync(Guid invitationId, Guid userId, bool accept, string initialFen, DateTime now, CancellationToken cancellationToken);
}

public interface IInvitationService
{
    Task<InvitationOperationResult> CreateAsync(Guid roomId, Guid senderId, string username, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvitationInfo>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken);
    Task<InvitationOperationResult> RespondAsync(Guid invitationId, Guid userId, bool accept, CancellationToken cancellationToken);
}

public sealed class InvitationService(IInvitationRepository repository, TimeProvider timeProvider) : IInvitationService
{
    public Task<InvitationOperationResult> CreateAsync(Guid roomId, Guid senderId, string username, CancellationToken cancellationToken)
    {
        var normalized = username.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 64) return Task.FromResult(new InvitationOperationResult(InvitationOperationStatus.NotFound));
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return repository.CreateAsync(roomId, senderId, normalized, now, now.AddHours(24), cancellationToken);
    }

    public Task<IReadOnlyList<InvitationInfo>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken) =>
        repository.GetIncomingAsync(userId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    public Task<InvitationOperationResult> RespondAsync(Guid invitationId, Guid userId, bool accept, CancellationToken cancellationToken) =>
        repository.RespondAsync(invitationId, userId, accept, BoardState.CreateInitialBoard().ToFen(), timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}
