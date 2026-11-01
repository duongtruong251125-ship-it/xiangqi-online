namespace XiangqiOnline.GameEngine;

public static class FenSerializer
{
    public static string Serialize(BoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return board.ToFen();
    }

    public static BoardState Deserialize(string fen) => BoardState.FromFen(fen);

    public static bool Validate(string? fen)
    {
        if (string.IsNullOrWhiteSpace(fen)) return false;
        try { return BoardState.FromFen(fen).ValidateBoardStructure(); }
        catch (FormatException) { return false; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}
