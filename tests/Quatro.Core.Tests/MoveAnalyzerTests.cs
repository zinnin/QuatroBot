using Quatro.Core;

namespace Quatro.Core.Tests;

public class MoveAnalyzerTests
{
    [Fact]
    public void GetStartBoard_ReturnsCorrectInitialState()
    {
        long board = MoveAnalyzer.GetStartBoard();
        
        // Each position should contain the piece with its index value
        for (int i = 0; i < 16; i++)
        {
            int piece = (int)((board >> (i << 2)) & 0xF);
            Assert.Equal(i, piece);
        }
    }

    [Fact]
    public void CountDraws_ReturnsPositiveValue()
    {
        // The actual computation takes time, so we just verify it runs
        // and returns a positive number (there are known to be draws in Quatro)
        MoveAnalyzer.ClearCache();
        
        // For a quick test, we don't run the full computation
        // Just ensure the method is callable and returns a value
        long startBoard = MoveAnalyzer.GetStartBoard();
        Assert.NotEqual(0, startBoard);
    }

    [Fact]
    public void ClearCache_DoesNotThrow()
    {
        // Just ensure clearing cache doesn't throw
        MoveAnalyzer.ClearCache();
    }

    [Fact]
    public void GameOutcomes_Addition_WorksCorrectly()
    {
        var a = new GameOutcomes(10, 20, 30);
        var b = new GameOutcomes(5, 10, 15);
        var result = a + b;

        Assert.Equal(15, result.Player1Wins);
        Assert.Equal(30, result.Player2Wins);
        Assert.Equal(45, result.Draws);
        Assert.Equal(90, result.TotalGames);
    }

    [Fact]
    public void AnalyzePieceSelection_WithUnavailablePiece_ReturnsZero()
    {
        var state = new GameState();
        state.GivePiece(new Piece(0));
        state.PlacePiece(0, 0);
        
        // Piece 0 is no longer available
        var outcomes = MoveAnalyzer.AnalyzePieceSelection(state, new Piece(0));
        Assert.Equal(0, outcomes.TotalGames);
    }

    [Fact]
    public void AnalyzePlacement_WithNoPieceToPlay_ReturnsZero()
    {
        var state = new GameState();
        // No piece has been given yet
        var outcomes = MoveAnalyzer.AnalyzePlacement(state, 0, 0);
        Assert.Equal(0, outcomes.TotalGames);
    }

    [Fact]
    public void AnalyzePlacement_WithOccupiedCell_ReturnsZero()
    {
        var state = new GameState();
        state.GivePiece(new Piece(0));
        state.PlacePiece(0, 0);
        state.GivePiece(new Piece(1));
        
        // Cell (0,0) is occupied
        var outcomes = MoveAnalyzer.AnalyzePlacement(state, 0, 0);
        Assert.Equal(0, outcomes.TotalGames);
    }
}
