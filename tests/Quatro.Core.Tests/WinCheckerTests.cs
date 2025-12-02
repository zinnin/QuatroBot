using Quatro.Core;

namespace Quatro.Core.Tests;

public class WinCheckerTests
{
    [Fact]
    public void WinChecker_EmptyBoard_NoWin()
    {
        var board = new Board();
        Assert.False(WinChecker.HasWin(board));
    }

    [Theory]
    [InlineData(0, 1, 2, 3)]   // First row
    [InlineData(4, 5, 6, 7)]   // Second row
    [InlineData(8, 9, 10, 11)] // Third row
    [InlineData(12, 13, 14, 15)] // Fourth row
    public void WinChecker_FourTallInRow_IsWin(int pos1, int pos2, int pos3, int pos4)
    {
        var board = new Board();
        // Place 4 tall pieces (values with bit 0 set: 1, 3, 5, 7)
        board[pos1] = 1;
        board[pos2] = 3;
        board[pos3] = 5;
        board[pos4] = 7;

        Assert.True(WinChecker.HasWin(board));
    }

    [Theory]
    [InlineData(0, 4, 8, 12)]  // First column
    [InlineData(1, 5, 9, 13)]  // Second column
    [InlineData(2, 6, 10, 14)] // Third column
    [InlineData(3, 7, 11, 15)] // Fourth column
    public void WinChecker_FourDarkInColumn_IsWin(int pos1, int pos2, int pos3, int pos4)
    {
        var board = new Board();
        // Place 4 dark pieces (values with bit 1 set: 2, 3, 6, 7)
        board[pos1] = 2;
        board[pos2] = 3;
        board[pos3] = 6;
        board[pos4] = 7;

        Assert.True(WinChecker.HasWin(board));
    }

    [Fact]
    public void WinChecker_FourRoundInMainDiagonal_IsWin()
    {
        var board = new Board();
        // Place 4 round pieces (values with bit 2 set: 4, 5, 6, 7)
        board[0] = 4;
        board[5] = 5;
        board[10] = 6;
        board[15] = 7;

        Assert.True(WinChecker.HasWin(board));
    }

    [Fact]
    public void WinChecker_FourSolidInAntiDiagonal_IsWin()
    {
        var board = new Board();
        // Place 4 solid pieces (values with bit 3 set: 8, 9, 10, 11)
        board[3] = 8;
        board[6] = 9;
        board[9] = 10;
        board[12] = 11;

        Assert.True(WinChecker.HasWin(board));
    }

    [Fact]
    public void WinChecker_FourShortInRow_IsWin()
    {
        var board = new Board();
        // Place 4 short pieces (values with bit 0 clear: 0, 2, 4, 6)
        board[0] = 0;
        board[1] = 2;
        board[2] = 4;
        board[3] = 6;

        Assert.True(WinChecker.HasWin(board));
    }

    [Fact]
    public void WinChecker_NoSharedCharacteristic_NoWin()
    {
        var board = new Board();
        // Place pieces that don't share any characteristic in a row
        // 0 = Short/Light/Square/Hollow
        // 3 = Tall/Dark/Square/Hollow
        // 4 = Short/Light/Round/Hollow
        // 11 = Tall/Dark/Square/Solid
        board[0] = 0;
        board[1] = 3;
        board[2] = 4;
        board[3] = 11;

        Assert.False(WinChecker.HasWin(board));
    }

    [Fact]
    public void WinChecker_IncompleteRow_NoWin()
    {
        var board = new Board();
        // Only 3 tall pieces in a row
        board[0] = 1;
        board[1] = 3;
        board[2] = 5;
        // Position 3 is empty

        Assert.False(WinChecker.HasWin(board));
    }

    [Fact]
    public void WinChecker_GetWinningLine_ReturnsCorrectLine()
    {
        var board = new Board();
        board[0] = 1;
        board[1] = 3;
        board[2] = 5;
        board[3] = 7;

        var winningLine = WinChecker.GetWinningLine(board);
        Assert.NotNull(winningLine);
        Assert.Equal([0, 1, 2, 3], winningLine);
    }

    [Fact]
    public void WinChecker_AllWinningLines_Returns10Lines()
    {
        Assert.Equal(10, WinChecker.AllWinningLines.Count);
    }
}
