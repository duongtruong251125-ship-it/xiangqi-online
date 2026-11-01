namespace XiangqiOnline.GameEngine.AI.Search;

public sealed class ZobristHasher
{
    private readonly ulong[,,] _pieces = new ulong[2, 7, 90];
    private readonly ulong _side;
    public ZobristHasher(ulong seed = 0xC07A0A6UL)
    {
        var state = seed;
        for (var color = 0; color < 2; color++)
        for (var type = 0; type < 7; type++)
        for (var square = 0; square < 90; square++) _pieces[color, type, square] = Next(ref state);
        _side = Next(ref state);
    }

    public ulong Hash(BoardState board)
    {
        ulong hash = board.SideToMove == PieceColor.Black ? _side : 0;
        for (var square = 0; square < 90; square++)
        {
            var piece = board.Squares[square];
            if (piece is not null) hash ^= _pieces[(int)piece.Color, (int)piece.Type, square];
        }
        return hash;
    }

    private static ulong Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
