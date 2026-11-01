using XiangqiOnline.Domain.Enums;

namespace XiangqiOnline.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public Visibility Visibility { get; set; }
    public RoomStatus Status { get; set; }
    public int MaxPlayers { get; set; } = 2;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public long Version { get; set; }
}

public sealed class RoomMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public RoomRole Role { get; set; }
    public Side? Side { get; set; }
    public bool IsReady { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
}
