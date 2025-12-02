namespace Quatro.Core;

/// <summary>
/// Checks for winning conditions in Quatro.
/// A win occurs when 4 pieces in a row, column, or diagonal share any characteristic.
/// </summary>
public static class WinChecker
{
    // All winning lines: rows, columns, and diagonals (10 total)
    private static readonly int[][] WinningLines =
    [
        // Rows
        [0, 1, 2, 3],
        [4, 5, 6, 7],
        [8, 9, 10, 11],
        [12, 13, 14, 15],
        // Columns
        [0, 4, 8, 12],
        [1, 5, 9, 13],
        [2, 6, 10, 14],
        [3, 7, 11, 15],
        // Diagonals
        [0, 5, 10, 15],
        [3, 6, 9, 12]
    ];

    /// <summary>
    /// Checks if the board has a winning line.
    /// </summary>
    public static bool HasWin(Board board)
    {
        return GetWinningLine(board) != null;
    }

    /// <summary>
    /// Gets the winning line indices if there is a win, otherwise null.
    /// </summary>
    public static int[]? GetWinningLine(Board board)
    {
        foreach (var line in WinningLines)
        {
            if (IsWinningLine(board, line))
                return line;
        }
        return null;
    }

    /// <summary>
    /// Gets all winning lines on the board.
    /// </summary>
    public static IEnumerable<int[]> GetAllWinningLines(Board board)
    {
        foreach (var line in WinningLines)
        {
            if (IsWinningLine(board, line))
                yield return line;
        }
    }

    /// <summary>
    /// Checks if a specific line (4 positions) is a winning line.
    /// A line wins if all 4 pieces share at least one characteristic.
    /// </summary>
    public static bool IsWinningLine(Board board, int[] positions)
    {
        // Check if all positions are filled
        byte[] pieces = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            pieces[i] = board[positions[i]];
            if (pieces[i] == Board.EmptyCell)
                return false;
        }

        // Check each of the 4 characteristics (bits 0-3)
        for (int bit = 0; bit < 4; bit++)
        {
            if (SharesCharacteristic(pieces, bit))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if all 4 pieces share the same value for a specific characteristic bit.
    /// </summary>
    private static bool SharesCharacteristic(byte[] pieces, int bitIndex)
    {
        int mask = 1 << bitIndex;
        int firstValue = pieces[0] & mask;
        for (int i = 1; i < 4; i++)
        {
            if ((pieces[i] & mask) != firstValue)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Gets all the winning line definitions.
    /// </summary>
    public static IReadOnlyList<int[]> AllWinningLines => WinningLines;
}
