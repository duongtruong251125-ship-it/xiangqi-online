using XiangqiOnline.Application.Rooms;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Domain.Enums;

namespace XiangqiOnline.UnitTests;

public sealed class RoomServiceTests
{
    [Fact]
    public async Task CreateTrimsNameAndAssignsOwnerToRed()
    {
        var repository = new FakeRoomRepository();
        var service = new RoomService(repository, TimeProvider.System);
        var ownerId = Guid.NewGuid();

        var room = await service.CreateAsync(ownerId, "  Kỳ đài  ", Visibility.Private, CancellationToken.None);

        Assert.Equal("Kỳ đài", repository.CreatedRoom!.Name);
        Assert.Equal(Visibility.Private, repository.CreatedRoom.Visibility);
        Assert.Equal(ownerId, repository.Owner!.UserId);
        Assert.Equal(Side.Red, repository.Owner.Side);
        Assert.False(repository.Owner.IsReady);
        Assert.Equal(6, room.Code.Length);
        Assert.All(room.Code, character => Assert.Contains(character, "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"));
        Assert.Equal("Kỳ đài", room.Name);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("  ")]
    [InlineData("This name is much longer than the accepted room name limit because it has more than eighty characters")]
    public async Task CreateRejectsInvalidRoomName(string name)
    {
        var service = new RoomService(new FakeRoomRepository(), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Guid.NewGuid(), name, Visibility.Public, CancellationToken.None));
    }

    [Fact]
    public async Task JoinByCodeNormalizesAndRejectsMalformedCodeBeforeRepository()
    {
        var repository = new FakeRoomRepository();
        var service = new RoomService(repository, TimeProvider.System);
        var userId = Guid.NewGuid();

        await service.JoinByCodeAsync(userId, " abcd23 ", CancellationToken.None);
        var invalid = await service.JoinByCodeAsync(userId, "bad!", CancellationToken.None);

        Assert.Equal("ABCD23", repository.JoinCode);
        Assert.Equal(RoomActionStatus.InvalidCode, invalid.Status);
        Assert.Equal(1, repository.JoinCalls);
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        public Room? CreatedRoom { get; private set; }
        public RoomMember? Owner { get; private set; }
        public string? JoinCode { get; private set; }
        public int JoinCalls { get; private set; }

        public Task<bool> CreateAsync(Room room, RoomMember owner, CancellationToken cancellationToken)
        {
            CreatedRoom = room;
            Owner = owner;
            return Task.FromResult(true);
        }

        public Task<RoomActionResult> JoinAsync(string? normalizedCode, Guid? roomId, Guid userId, bool allowPrivateById, DateTime now, CancellationToken cancellationToken)
        {
            JoinCode = normalizedCode;
            JoinCalls++;
            return Task.FromResult(new RoomActionResult(RoomActionStatus.Success));
        }

        public Task<RoomInfo?> GetAsync(Guid roomId, Guid userId, CancellationToken cancellationToken) => Task.FromResult<RoomInfo?>(null);
        public Task<PublicRoomsPage> GetPublicRoomsAsync(string? search, RoomStatus? status, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RoomActionResult> ChangeSideAsync(Guid roomId, Guid userId, Side side, long expectedVersion, DateTime now, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RoomActionResult> SetReadyAsync(Guid roomId, Guid userId, bool isReady, long expectedVersion, DateTime now, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RoomActionResult> LeaveAsync(Guid roomId, Guid userId, DateTime now, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RoomActionResult> StartAsync(Guid roomId, Guid userId, long expectedVersion, string initialFen, DateTime now, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
