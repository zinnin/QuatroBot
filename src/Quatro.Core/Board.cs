namespace Quatro.Core;

/// <summary>
/// Represents the 4x4 game board for Quatro.
/// Uses a compact representation for efficient storage and simulation.
/// </summary>
public class Board
{
    /// <summary>
    /// Represents an empty cell on the board.
    /// </summary>
    public const byte EmptyCell = 0xFF;

    // 16 cells, each storing a piece value (0-15) or EmptyCell (0xFF)
    private readonly byte[] _cells = new byte[16];

    public Board()
    {
        Array.Fill(_cells, EmptyCell);
    }

    /// <summary>
    /// Creates a board from a raw byte array representation.
    /// </summary>
    public Board(byte[] cells)
    {
        if (cells.Length != 16)
            throw new ArgumentException("Board must have exactly 16 cells", nameof(cells));
        Array.Copy(cells, _cells, 16);
    }

    /// <summary>
    /// Gets or sets the piece at the specified position.
    /// </summary>
    public byte this[int row, int col]
    {
        get
        {
            ValidatePosition(row, col);
            return _cells[row * 4 + col];
        }
        set
        {
            ValidatePosition(row, col);
            _cells[row * 4 + col] = value;
        }
    }

    /// <summary>
    /// Gets or sets the piece at the specified index (0-15).
    /// </summary>
    public byte this[int index]
    {
        get
        {
            if (index < 0 || index > 15)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _cells[index];
        }
        set
        {
            if (index < 0 || index > 15)
                throw new ArgumentOutOfRangeException(nameof(index));
            _cells[index] = value;
        }
    }

    /// <summary>
    /// Gets the raw byte array representation of the board.
    /// </summary>
    public byte[] ToByteArray()
    {
        var result = new byte[16];
        Array.Copy(_cells, result, 16);
        return result;
    }

    /// <summary>
    /// Places a piece at the specified position.
    /// </summary>
    public bool TryPlacePiece(int row, int col, Piece piece)
    {
        ValidatePosition(row, col);
        int index = row * 4 + col;
        if (_cells[index] != EmptyCell)
            return false;
        _cells[index] = piece.Value;
        return true;
    }

    /// <summary>
    /// Checks if a cell is empty.
    /// </summary>
    public bool IsEmpty(int row, int col)
    {
        ValidatePosition(row, col);
        return _cells[row * 4 + col] == EmptyCell;
    }

    /// <summary>
    /// Gets all empty cell positions.
    /// </summary>
    public IEnumerable<(int Row, int Col)> GetEmptyCells()
    {
        for (int i = 0; i < 16; i++)
        {
            if (_cells[i] == EmptyCell)
                yield return (i / 4, i % 4);
        }
    }

    /// <summary>
    /// Gets the number of pieces placed on the board.
    /// </summary>
    public int PieceCount => _cells.Count(c => c != EmptyCell);

    /// <summary>
    /// Creates a deep copy of this board.
    /// </summary>
    public Board Clone() => new Board(ToByteArray());

    private static void ValidatePosition(int row, int col)
    {
        if (row < 0 || row > 3)
            throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col > 3)
            throw new ArgumentOutOfRangeException(nameof(col));
    }
}
