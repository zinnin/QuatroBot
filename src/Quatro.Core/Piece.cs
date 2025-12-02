namespace Quatro.Core;

/// <summary>
/// Represents a Quatro piece with 4 binary characteristics.
/// Each piece is uniquely identified by a value from 0-15.
/// Bit 0: Tall (1) / Short (0)
/// Bit 1: Dark (1) / Light (0)
/// Bit 2: Round (1) / Square (0)
/// Bit 3: Solid (1) / Hollow (0)
/// </summary>
public readonly struct Piece : IEquatable<Piece>
{
    private readonly byte _value;

    public Piece(byte value)
    {
        if (value > 15)
            throw new ArgumentOutOfRangeException(nameof(value), "Piece value must be between 0 and 15");
        _value = value;
    }

    public Piece(bool isTall, bool isDark, bool isRound, bool isSolid)
    {
        _value = (byte)(
            (isTall ? 1 : 0) |
            (isDark ? 2 : 0) |
            (isRound ? 4 : 0) |
            (isSolid ? 8 : 0));
    }

    /// <summary>
    /// Gets the raw byte value (0-15) representing this piece.
    /// </summary>
    public byte Value => _value;

    public bool IsTall => (_value & 1) != 0;
    public bool IsShort => !IsTall;
    public bool IsDark => (_value & 2) != 0;
    public bool IsLight => !IsDark;
    public bool IsRound => (_value & 4) != 0;
    public bool IsSquare => !IsRound;
    public bool IsSolid => (_value & 8) != 0;
    public bool IsHollow => !IsSolid;

    /// <summary>
    /// Gets whether this piece shares the specified characteristic bit with another piece.
    /// </summary>
    public bool SharesCharacteristic(Piece other, int bitIndex)
    {
        int mask = 1 << bitIndex;
        return (_value & mask) == (other._value & mask);
    }

    public bool Equals(Piece other) => _value == other._value;
    public override bool Equals(object? obj) => obj is Piece other && Equals(other);
    public override int GetHashCode() => _value;
    public static bool operator ==(Piece left, Piece right) => left.Equals(right);
    public static bool operator !=(Piece left, Piece right) => !left.Equals(right);

    public override string ToString()
    {
        var height = IsTall ? "Tall" : "Short";
        var color = IsDark ? "Dark" : "Light";
        var shape = IsRound ? "Round" : "Square";
        var fill = IsSolid ? "Solid" : "Hollow";
        return $"{height}/{color}/{shape}/{fill}";
    }

    /// <summary>
    /// Gets all 16 possible pieces.
    /// </summary>
    public static IEnumerable<Piece> AllPieces
    {
        get
        {
            for (byte i = 0; i < 16; i++)
                yield return new Piece(i);
        }
    }
}
