using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;

namespace XiangqiOnline.API.Hubs;

public sealed class GameHubRateLimitFilter(TimeProvider timeProvider, ILogger<GameHubRateLimitFilter> logger) : IHubFilter
{
    private readonly ConcurrentDictionary<(string UserId, string Method), Window> _windows = new();
    private long _calls;

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var method = invocationContext.HubMethodName;
        var (limit, duration) = method switch
        {
            "SubmitMove" => (30, TimeSpan.FromMinutes(1)),
            "JoinGame" => (12, TimeSpan.FromMinutes(1)),
            "RequestGameSync" => (20, TimeSpan.FromMinutes(1)),
            "ResignGame" => (4, TimeSpan.FromMinutes(1)),
            "LeaveGame" => (20, TimeSpan.FromMinutes(1)),
            "SendChatMessage" => (20, TimeSpan.FromMinutes(1)),
            "StartCall" => (3, TimeSpan.FromMinutes(1)),
            "AcceptCall" or "RejectCall" or "EndCall" => (6, TimeSpan.FromMinutes(1)),
            "SendCallSignal" => (120, TimeSpan.FromMinutes(1)),
            _ => (60, TimeSpan.FromMinutes(1))
        };
        var userId = invocationContext.Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "unauthenticated";
        var now = timeProvider.GetUtcNow();
        var key = (userId, method);
        var window = _windows.GetOrAdd(key, _ => new Window(now));
        if (!window.TryAcquire(now, duration, limit))
        {
            logger.LogWarning("Game hub rate limit rejected user {UserId} method {Method}.", userId, method);
            throw new HubException("GAME_RATE_LIMITED: Vui lòng chờ trước khi gửi thêm yêu cầu.");
        }
        if (Interlocked.Increment(ref _calls) % 512 == 0)
        {
            var threshold = now - TimeSpan.FromHours(1);
            foreach (var item in _windows)
                if (item.Value.GetLastUsedAt() < threshold) _windows.TryRemove(item.Key, out _);
        }
        return await next(invocationContext);
    }

    private sealed class Window(DateTimeOffset startedAt)
    {
        private readonly object _gate = new();
        private DateTimeOffset _startedAt = startedAt;
        private int _count;
        private long _lastUsedTicks = startedAt.UtcTicks;

        public DateTimeOffset GetLastUsedAt() => new(Interlocked.Read(ref _lastUsedTicks), TimeSpan.Zero);

        public bool TryAcquire(DateTimeOffset now, TimeSpan duration, int limit)
        {
            lock (_gate)
            {
                Interlocked.Exchange(ref _lastUsedTicks, now.UtcTicks);
                if (now - _startedAt >= duration || now < _startedAt) { _startedAt = now; _count = 0; }
                if (_count >= limit) return false;
                _count++;
                return true;
            }
        }
    }
}
