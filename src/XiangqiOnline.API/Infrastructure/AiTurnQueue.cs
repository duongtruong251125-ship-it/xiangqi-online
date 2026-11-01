using System.Threading.Channels;
using XiangqiOnline.Application.AiGames;

namespace XiangqiOnline.API.Infrastructure;

public sealed class AiTurnQueue : IAiTurnQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(64)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });
    public bool TryEnqueue(Guid gameId) => _channel.Writer.TryWrite(gameId);
    public bool TryDequeue(out Guid gameId) => _channel.Reader.TryRead(out gameId);
}
