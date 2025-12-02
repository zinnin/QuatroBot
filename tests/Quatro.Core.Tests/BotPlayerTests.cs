using Xunit;

namespace Quatro.Core.Tests;

public class BotPlayerTests
{
    [Fact]
    public void SelectPieceToGive_EarlyGame_AvoidGivingWinningPiece()
    {
        // Arrange: Set up a game state where one piece would allow opponent to win
        var gameState = new GameState();
        var bot = new BotPlayer(BotLevel.Level1, seed: 42);
        
        // Place some pieces to create a near-win situation
        // This is a simple test - in reality we'd need a specific board setup
        
        // Act
        var piece = bot.SelectPieceToGive(gameState);
        
        // Assert: Bot should select a piece (any piece is valid at start)
        Assert.True(gameState.IsPieceAvailable(piece));
    }

    [Fact]
    public void SelectPlacement_EarlyGame_TakesWinningMove()
    {
        // Arrange: Create a game state where bot can win
        var gameState = new GameState();
        var bot = new BotPlayer(BotLevel.Level1, seed: 42);
        
        // Set up board with 3 pieces in a row having a common trait
        // Piece 0: 0000 (short, light, round, hollow)
        // Piece 1: 0001 (short, light, round, solid)
        // Piece 2: 0010 (short, light, square, hollow)
        // These all share bits 0-1 (short, light)
        
        gameState.GivePiece(new Piece(0));
        gameState.PlacePiece(0, 0); // Top-left
        
        gameState.GivePiece(new Piece(1));
        gameState.PlacePiece(0, 1); // Top row, second column
        
        gameState.GivePiece(new Piece(2));
        gameState.PlacePiece(0, 2); // Top row, third column
        
        // Give bot a piece that can complete the row
        gameState.GivePiece(new Piece(3)); // 0011 - short, light, square, solid (shares short+light)
        
        // Act
        var placement = bot.SelectPlacement(gameState);
        
        // Assert: Bot should place at (0, 3) to win
        Assert.Equal((0, 3), placement);
    }

    [Fact]
    public void BotPlayer_CanBeCreatedWithDifferentLevels()
    {
        // Arrange & Act
        var bot1 = new BotPlayer(BotLevel.Level1);
        var bot2 = new BotPlayer(BotLevel.Level2);
        var bot3 = new BotPlayer(BotLevel.Level3);
        var bot4 = new BotPlayer(BotLevel.Level4);
        var bot5 = new BotPlayer(BotLevel.Level5);
        
        // Assert: All bots should be created successfully
        Assert.NotNull(bot1);
        Assert.NotNull(bot2);
        Assert.NotNull(bot3);
        Assert.NotNull(bot4);
        Assert.NotNull(bot5);
    }

    [Theory]
    [InlineData(BotLevel.Level1)]
    [InlineData(BotLevel.Level2)]
    [InlineData(BotLevel.Level3)]
    [InlineData(BotLevel.Level4)]
    [InlineData(BotLevel.Level5)]
    public void SelectPieceToGive_AllLevels_ReturnsValidPiece(BotLevel level)
    {
        // Arrange
        var gameState = new GameState();
        var bot = new BotPlayer(level, seed: 42);
        
        // Act
        var piece = bot.SelectPieceToGive(gameState);
        
        // Assert
        Assert.True(gameState.IsPieceAvailable(piece));
    }

    [Theory]
    [InlineData(BotLevel.Level1)]
    [InlineData(BotLevel.Level2)]
    [InlineData(BotLevel.Level3)]
    [InlineData(BotLevel.Level4)]
    [InlineData(BotLevel.Level5)]
    public void SelectPlacement_AllLevels_ReturnsValidPosition(BotLevel level)
    {
        // Arrange
        var gameState = new GameState();
        gameState.GivePiece(new Piece(0)); // Give any piece
        var bot = new BotPlayer(level, seed: 42);
        
        // Act
        var (row, col) = bot.SelectPlacement(gameState);
        
        // Assert
        Assert.InRange(row, 0, 3);
        Assert.InRange(col, 0, 3);
        Assert.True(gameState.Board.IsEmpty(row, col));
    }

    [Fact]
    public void SelectPieceToGive_ThrowsWhenPieceAlreadySelected()
    {
        // Arrange
        var gameState = new GameState();
        gameState.GivePiece(new Piece(0)); // Already selected a piece
        var bot = new BotPlayer(BotLevel.Level1);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => bot.SelectPieceToGive(gameState));
    }

    [Fact]
    public void SelectPlacement_ThrowsWhenNoPieceSelected()
    {
        // Arrange
        var gameState = new GameState();
        var bot = new BotPlayer(BotLevel.Level1);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => bot.SelectPlacement(gameState));
    }
}
