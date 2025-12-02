using Quatro.Core;

namespace Quatro.Core.Tests;

public class GameStateTests
{
    [Fact]
    public void GameState_NewGame_HasAllPiecesAvailable()
    {
        var state = new GameState();
        Assert.Equal(0xFFFF, state.AvailablePieces);
        Assert.Equal(16, state.GetAvailablePieces().Count());
    }

    [Fact]
    public void GameState_NewGame_IsPlayer1Turn()
    {
        var state = new GameState();
        Assert.True(state.IsPlayer1Turn);
    }

    [Fact]
    public void GameState_NewGame_NoPieceToPlay()
    {
        var state = new GameState();
        Assert.Null(state.PieceToPlay);
    }

    [Fact]
    public void GameState_GivePiece_SetsPieceToPlay()
    {
        var state = new GameState();
        var piece = new Piece(5);
        Assert.True(state.GivePiece(piece));
        Assert.Equal(piece, state.PieceToPlay);
        Assert.False(state.IsPieceAvailable(piece));
    }

    [Fact]
    public void GameState_GivePiece_FailsIfPieceAlreadyGiven()
    {
        var state = new GameState();
        Assert.True(state.GivePiece(new Piece(5)));
        Assert.False(state.GivePiece(new Piece(6)));
    }

    [Fact]
    public void GameState_GivePiece_FailsIfPieceNotAvailable()
    {
        var state = new GameState();
        var piece = new Piece(5);
        state.GivePiece(piece);
        state.PlacePiece(0, 0);

        // Piece 5 is no longer available
        Assert.False(state.GivePiece(piece));
    }

    [Fact]
    public void GameState_PlacePiece_PlacesPieceAndSwitchesTurn()
    {
        var state = new GameState();
        var piece = new Piece(5);
        state.GivePiece(piece);
        // After GivePiece, turn switched to P2
        Assert.False(state.IsPlayer1Turn);

        Assert.True(state.PlacePiece(0, 0));
        Assert.Equal(5, state.Board[0, 0]);
        Assert.Null(state.PieceToPlay);
        // After P2 places, turn stays with P2 (to select next piece)
        Assert.False(state.IsPlayer1Turn);
    }

    [Fact]
    public void GameState_PlacePiece_FailsIfNoPieceToPlay()
    {
        var state = new GameState();
        Assert.False(state.PlacePiece(0, 0));
    }

    [Fact]
    public void GameState_PlacePiece_FailsIfCellOccupied()
    {
        var state = new GameState();
        state.GivePiece(new Piece(5));
        state.PlacePiece(0, 0);

        state.GivePiece(new Piece(6));
        Assert.False(state.PlacePiece(0, 0)); // Cell already occupied
    }

    [Fact]
    public void GameState_Win_DetectedWhenPlacing()
    {
        var state = new GameState();

        // Set up a winning condition - 4 tall pieces in first row
        // Tall pieces: 1, 3, 5, 7
        state.GivePiece(new Piece(1));
        state.PlacePiece(0, 0);

        state.GivePiece(new Piece(3));
        state.PlacePiece(0, 1);

        state.GivePiece(new Piece(5));
        state.PlacePiece(0, 2);

        state.GivePiece(new Piece(7));
        state.PlacePiece(0, 3);

        Assert.True(state.IsGameOver);
        Assert.Equal(1, state.Winner); // Player 1 won (with corrected turn logic, P1 placed the last piece)
    }

    [Fact]
    public void GameState_Serialization_RoundTrips()
    {
        var state = new GameState();
        state.GivePiece(new Piece(5));
        state.PlacePiece(0, 0);
        state.GivePiece(new Piece(10));
        state.PlacePiece(1, 1);
        state.GivePiece(new Piece(3));

        var bytes = state.ToBytes();
        var restored = GameState.FromBytes(bytes);

        Assert.Equal(state.Board.ToByteArray(), restored.Board.ToByteArray());
        Assert.Equal(state.AvailablePieces, restored.AvailablePieces);
        Assert.Equal(state.PieceToPlay, restored.PieceToPlay);
        Assert.Equal(state.IsPlayer1Turn, restored.IsPlayer1Turn);
        Assert.Equal(state.Winner, restored.Winner);
    }

    [Fact]
    public void GameState_ToBytes_Is20BytesLong()
    {
        var state = new GameState();
        var bytes = state.ToBytes();
        Assert.Equal(20, bytes.Length);
    }

    [Fact]
    public void GameState_Clone_CreatesIndependentCopy()
    {
        var state = new GameState();
        state.GivePiece(new Piece(5));
        state.PlacePiece(0, 0);

        var clone = state.Clone();
        clone.GivePiece(new Piece(10));
        clone.PlacePiece(1, 1);

        Assert.Equal(1, state.Board.PieceCount);
        Assert.Equal(2, clone.Board.PieceCount);
    }

    [Fact]
    public void GameState_IsDraw_WhenBoardFullAndNoWinner()
    {
        // Create a state where board is full but no winner
        // This is difficult to achieve naturally, so we construct it manually
        var boardData = new byte[16];
        // Fill with pieces that don't create any winning lines
        // Pattern: alternating characteristics to avoid 4 in a row
        var pieces = new byte[] { 0, 15, 3, 12, 5, 10, 6, 9, 0, 15, 3, 12, 5, 10, 6, 9 };
        Array.Copy(pieces, boardData, 16);

        var state = new GameState(boardData, 0, null, true, 0);
        Assert.True(state.IsGameOver);
        Assert.True(state.IsDraw);
        Assert.Equal(0, state.Winner);
    }

    [Fact]
    public void GameState_GetAvailablePieces_UpdatesAfterGiving()
    {
        var state = new GameState();
        Assert.Equal(16, state.GetAvailablePieces().Count());

        state.GivePiece(new Piece(0));
        state.PlacePiece(0, 0);
        Assert.Equal(15, state.GetAvailablePieces().Count());
        Assert.DoesNotContain(new Piece(0), state.GetAvailablePieces());
    }
}
