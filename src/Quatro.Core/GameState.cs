namespace Quatro.Core;

/// <summary>
/// Represents the complete state of a Quatro game.
/// Designed for efficient serialization to support many simulations.
/// </summary>
public class GameState
{
    /// <summary>
    /// The game board.
    /// </summary>
    public Board Board { get; }

    /// <summary>
    /// Bitfield of available pieces (bit i is set if piece i is available).
    /// </summary>
    public ushort AvailablePieces { get; private set; }

    /// <summary>
    /// The piece that must be played by the current player (or null if no piece has been given).
    /// </summary>
    public Piece? PieceToPlay { get; private set; }

    /// <summary>
    /// True if it's player 1's turn, false if player 2's turn.
    /// </summary>
    public bool IsPlayer1Turn { get; private set; }

    /// <summary>
    /// The winner of the game (1 or 2), or 0 if no winner yet.
    /// </summary>
    public int Winner { get; private set; }

    /// <summary>
    /// True if the game is over (either someone won or it's a draw).
    /// </summary>
    public bool IsGameOver => Winner != 0 || (Board.PieceCount == 16);

    /// <summary>
    /// True if the game ended in a draw.
    /// </summary>
    public bool IsDraw => Winner == 0 && Board.PieceCount == 16;

    public GameState()
    {
        Board = new Board();
        AvailablePieces = 0xFFFF; // All 16 pieces available
        IsPlayer1Turn = true;
        Winner = 0;
    }

    /// <summary>
    /// Creates a game state from a compact representation.
    /// </summary>
    public GameState(byte[] boardData, ushort availablePieces, byte? pieceToPlay, bool isPlayer1Turn, int winner)
    {
        Board = new Board(boardData);
        AvailablePieces = availablePieces;
        PieceToPlay = pieceToPlay.HasValue ? new Piece(pieceToPlay.Value) : null;
        IsPlayer1Turn = isPlayer1Turn;
        Winner = winner;
    }

    /// <summary>
    /// Checks if a piece is available.
    /// </summary>
    public bool IsPieceAvailable(Piece piece) => (AvailablePieces & (1 << piece.Value)) != 0;

    /// <summary>
    /// Gets all available pieces.
    /// </summary>
    public IEnumerable<Piece> GetAvailablePieces()
    {
        for (byte i = 0; i < 16; i++)
        {
            if ((AvailablePieces & (1 << i)) != 0)
                yield return new Piece(i);
        }
    }

    /// <summary>
    /// Gives a piece to the opponent to play.
    /// </summary>
    public bool GivePiece(Piece piece)
    {
        if (!IsPieceAvailable(piece))
            return false;
        if (PieceToPlay.HasValue)
            return false;
        if (IsGameOver)
            return false;

        PieceToPlay = piece;
        AvailablePieces &= (ushort)~(1 << piece.Value);
        // Switch turn to opponent who will place the piece
        IsPlayer1Turn = !IsPlayer1Turn;
        return true;
    }

    /// <summary>
    /// Places the given piece on the board.
    /// </summary>
    public bool PlacePiece(int row, int col)
    {
        if (!PieceToPlay.HasValue)
            return false;
        if (IsGameOver)
            return false;

        if (!Board.TryPlacePiece(row, col, PieceToPlay.Value))
            return false;

        // Check for win
        if (WinChecker.HasWin(Board))
        {
            Winner = IsPlayer1Turn ? 1 : 2;
        }

        PieceToPlay = null;
        // Turn doesn't switch after placing - same player selects next piece
        return true;
    }

    /// <summary>
    /// Serializes the game state to a compact byte array.
    /// Format: 16 bytes board + 2 bytes available pieces + 1 byte piece to play + 1 byte flags
    /// Total: 20 bytes
    /// </summary>
    public byte[] ToBytes()
    {
        var result = new byte[20];
        Array.Copy(Board.ToByteArray(), result, 16);
        result[16] = (byte)(AvailablePieces & 0xFF);
        result[17] = (byte)((AvailablePieces >> 8) & 0xFF);
        result[18] = PieceToPlay?.Value ?? 0xFF;
        result[19] = (byte)(
            (IsPlayer1Turn ? 1 : 0) |
            (Winner << 1));
        return result;
    }

    /// <summary>
    /// Deserializes a game state from a byte array.
    /// </summary>
    public static GameState FromBytes(byte[] data)
    {
        if (data.Length != 20)
            throw new ArgumentException("Data must be exactly 20 bytes", nameof(data));

        var boardData = new byte[16];
        Array.Copy(data, boardData, 16);

        ushort availablePieces = (ushort)(data[16] | (data[17] << 8));
        byte? pieceToPlay = data[18] == 0xFF ? null : data[18];
        bool isPlayer1Turn = (data[19] & 1) != 0;
        int winner = (data[19] >> 1) & 3;

        return new GameState(boardData, availablePieces, pieceToPlay, isPlayer1Turn, winner);
    }

    /// <summary>
    /// Creates a deep copy of this game state.
    /// </summary>
    public GameState Clone() => FromBytes(ToBytes());
}
