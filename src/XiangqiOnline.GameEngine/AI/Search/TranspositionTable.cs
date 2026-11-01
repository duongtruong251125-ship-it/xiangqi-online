namespace XiangqiOnline.GameEngine.AI.Search;

public enum BoundType : byte { Exact, LowerBound, UpperBound }
public readonly record struct TranspositionEntry(ulong Key, int Depth, int Score, BoundType Bound, Move? BestMove, int Generation);

public sealed class TranspositionTable(int capacityPower = 18)
{
    private readonly TranspositionEntry?[] _entries = new TranspositionEntry?[1 << Math.Clamp(capacityPower, 10, 20)];
    private int _generation;
    public int Generation => _generation;
    public void NextGeneration() => _generation++;
    public bool TryGet(ulong key, out TranspositionEntry entry)
    {
        var found = _entries[(int)(key & (uint)(_entries.Length - 1))];
        if (found is { } value && value.Key == key) { entry = value; return true; }
        entry = default; return false;
    }
    public void Store(TranspositionEntry entry)
    {
        var index = (int)(entry.Key & (uint)(_entries.Length - 1));
        var current = _entries[index];
        if (current is null || current.Value.Generation != _generation || entry.Depth >= current.Value.Depth || entry.Bound == BoundType.Exact)
            _entries[index] = entry with { Generation = _generation };
    }
}
