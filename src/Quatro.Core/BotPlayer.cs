namespace Quatro.Core;

/// <summary>
/// Difficulty level for the bot player.
/// </summary>
public enum BotLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4,
    Level5 = 5
}

/// <summary>
/// Implements bot logic for playing Quatro.
/// </summary>
public class BotPlayer
{
    private readonly BotLevel _level;
    private readonly Random _random;

    public BotPlayer(BotLevel level, int? seed = null)
    {
        _level = level;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Selects a piece to give to the opponent.
    /// </summary>
    public Piece SelectPieceToGive(GameState gameState)
    {
        if (gameState.PieceToPlay.HasValue)
            throw new InvalidOperationException("Cannot select piece when one is already selected");

        var availablePieces = gameState.GetAvailablePieces().ToList();
        if (availablePieces.Count == 0)
            throw new InvalidOperationException("No available pieces");

        // Check if this is early game (<7 pieces played)
        if (gameState.Board.PieceCount < 7)
        {
            return SelectPieceEarlyGame(gameState, availablePieces);
        }
        else
        {
            return SelectPieceLateGame(gameState, availablePieces);
        }
    }

    /// <summary>
    /// Selects a position to place the piece.
    /// </summary>
    public (int row, int col) SelectPlacement(GameState gameState)
    {
        if (!gameState.PieceToPlay.HasValue)
            throw new InvalidOperationException("No piece to place");

        var emptyCells = gameState.Board.GetEmptyCells().ToList();
        if (emptyCells.Count == 0)
            throw new InvalidOperationException("No empty cells");

        // Check if this is early game (<7 pieces played)
        if (gameState.Board.PieceCount < 7)
        {
            return SelectPlacementEarlyGame(gameState, emptyCells);
        }
        else
        {
            return SelectPlacementLateGame(gameState, emptyCells);
        }
    }

    /// <summary>
    /// Early game piece selection: random, but avoid giving winning pieces.
    /// </summary>
    private Piece SelectPieceEarlyGame(GameState gameState, List<Piece> availablePieces)
    {
        var emptyCells = gameState.Board.GetEmptyCells().ToList();
        
        // Find pieces that DON'T allow opponent to win immediately
        var safePieces = new List<Piece>();
        
        foreach (var piece in availablePieces)
        {
            bool allowsWin = false;
            
            // Check if giving this piece allows opponent to win on any empty cell
            foreach (var cell in emptyCells)
            {
                var testState = gameState.Clone();
                testState.GivePiece(piece);
                testState.PlacePiece(cell.Row, cell.Col);
                
                if (testState.IsGameOver && testState.Winner != 0)
                {
                    allowsWin = true;
                    break;
                }
            }
            
            if (!allowsWin)
                safePieces.Add(piece);
        }
        
        // If we have safe pieces, pick one randomly
        if (safePieces.Count > 0)
            return safePieces[_random.Next(safePieces.Count)];
        
        // Otherwise, we must give an unsafe piece (opponent will win)
        return availablePieces[_random.Next(availablePieces.Count)];
    }

    /// <summary>
    /// Early game placement: random, but take winning moves when available.
    /// </summary>
    private (int row, int col) SelectPlacementEarlyGame(GameState gameState, List<(int Row, int Col)> emptyCells)
    {
        // First, check if we can win
        foreach (var cell in emptyCells)
        {
            var testState = gameState.Clone();
            testState.PlacePiece(cell.Row, cell.Col);
            
            if (testState.IsGameOver && testState.Winner != 0)
            {
                // This is a winning move - take it!
                return cell;
            }
        }
        
        // No winning move, pick randomly
        return emptyCells[_random.Next(emptyCells.Count)];
    }

    /// <summary>
    /// Late game piece selection: use MoveAnalyzer and level-based strategy.
    /// </summary>
    private Piece SelectPieceLateGame(GameState gameState, List<Piece> availablePieces)
    {
        // Analyze each piece to get win/loss/draw counts
        var pieceAnalysis = new List<(Piece piece, GameOutcomes outcomes)>();
        
        foreach (var piece in availablePieces)
        {
            var outcomes = MoveAnalyzer.AnalyzePieceSelection(gameState, piece);
            pieceAnalysis.Add((piece, outcomes));
        }
        
        // Select based on bot level
        return SelectPieceByLevel(pieceAnalysis);
    }

    /// <summary>
    /// Late game placement: use MoveAnalyzer and level-based strategy.
    /// </summary>
    private (int row, int col) SelectPlacementLateGame(GameState gameState, List<(int Row, int Col)> emptyCells)
    {
        // First, check if we can win
        foreach (var cell in emptyCells)
        {
            var testState = gameState.Clone();
            testState.PlacePiece(cell.Row, cell.Col);
            
            if (testState.IsGameOver && testState.Winner != 0)
            {
                // This is a winning move - take it!
                return cell;
            }
        }
        
        // Analyze each placement to get win/loss/draw counts
        var placementAnalysis = new List<((int row, int col) cell, GameOutcomes outcomes)>();
        
        foreach (var cell in emptyCells)
        {
            var outcomes = MoveAnalyzer.AnalyzePlacement(gameState, cell.Row, cell.Col);
            placementAnalysis.Add((cell, outcomes));
        }
        
        // Select based on bot level
        return SelectPlacementByLevel(placementAnalysis, gameState);
    }

    /// <summary>
    /// Selects a piece based on the bot level and analysis results.
    /// </summary>
    private Piece SelectPieceByLevel(List<(Piece piece, GameOutcomes outcomes)> analysis)
    {
        // Determine which player we are based on who's turn it is in the game state
        // When selecting a piece, we're the current player, and outcomes are from opponent's perspective
        // So we want to minimize opponent's wins (which are "losses" for us)
        
        List<(Piece piece, long score)> scoredPieces = _level switch
        {
            BotLevel.Level1 => ScorePiecesLevel1(analysis),
            BotLevel.Level2 => ScorePiecesLevel2(analysis),
            BotLevel.Level3 => ScorePiecesLevel3(analysis),
            BotLevel.Level4 => ScorePiecesLevel4(analysis),
            BotLevel.Level5 => ScorePiecesLevel5(analysis),
            _ => throw new ArgumentException($"Invalid bot level: {_level}")
        };
        
        // Find pieces with the best score
        long bestScore = scoredPieces.Max(p => p.score);
        var bestPieces = scoredPieces.Where(p => p.score == bestScore).ToList();
        
        // Pick randomly among best pieces
        return bestPieces[_random.Next(bestPieces.Count)].piece;
    }

    /// <summary>
    /// Selects a placement based on the bot level and analysis results.
    /// </summary>
    private (int row, int col) SelectPlacementByLevel(
        List<((int row, int col) cell, GameOutcomes outcomes)> analysis,
        GameState gameState)
    {
        // When placing a piece, outcomes are from our perspective after placement
        // We want to maximize our wins
        
        List<((int row, int col) cell, long score)> scoredPlacements = _level switch
        {
            BotLevel.Level1 => ScorePlacementsLevel1(analysis, gameState),
            BotLevel.Level2 => ScorePlacementsLevel2(analysis, gameState),
            BotLevel.Level3 => ScorePlacementsLevel3(analysis, gameState),
            BotLevel.Level4 => ScorePlacementsLevel4(analysis, gameState),
            BotLevel.Level5 => ScorePlacementsLevel5(analysis, gameState),
            _ => throw new ArgumentException($"Invalid bot level: {_level}")
        };
        
        // Find placements with the best score
        long bestScore = scoredPlacements.Max(p => p.score);
        var bestPlacements = scoredPlacements.Where(p => p.score == bestScore).ToList();
        
        // Pick randomly among best placements
        return bestPlacements[_random.Next(bestPlacements.Count)].cell;
    }

    // Level 1: Pick options with most losses
    // Interpretation: We want the opponent to have the most losses possible
    private List<(Piece piece, long score)> ScorePiecesLevel1(List<(Piece piece, GameOutcomes outcomes)> analysis)
    {
        // When giving a piece, the outcomes are from the opponent's perspective (who will place it)
        // We want to maximize opponent losses
        return analysis.Select(a => 
        {
            // Total losses = opponent's opponent wins (which is our wins)
            // Calculate total outcomes and opponent losses
            long totalGames = a.outcomes.TotalGames;
            long opponentWins = a.outcomes.Player1Wins + a.outcomes.Player2Wins;
            long opponentLosses = totalGames - opponentWins - a.outcomes.Draws;
            return (a.piece, score: opponentLosses);
        }).ToList();
    }

    private List<((int row, int col) cell, long score)> ScorePlacementsLevel1(
        List<((int row, int col) cell, GameOutcomes outcomes)> analysis, GameState gameState)
    {
        // When placing, we want to maximize opponent losses (= our wins)
        bool isPlayer1 = gameState.IsPlayer1Turn;
        
        return analysis.Select(a => 
        {
            long ourWins = isPlayer1 ? a.outcomes.Player1Wins : a.outcomes.Player2Wins;
            return (a.cell, score: ourWins);
        }).ToList();
    }

    // Level 2: Pick options with most ties + losses
    private List<(Piece piece, long score)> ScorePiecesLevel2(List<(Piece piece, GameOutcomes outcomes)> analysis)
    {
        return analysis.Select(a => 
        {
            long totalGames = a.outcomes.TotalGames;
            long opponentWins = a.outcomes.Player1Wins + a.outcomes.Player2Wins;
            long opponentLosses = totalGames - opponentWins - a.outcomes.Draws;
            long score = a.outcomes.Draws + opponentLosses;
            return (a.piece, score);
        }).ToList();
    }

    private List<((int row, int col) cell, long score)> ScorePlacementsLevel2(
        List<((int row, int col) cell, GameOutcomes outcomes)> analysis, GameState gameState)
    {
        bool isPlayer1 = gameState.IsPlayer1Turn;
        
        return analysis.Select(a => 
        {
            long ourWins = isPlayer1 ? a.outcomes.Player1Wins : a.outcomes.Player2Wins;
            long opponentWins = isPlayer1 ? a.outcomes.Player2Wins : a.outcomes.Player1Wins;
            long ourLosses = opponentWins;
            long score = a.outcomes.Draws + ourLosses;
            return (a.cell, score);
        }).ToList();
    }

    // Level 3: Pick options with most losses (same as Level 1)
    private List<(Piece piece, long score)> ScorePiecesLevel3(List<(Piece piece, GameOutcomes outcomes)> analysis)
    {
        return ScorePiecesLevel1(analysis);
    }

    private List<((int row, int col) cell, long score)> ScorePlacementsLevel3(
        List<((int row, int col) cell, GameOutcomes outcomes)> analysis, GameState gameState)
    {
        return ScorePlacementsLevel1(analysis, gameState);
    }

    // Level 4: Pick options with most losses + wins
    private List<(Piece piece, long score)> ScorePiecesLevel4(List<(Piece piece, GameOutcomes outcomes)> analysis)
    {
        return analysis.Select(a => 
        {
            // Most losses + wins = everything except draws
            long totalGames = a.outcomes.TotalGames;
            long score = totalGames - a.outcomes.Draws;
            return (a.piece, score);
        }).ToList();
    }

    private List<((int row, int col) cell, long score)> ScorePlacementsLevel4(
        List<((int row, int col) cell, GameOutcomes outcomes)> analysis, GameState gameState)
    {
        // Most losses + wins = everything except draws
        return analysis.Select(a => 
        {
            long score = a.outcomes.TotalGames - a.outcomes.Draws;
            return (a.cell, score);
        }).ToList();
    }

    // Level 5: Pick options with least wins where wins > losses
    private List<(Piece piece, long score)> ScorePiecesLevel5(List<(Piece piece, GameOutcomes outcomes)> analysis)
    {
        return analysis.Select(a => 
        {
            long totalGames = a.outcomes.TotalGames;
            long opponentWins = a.outcomes.Player1Wins + a.outcomes.Player2Wins;
            long opponentLosses = totalGames - opponentWins - a.outcomes.Draws;
            
            // Only consider if opponent wins > opponent losses
            if (opponentWins > opponentLosses)
            {
                // Want to pick least wins (so minimize opponent wins)
                return (a.piece, score: -opponentWins);
            }
            else
            {
                // This doesn't meet criteria, very low score
                return (a.piece, score: long.MinValue / 2);
            }
        }).ToList();
    }

    private List<((int row, int col) cell, long score)> ScorePlacementsLevel5(
        List<((int row, int col) cell, GameOutcomes outcomes)> analysis, GameState gameState)
    {
        bool isPlayer1 = gameState.IsPlayer1Turn;
        
        return analysis.Select(a => 
        {
            long ourWins = isPlayer1 ? a.outcomes.Player1Wins : a.outcomes.Player2Wins;
            long ourLosses = isPlayer1 ? a.outcomes.Player2Wins : a.outcomes.Player1Wins;
            
            // Only consider if our wins > our losses
            if (ourWins > ourLosses)
            {
                // Want to pick least wins
                return (a.cell, score: -ourWins);
            }
            else
            {
                // This doesn't meet criteria, very low score
                return (a.cell, score: long.MinValue / 2);
            }
        }).ToList();
    }
}
