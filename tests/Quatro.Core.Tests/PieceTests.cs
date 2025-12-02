using Quatro.Core;

namespace Quatro.Core.Tests;

public class PieceTests
{
    [Fact]
    public void Piece_AllCharacteristicsFalse_HasValue0()
    {
        var piece = new Piece(isTall: false, isDark: false, isRound: false, isSolid: false);
        Assert.Equal(0, piece.Value);
    }

    [Fact]
    public void Piece_AllCharacteristicsTrue_HasValue15()
    {
        var piece = new Piece(isTall: true, isDark: true, isRound: true, isSolid: true);
        Assert.Equal(15, piece.Value);
    }

    [Theory]
    [InlineData(0, false, false, false, false)]
    [InlineData(1, true, false, false, false)]
    [InlineData(2, false, true, false, false)]
    [InlineData(4, false, false, true, false)]
    [InlineData(8, false, false, false, true)]
    [InlineData(15, true, true, true, true)]
    public void Piece_FromValue_HasCorrectCharacteristics(byte value, bool isTall, bool isDark, bool isRound, bool isSolid)
    {
        var piece = new Piece(value);
        Assert.Equal(isTall, piece.IsTall);
        Assert.Equal(isDark, piece.IsDark);
        Assert.Equal(isRound, piece.IsRound);
        Assert.Equal(isSolid, piece.IsSolid);
        Assert.Equal(!isTall, piece.IsShort);
        Assert.Equal(!isDark, piece.IsLight);
        Assert.Equal(!isRound, piece.IsSquare);
        Assert.Equal(!isSolid, piece.IsHollow);
    }

    [Fact]
    public void Piece_InvalidValue_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Piece(16));
    }

    [Fact]
    public void Piece_AllPieces_Returns16UniquePieces()
    {
        var allPieces = Piece.AllPieces.ToList();
        Assert.Equal(16, allPieces.Count);
        Assert.Equal(16, allPieces.Select(p => p.Value).Distinct().Count());
    }

    [Fact]
    public void Piece_SharesCharacteristic_ReturnsTrue_WhenSame()
    {
        var piece1 = new Piece(0b0001); // Tall
        var piece2 = new Piece(0b0011); // Tall, Dark
        Assert.True(piece1.SharesCharacteristic(piece2, 0)); // Both Tall
    }

    [Fact]
    public void Piece_SharesCharacteristic_ReturnsFalse_WhenDifferent()
    {
        var piece1 = new Piece(0b0001); // Tall
        var piece2 = new Piece(0b0010); // Dark (not Tall)
        Assert.False(piece1.SharesCharacteristic(piece2, 0)); // Different height
    }

    [Fact]
    public void Piece_Equality_WorksCorrectly()
    {
        var piece1 = new Piece(5);
        var piece2 = new Piece(5);
        var piece3 = new Piece(6);

        Assert.True(piece1 == piece2);
        Assert.False(piece1 == piece3);
        Assert.True(piece1.Equals(piece2));
        Assert.Equal(piece1.GetHashCode(), piece2.GetHashCode());
    }
}
