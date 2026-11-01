using System.Collections.Concurrent;

namespace XiangqiOnline.API.Hubs;

public sealed class GameCallEndpoints
{
    private readonly ConcurrentDictionary<(Guid GameId, Guid UserId), string> _selected = new();
    private readonly ConcurrentDictionary<Guid, (string Caller, string Callee, Guid CallerId, Guid CalleeId)> _calls = new();
    public void Select(Guid gameId, Guid userId, string connectionId) => _selected[(gameId, userId)] = connectionId;
    public IReadOnlyList<Guid> Remove(string connectionId)
    {
        foreach (var entry in _selected.Where(x => x.Value == connectionId)) _selected.TryRemove(entry.Key, out _);
        var calls = _calls.Where(x => x.Value.Caller == connectionId || x.Value.Callee == connectionId).Select(x => x.Key).ToArray();
        foreach (var callId in calls) _calls.TryRemove(callId, out _);
        return calls;
    }
    public bool TryBind(Guid gameId, Guid callerId, Guid calleeId, Guid callId)
    {
        if (!_selected.TryGetValue((gameId, callerId), out var caller) || !_selected.TryGetValue((gameId, calleeId), out var callee)) return false;
        return _calls.TryAdd(callId, (caller, callee, callerId, calleeId));
    }
    public bool TryGet(Guid callId, out (string Caller, string Callee, Guid CallerId, Guid CalleeId) endpoints) => _calls.TryGetValue(callId, out endpoints);
    public bool IsBoundEndpoint(Guid callId, Guid userId, string connectionId) => _calls.TryGetValue(callId, out var endpoints)
        && ((endpoints.CallerId == userId && endpoints.Caller == connectionId) || (endpoints.CalleeId == userId && endpoints.Callee == connectionId));
    public void RemoveCall(Guid callId) => _calls.TryRemove(callId, out _);
    public bool IsSelected(Guid gameId, Guid userId, string connectionId) => _selected.TryGetValue((gameId, userId), out var selected) && selected == connectionId;
}
