using Quatro.Core;

namespace Quatro.Core.Tests;

public class BoardTests
{
    [Fact]
    public void Board_NewBoard_AllCellsEmpty()
    {
        var board = new Board();
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                Assert.Equal(Board.EmptyCell, board[row, col]);
                Assert.True(board.IsEmpty(row, col));
            }
        }
    }

    [Fact]
    public void Board_PlacePiece_SetsCell()
    {
        var board = new Board();
        var piece = new Piece(5);
        Assert.True(board.TryPlacePiece(0, 0, piece));
        Assert.Equal(5, board[0, 0]);
        Assert.False(board.IsEmpty(0, 0));
    }

    [Fact]
    public void Board_PlacePiece_FailsOnOccupiedCell()
    {
        var board = new Board();
        var piece1 = new Piece(5);
        var piece2 = new Piece(6);
        Assert.True(board.TryPlacePiece(0, 0, piece1));
        Assert.False(board.TryPlacePiece(0, 0, piece2));
    }

    [Fact]
    public void Board_IndexAccess_WorksCorrectly()
    {
        var board = new Board();
        board[1, 2] = 7;
        Assert.Equal(7, board[1, 2]);
        Assert.Equal(7, board[6]); // row * 4 + col = 1 * 4 + 2 = 6
    }

    [Fact]
    public void Board_InvalidPosition_ThrowsException()
    {
        var board = new Board();
        Assert.Throws<ArgumentOutOfRangeException>(() => board[-1, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => board[4, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => board[0, -1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => board[0, 4]);
    }

    [Fact]
    public void Board_GetEmptyCells_ReturnsAllEmptyCells()
    {
        var board = new Board();
        board.TryPlacePiece(0, 0, new Piece(1));
        board.TryPlacePiece(1, 1, new Piece(2));

        var emptyCells = board.GetEmptyCells().ToList();
        Assert.Equal(14, emptyCells.Count);
        Assert.DoesNotContain((0, 0), emptyCells);
        Assert.DoesNotContain((1, 1), emptyCells);
    }

    [Fact]
    public void Board_PieceCount_ReturnsCorrectCount()
    {
        var board = new Board();
        Assert.Equal(0, board.PieceCount);

        board.TryPlacePiece(0, 0, new Piece(1));
        Assert.Equal(1, board.PieceCount);

        board.TryPlacePiece(1, 1, new Piece(2));
        Assert.Equal(2, board.PieceCount);
    }

    [Fact]
    public void Board_ToByteArray_ReturnsCorrectData()
    {
        var board = new Board();
        board[0, 0] = 5;
        board[3, 3] = 10;

        var bytes = board.ToByteArray();
        Assert.Equal(16, bytes.Length);
        Assert.Equal(5, bytes[0]);
        Assert.Equal(10, bytes[15]);
    }

    [Fact]
    public void Board_Clone_CreatesIndependentCopy()
    {
        var board = new Board();
        board[0, 0] = 5;

        var clone = board.Clone();
        clone[0, 0] = 10;

        Assert.Equal(5, board[0, 0]);
        Assert.Equal(10, clone[0, 0]);
    }

    [Fact]
    public void Board_FromByteArray_RestoresBoard()
    {
        var originalBoard = new Board();
        originalBoard[0, 0] = 1;
        originalBoard[1, 1] = 2;
        originalBoard[2, 2] = 3;

        var bytes = originalBoard.ToByteArray();
        var restoredBoard = new Board(bytes);

        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(originalBoard[i], restoredBoard[i]);
        }
    }
}
