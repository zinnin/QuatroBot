# QuatroBot

A WPF implementation of the Quatro game (also known as Quarto), a 4x4 grid 2-player strategy game.

## Game Rules

Players take turns selecting a piece for their opponent to place on the 4x4 board. Each piece has four binary characteristics:
- **Height**: Tall or Short
- **Color**: Dark or Light
- **Shape**: Round or Square
- **Fill**: Solid or Hollow

The goal is to place four pieces in a row (horizontally, vertically, or diagonally) that share at least one common characteristic.

## Project Structure

- **Quatro.Core** - Class library containing game models and logic
  - `Piece` - Represents a game piece with 4 characteristics (stored as a single byte 0-15)
  - `Board` - 4x4 game board
  - `GameState` - Complete game state with efficient serialization (20 bytes)
  - `WinChecker` - Win detection logic
  - `MoveAnalyzer` - Analyzes moves to count P1 wins, P2 wins, and draws

- **Quatro.Wpf** - WPF application for playing the game
  - Interactive game board and piece selection
  - Optional move analysis panel (View > Show Move Analysis)

- **Quatro.Core.Tests** - Unit tests for the core library

## Efficient Board State

The game state is designed for efficient serialization to support running many simulations:
- Board: 16 bytes (one byte per cell)
- Available pieces: 2 bytes (bitfield)
- Piece to play: 1 byte
- Flags: 1 byte
- **Total: 20 bytes**

## Move Analyzer

The `MoveAnalyzer` class provides analysis of game outcomes:

```csharp
// Analyze all outcomes from starting position
GameOutcomes outcomes = MoveAnalyzer.AnalyzeGame();
Console.WriteLine($"P1 Wins: {outcomes.Player1Wins}");
Console.WriteLine($"P2 Wins: {outcomes.Player2Wins}");
Console.WriteLine($"Draws: {outcomes.Draws}");

// Analyze specific piece selection
var pieceOutcomes = MoveAnalyzer.AnalyzePieceSelection(gameState, new Piece(5));

// Analyze specific placement
var placementOutcomes = MoveAnalyzer.AnalyzePlacement(gameState, row, col);
```

The algorithm uses:
- Compact 64-bit board representation (4 bits per piece)
- Transposition tables for symmetry reduction
- Memoization cache for efficiency
- Optimized move ordering to maximize early pruning

## Move Analysis Panel

Enable the analysis panel from the menu: **View > Show Move Analysis**

When enabled:
- Hover over available pieces to see P1 wins, P2 wins, and draws if that piece is given
- When a piece is selected, hover over empty board positions to see outcomes for that placement

## Requirements

- .NET 8.0 SDK
- Windows (for running the WPF application)

## Building

```bash
dotnet build
```

## Testing

```bash
dotnet test
```
