using System.Collections.Concurrent;

namespace Quatro.Core;

/// <summary>
/// Represents the outcome counts for game analysis.
/// </summary>
public readonly struct GameOutcomes
{
    public long Player1Wins { get; init; }
    public long Player2Wins { get; init; }
    public long Draws { get; init; }
    public long TotalGames => Player1Wins + Player2Wins + Draws;

    public GameOutcomes(long p1Wins, long p2Wins, long draws)
    {
        Player1Wins = p1Wins;
        Player2Wins = p2Wins;
        Draws = draws;
    }

    public static GameOutcomes operator +(GameOutcomes a, GameOutcomes b)
    {
        return new GameOutcomes(
            a.Player1Wins + b.Player1Wins,
            a.Player2Wins + b.Player2Wins,
            a.Draws + b.Draws);
    }
}

/// <summary>
/// Result of minimax evaluation - the guaranteed outcome with optimal play.
/// </summary>
public enum MinimaxResult
{
    /// <summary>Unknown or still calculating</summary>
    Unknown = 0,
    /// <summary>Current player can force a win with optimal play</summary>
    Win = 1,
    /// <summary>Current player will lose with optimal play from both sides</summary>
    Lose = -1,
    /// <summary>Game will end in a draw with optimal play from both sides</summary>
    Draw = 2
}

/// <summary>
/// Complete analysis result containing both outcome counts and minimax evaluation.
/// </summary>
public readonly struct AnalysisResult
{
    /// <summary>
    /// Counts of all possible game outcomes (assuming all moves equally likely).
    /// </summary>
    public GameOutcomes Outcomes { get; init; }
    
    /// <summary>
    /// The guaranteed result with optimal play from the current player's perspective.
    /// </summary>
    public MinimaxResult OptimalResult { get; init; }
    
    public AnalysisResult(GameOutcomes outcomes, MinimaxResult optimalResult)
    {
        Outcomes = outcomes;
        OptimalResult = optimalResult;
    }
}

/// <summary>
/// Analyzes moves in Quatro to count wins and draws.
/// Uses transposition tables and symmetry reduction for efficiency.
/// </summary>
public static class MoveAnalyzer
{
    /// <summary>
    /// Order of squares being filled, chosen to maximize the chance of an early win.
    /// </summary>
    private static readonly int[] IndexShuffle = { 0, 5, 10, 15, 14, 13, 12, 9, 1, 6, 3, 2, 7, 11, 4, 8 };

    /// <summary>
    /// Highest depth for using the lookup cache.
    /// </summary>
    private const int MaxLookupIndex = 10;

    /// <summary>
    /// Cache for memoization at each turn depth. Uses ConcurrentDictionary for thread safety.
    /// </summary>
    private static readonly ConcurrentDictionary<long, GameOutcomes>[] Cache;

    /// <summary>
    /// Cache for minimax results at each turn depth.
    /// </summary>
    private static readonly ConcurrentDictionary<long, MinimaxResult>[] MinimaxCache;

    /// <summary>
    /// Cache for rational play analysis. Key is derived from GameState bytes.
    /// </summary>
    private static readonly ConcurrentDictionary<long, GameOutcomes> RationalCache = new();

    /// <summary>
    /// Winning line masks for efficient evaluation.
    /// Each mask covers 4 positions (4 bits each = 16 bits total per line).
    /// </summary>
    private static readonly long[] Masks;

    /// <summary>
    /// Transposition table mapping piece values to their canonical forms under symmetry.
    /// </summary>
    private static readonly int[][] Transpositions;

    /// <summary>
    /// Minimum transposition values for canonical signature computation.
    /// </summary>
    private static readonly int[] MinTranspositionValues;

    /// <summary>
    /// Lists of transpositions that achieve the minimum for each piece.
    /// </summary>
    private static readonly List<int>[] MinTranspositions;

    static MoveAnalyzer()
    {
        // Initialize cache with thread-safe concurrent dictionaries
        Cache = new ConcurrentDictionary<long, GameOutcomes>[MaxLookupIndex];
        MinimaxCache = new ConcurrentDictionary<long, MinimaxResult>[MaxLookupIndex];
        for (int i = 0; i < MaxLookupIndex; i++)
        {
            Cache[i] = new ConcurrentDictionary<long, GameOutcomes>();
            MinimaxCache[i] = new ConcurrentDictionary<long, MinimaxResult>();
        }

        // Initialize masks for winning lines
        // Masks represent positions for each of the 10 winning lines
        Masks = new long[10];
        int[][] winLines =
        [
            [0, 1, 2, 3],     // Row 0
            [4, 5, 6, 7],     // Row 1
            [8, 9, 10, 11],   // Row 2
            [12, 13, 14, 15], // Row 3
            [0, 4, 8, 12],    // Column 0
            [1, 5, 9, 13],    // Column 1
            [2, 6, 10, 14],   // Column 2
            [3, 7, 11, 15],   // Column 3
            [0, 5, 10, 15],   // Main diagonal
            [3, 6, 9, 12]     // Anti-diagonal
        ];

        for (int i = 0; i < 10; i++)
        {
            long mask = 0;
            foreach (int pos in winLines[i])
            {
                mask |= 0xFL << (pos << 2);
            }
            Masks[i] = mask;
        }

        // Initialize transposition tables
        // Generate all 48 transpositions for piece values
        // These represent the symmetries of the piece characteristics
        var transpositionsList = new List<int[]>();
        
        // Generate permutations of the 4 bits (characteristics)
        // There are 4! = 24 permutations
        int[][] bitPermutations = GenerateBitPermutations();
        
        // For each permutation, we can also invert each bit (XOR with 1)
        // But to keep it manageable, we use identity and inverted versions
        foreach (var perm in bitPermutations)
        {
            // Identity (no inversion)
            transpositionsList.Add(GenerateTransposition(perm, [false, false, false, false]));
            // Invert all bits
            transpositionsList.Add(GenerateTransposition(perm, [true, true, true, true]));
        }

        Transpositions = new int[16][];
        for (int piece = 0; piece < 16; piece++)
        {
            Transpositions[piece] = new int[transpositionsList.Count];
            for (int t = 0; t < transpositionsList.Count; t++)
            {
                Transpositions[piece][t] = transpositionsList[t][piece];
            }
        }

        // Compute min transposition values and lists
        MinTranspositionValues = new int[16];
        MinTranspositions = new List<int>[16];

        for (int piece = 0; piece < 16; piece++)
        {
            int minVal = 16;
            var minList = new List<int>();

            for (int t = 0; t < transpositionsList.Count; t++)
            {
                int val = Transpositions[piece][t];
                if (val < minVal)
                {
                    minVal = val;
                    minList.Clear();
                    minList.Add(t);
                }
                else if (val == minVal)
                {
                    minList.Add(t);
                }
            }

            MinTranspositionValues[piece] = minVal;
            MinTranspositions[piece] = minList;
        }
    }

    private static int[][] GenerateBitPermutations()
    {
        // Generate all 24 permutations of 4 elements (bit positions)
        var result = new List<int[]>();
        int[] arr = { 0, 1, 2, 3 };
        GeneratePermutations(arr, 0, result);
        return result.ToArray();
    }

    private static void GeneratePermutations(int[] arr, int start, List<int[]> result)
    {
        if (start == arr.Length)
        {
            result.Add((int[])arr.Clone());
            return;
        }

        for (int i = start; i < arr.Length; i++)
        {
            (arr[start], arr[i]) = (arr[i], arr[start]);
            GeneratePermutations(arr, start + 1, result);
            (arr[start], arr[i]) = (arr[i], arr[start]);
        }
    }

    private static int[] GenerateTransposition(int[] bitPerm, bool[] invert)
    {
        var result = new int[16];
        for (int piece = 0; piece < 16; piece++)
        {
            int newPiece = 0;
            for (int b = 0; b < 4; b++)
            {
                int bit = (piece >> bitPerm[b]) & 1;
                if (invert[b]) bit ^= 1;
                newPiece |= bit << b;
            }
            result[piece] = newPiece;
        }
        return result;
    }

    /// <summary>
    /// Gets the starting board state with pieces 0-15 in positions 0-15.
    /// </summary>
    public static long GetStartBoard()
    {
        long board = 0;
        for (int i = 0; i < 16; i++)
        {
            board |= (long)i << (i << 2);
        }
        return board;
    }

    /// <summary>
    /// Counts the number of possible draw games from the starting position.
    /// </summary>
    public static long CountDraws()
    {
        return AnalyzeGame().Draws;
    }

    /// <summary>
    /// Analyzes all possible game outcomes from the starting position.
    /// </summary>
    public static GameOutcomes AnalyzeGame()
    {
        // Clear cache for fresh computation
        ClearCache();
        return AnalyzeGame(GetStartBoard(), 0);
    }

    /// <summary>
    /// Analyzes all possible game outcomes from a given board state and turn.
    /// Uses parallel processing when there are enough branches to justify overhead.
    /// </summary>
    public static GameOutcomes AnalyzeGame(long board, int turn, CancellationToken cancellationToken = default)
    {
        // Check for cancellation without throwing
        if (cancellationToken.IsCancellationRequested)
            return new GameOutcomes(0, 0, 0);

        long signature = 0;
        if (turn < MaxLookupIndex)
        {
            signature = GetSignature(board, turn);
            if (Cache[turn].TryGetValue(signature, out var cached))
                return cached;
        }

        int indexShuffled = IndexShuffle[turn];
        int branchCount = 16 - turn;
        
        // Use parallel processing when we have enough branches to justify overhead
        // Parallelize when there are 6+ branches available, regardless of turn number
        // This ensures parallelization works even when starting mid-game
        if (branchCount >= 6)
        {
            long p1Wins = 0, p2Wins = 0, draws = 0;
            
            // Use 4x processor count for better throughput on CPU-bound work
            // This allows for more work items to be queued and processed
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 4
            };

            try
            {
                Parallel.For(turn, 16, parallelOptions, (n, loopState) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        loopState.Stop();
                        return;
                    }

                    long newBoard = Swap(board, indexShuffled, IndexShuffle[n]);
                    
                    // Check if this move creates a win
                    if (PartialEvaluate(newBoard, indexShuffled))
                    {
                        bool isPlayer1Placing = (turn % 2) == 1;
                        if (isPlayer1Placing)
                            Interlocked.Add(ref p1Wins, 1);
                        else
                            Interlocked.Add(ref p2Wins, 1);
                        return;
                    }
                    
                    if (turn == 15)
                    {
                        Interlocked.Add(ref draws, 1);
                    }
                    else
                    {
                        var result = AnalyzeGame(newBoard, turn + 1, cancellationToken);
                        Interlocked.Add(ref p1Wins, result.Player1Wins);
                        Interlocked.Add(ref p2Wins, result.Player2Wins);
                        Interlocked.Add(ref draws, result.Draws);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Parallel loop was cancelled, return partial results collected so far
            }

            var outcomes = new GameOutcomes(p1Wins, p2Wins, draws);
            
            // Only cache if not cancelled (to avoid caching partial results)
            if (!cancellationToken.IsCancellationRequested && turn < MaxLookupIndex)
                Cache[turn][signature] = outcomes;
                
            return outcomes;
        }
        else
        {
            // Sequential processing for deeper turns with fewer branches
            var outcomes = new GameOutcomes(0, 0, 0);

            for (int n = turn; n < 16; n++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new GameOutcomes(0, 0, 0);

                long newBoard = Swap(board, indexShuffled, IndexShuffle[n]);
                
                // Check if this move creates a win
                if (PartialEvaluate(newBoard, indexShuffled))
                {
                    bool isPlayer1Placing = (turn % 2) == 1;
                    if (isPlayer1Placing)
                        outcomes = outcomes + new GameOutcomes(1, 0, 0);
                    else
                        outcomes = outcomes + new GameOutcomes(0, 1, 0);
                    continue;
                }
                
                if (turn == 15)
                {
                    outcomes = outcomes + new GameOutcomes(0, 0, 1);
                }
                else
                {
                    outcomes = outcomes + AnalyzeGame(newBoard, turn + 1, cancellationToken);
                }
            }

            if (turn < MaxLookupIndex && !cancellationToken.IsCancellationRequested)
                Cache[turn][signature] = outcomes;

            return outcomes;
        }
    }

    /// <summary>
    /// Analyzes outcomes for selecting a specific piece at the current game state.
    /// This simulates what happens when the current player gives this piece to their opponent.
    /// </summary>
    public static GameOutcomes AnalyzePieceSelection(GameState gameState, Piece piece, CancellationToken cancellationToken = default)
    {
        if (!gameState.IsPieceAvailable(piece))
            return new GameOutcomes(0, 0, 0);
        
        // Check for cancellation without throwing
        if (cancellationToken.IsCancellationRequested)
            return new GameOutcomes(0, 0, 0);
        
        // Clone and give the piece
        var testState = gameState.Clone();
        testState.GivePiece(piece);
        
        return AnalyzeFromGameStateRational(testState, cancellationToken);
    }

    /// <summary>
    /// Analyzes outcomes for placing the current piece at a specific position.
    /// </summary>
    public static GameOutcomes AnalyzePlacement(GameState gameState, int row, int col, CancellationToken cancellationToken = default)
    {
        if (!gameState.PieceToPlay.HasValue)
            return new GameOutcomes(0, 0, 0);
        
        if (!gameState.Board.IsEmpty(row, col))
            return new GameOutcomes(0, 0, 0);
        
        // Check for cancellation without throwing
        if (cancellationToken.IsCancellationRequested)
            return new GameOutcomes(0, 0, 0);
        
        // Clone and place the piece
        var testState = gameState.Clone();
        testState.PlacePiece(row, col);
        
        // Check if this placement wins
        if (testState.IsGameOver && testState.Winner != 0)
        {
            if (testState.Winner == 1)
                return new GameOutcomes(1, 0, 0);
            else
                return new GameOutcomes(0, 1, 0);
        }
        
        return AnalyzeFromGameStateRational(testState, cancellationToken);
    }

    /// <summary>
    /// Analyzes game outcomes from a GameState object using parallel processing.
    /// </summary>
    public static GameOutcomes AnalyzeFromGameState(GameState gameState, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return new GameOutcomes(0, 0, 0);

        if (gameState.IsGameOver)
        {
            if (gameState.Winner == 1)
                return new GameOutcomes(1, 0, 0);
            else if (gameState.Winner == 2)
                return new GameOutcomes(0, 1, 0);
            else
                return new GameOutcomes(0, 0, 1);
        }

        long p1Wins = 0, p2Wins = 0, draws = 0;
        
        // Use 4x processor count for better throughput on CPU-bound work
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount * 4
        };

        try
        {
            if (gameState.PieceToPlay.HasValue)
            {
                // Need to place the piece - try all empty positions in parallel
                var emptyCells = gameState.Board.GetEmptyCells().ToList();
                
                Parallel.ForEach(emptyCells, parallelOptions, (cell, loopState) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        loopState.Stop();
                        return;
                    }
                    var result = AnalyzePlacement(gameState, cell.Row, cell.Col, cancellationToken);
                    Interlocked.Add(ref p1Wins, result.Player1Wins);
                    Interlocked.Add(ref p2Wins, result.Player2Wins);
                    Interlocked.Add(ref draws, result.Draws);
                });
            }
            else
            {
                // Need to select a piece to give - process in parallel
                var availablePieces = gameState.GetAvailablePieces().ToList();
                
                Parallel.ForEach(availablePieces, parallelOptions, (piece, loopState) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        loopState.Stop();
                        return;
                    }
                    var result = AnalyzePieceSelection(gameState, piece, cancellationToken);
                    Interlocked.Add(ref p1Wins, result.Player1Wins);
                    Interlocked.Add(ref p2Wins, result.Player2Wins);
                    Interlocked.Add(ref draws, result.Draws);
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Gracefully handle cancellation
        }
        
        return new GameOutcomes(p1Wins, p2Wins, draws);
    }

    /// <summary>
    /// Analyzes game outcomes assuming rational play - players always take winning moves.
    /// This dramatically reduces the search space by pruning branches where a player
    /// has a winning move but doesn't take it.
    /// </summary>
    public static GameOutcomes AnalyzeFromGameStateRational(GameState gameState, CancellationToken cancellationToken = default)
    {
        // Use the internal method with depth 0 (top level)
        return AnalyzeFromGameStateRationalInternal(gameState, 0, cancellationToken);
    }
    
    /// <summary>
    /// Internal implementation with depth tracking for controlling parallelization.
    /// </summary>
    private static GameOutcomes AnalyzeFromGameStateRationalInternal(GameState gameState, int depth, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return new GameOutcomes(0, 0, 0);

        if (gameState.IsGameOver)
        {
            if (gameState.Winner == 1)
                return new GameOutcomes(1, 0, 0);
            else if (gameState.Winner == 2)
                return new GameOutcomes(0, 1, 0);
            else
                return new GameOutcomes(0, 0, 1);
        }

        // Generate cache key from game state
        // Use a hash of the serialized state for efficient lookup
        var stateBytes = gameState.ToBytes();
        long cacheKey = GenerateCacheKey(stateBytes);
        
        // Check cache first
        if (RationalCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Only use parallel processing for the first few levels of recursion
        // This prevents thread pool saturation and improves CPU utilization
        bool useParallel = depth < 3;
        
        // Use parallel options for CPU-bound work
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        GameOutcomes result;

        if (gameState.PieceToPlay.HasValue)
        {
            // Current player needs to place the piece
            // RATIONAL PLAY: Check if any placement wins - if so, player WILL take it
            var emptyCells = gameState.Board.GetEmptyCells().ToList();
            
            // First, check for any winning placements (this is the key optimization)
            foreach (var cell in emptyCells)
            {
                var testState = gameState.Clone();
                testState.PlacePiece(cell.Row, cell.Col);
                
                if (testState.IsGameOver && testState.Winner != 0)
                {
                    // Current player has a winning move - they WILL take it
                    result = testState.Winner == 1 
                        ? new GameOutcomes(1, 0, 0) 
                        : new GameOutcomes(0, 1, 0);
                    RationalCache.TryAdd(cacheKey, result);
                    return result;
                }
            }
            
            // No winning move - explore all non-winning placements
            long p1Wins = 0, p2Wins = 0, draws = 0;
            
            if (useParallel && emptyCells.Count > 1)
            {
                try
                {
                    Parallel.ForEach(emptyCells, parallelOptions, (cell, loopState) =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            loopState.Stop();
                            return;
                        }
                        
                        var testState = gameState.Clone();
                        testState.PlacePiece(cell.Row, cell.Col);
                        
                        // We already checked for wins above, so this is not a winning placement
                        var cellResult = AnalyzeFromGameStateRationalInternal(testState, depth + 1, cancellationToken);
                        Interlocked.Add(ref p1Wins, cellResult.Player1Wins);
                        Interlocked.Add(ref p2Wins, cellResult.Player2Wins);
                        Interlocked.Add(ref draws, cellResult.Draws);
                    });
                }
                catch (OperationCanceledException)
                {
                    // Gracefully handle cancellation
                }
            }
            else
            {
                // Sequential processing for deeper levels
                foreach (var cell in emptyCells)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    var testState = gameState.Clone();
                    testState.PlacePiece(cell.Row, cell.Col);
                    
                    var cellResult = AnalyzeFromGameStateRationalInternal(testState, depth + 1, cancellationToken);
                    p1Wins += cellResult.Player1Wins;
                    p2Wins += cellResult.Player2Wins;
                    draws += cellResult.Draws;
                }
            }
            
            result = new GameOutcomes(p1Wins, p2Wins, draws);
        }
        else
        {
            // Current player needs to select a piece to give to opponent
            // RATIONAL PLAY: Don't give a piece that allows opponent to win immediately
            // (unless there's no other choice)
            var availablePieces = gameState.GetAvailablePieces().ToList();
            var emptyCells = gameState.Board.GetEmptyCells().ToList();
            
            // Filter pieces: find pieces that DON'T allow opponent to win immediately
            var safePieces = new List<Piece>();
            var unsafePieces = new List<Piece>();
            
            foreach (var piece in availablePieces)
            {
                bool allowsOpponentWin = false;
                
                // Check if giving this piece allows opponent to win
                foreach (var cell in emptyCells)
                {
                    var testState = gameState.Clone();
                    testState.GivePiece(piece);
                    testState.PlacePiece(cell.Row, cell.Col);
                    
                    if (testState.IsGameOver && testState.Winner != 0)
                    {
                        allowsOpponentWin = true;
                        break;
                    }
                }
                
                if (allowsOpponentWin)
                    unsafePieces.Add(piece);
                else
                    safePieces.Add(piece);
            }
            
            // Use safe pieces if available, otherwise must use unsafe pieces
            var piecesToAnalyze = safePieces.Count > 0 ? safePieces : unsafePieces;
            
            long p1Wins = 0, p2Wins = 0, draws = 0;
            
            if (useParallel && piecesToAnalyze.Count > 1)
            {
                try
                {
                    Parallel.ForEach(piecesToAnalyze, parallelOptions, (piece, loopState) =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            loopState.Stop();
                            return;
                        }
                        
                        var testState = gameState.Clone();
                        testState.GivePiece(piece);
                        
                        var pieceResult = AnalyzeFromGameStateRationalInternal(testState, depth + 1, cancellationToken);
                        Interlocked.Add(ref p1Wins, pieceResult.Player1Wins);
                        Interlocked.Add(ref p2Wins, pieceResult.Player2Wins);
                        Interlocked.Add(ref draws, pieceResult.Draws);
                    });
                }
                catch (OperationCanceledException)
                {
                    // Gracefully handle cancellation
                }
            }
            else
            {
                // Sequential processing for deeper levels
                foreach (var piece in piecesToAnalyze)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    var testState = gameState.Clone();
                    testState.GivePiece(piece);
                    
                    var pieceResult = AnalyzeFromGameStateRationalInternal(testState, depth + 1, cancellationToken);
                    p1Wins += pieceResult.Player1Wins;
                    p2Wins += pieceResult.Player2Wins;
                    draws += pieceResult.Draws;
                }
            }
            
            result = new GameOutcomes(p1Wins, p2Wins, draws);
        }
        
        // Cache the result before returning
        if (!cancellationToken.IsCancellationRequested)
            RationalCache.TryAdd(cacheKey, result);
            
        return result;
    }
    
    /// <summary>
    /// Generates a cache key from the game state bytes.
    /// Uses a fast hash function for efficient lookup.
    /// </summary>
    private static long GenerateCacheKey(byte[] stateBytes)
    {
        // Use a more robust hash combining algorithm (FNV-1a inspired)
        unchecked
        {
            long hash = -3750763034362895579L; // FNV offset basis as signed long
            foreach (byte b in stateBytes)
            {
                hash ^= b;
                hash *= 1099511628211L; // FNV prime
            }
            return hash;
        }
    }

    /// <summary>
    /// Gets the canonical signature for a board state at a given turn.
    /// </summary>
    private static long GetSignature(long board, int turn)
    {
        int firstPiece = GetPiece(board, IndexShuffle[0]);
        long signature = MinTranspositionValues[firstPiece];
        var ts = new List<int>(MinTranspositions[firstPiece]);

        for (int n = 1; n < turn; n++)
        {
            int min = 16;
            var ts2 = new List<int>();

            foreach (int t in ts)
            {
                int piece = GetPiece(board, IndexShuffle[n]);
                int posId = Transpositions[piece][t];

                if (posId == min)
                {
                    ts2.Add(t);
                }
                else if (posId < min)
                {
                    min = posId;
                    ts2.Clear();
                    ts2.Add(t);
                }
            }

            ts = ts2;
            signature = (signature << 4) | (uint)min;
        }

        return signature;
    }

    /// <summary>
    /// Gets the piece value at a given position on the board.
    /// </summary>
    private static int GetPiece(long board, int position)
    {
        return (int)((board >> (position << 2)) & 0xF);
    }

    /// <summary>
    /// Swaps two positions on the board.
    /// </summary>
    private static long Swap(long board, int pos1, int pos2)
    {
        if (pos1 == pos2) return board;

        int piece1 = GetPiece(board, pos1);
        int piece2 = GetPiece(board, pos2);

        // Clear both positions
        long mask1 = 0xFL << (pos1 << 2);
        long mask2 = 0xFL << (pos2 << 2);
        board &= ~(mask1 | mask2);

        // Set swapped values
        board |= (long)piece1 << (pos2 << 2);
        board |= (long)piece2 << (pos1 << 2);

        return board;
    }

    /// <summary>
    /// Partially evaluates the board for wins based on the turn.
    /// Only checks relevant winning lines for the given position.
    /// 
    /// The turn values correspond to positions in IndexShuffle:
    /// - Turn 15 (position 8): completes main diagonal (mask 8)
    /// - Turn 12 (position 12): completes row 3 (mask 3)
    /// - Turn 1 (position 5): can complete column 1 (mask 5)
    /// - Turn 3 (position 15): completes anti-diagonal (mask 9)
    /// - Turn 2 (position 10): can complete row 0 (mask 0) or column 2 (mask 6)
    /// - Turn 11 (position 7): completes column 3 (mask 7)
    /// - Turn 4 (position 14): completes row 1 (mask 1)  
    /// - Turn 8 (position 1): can complete column 0 (mask 4) or row 2 (mask 2)
    /// </summary>
    private static bool PartialEvaluate(long board, int turn)
    {
        return turn switch
        {
            15 => Evaluate(board, Masks[8]),  // Main diagonal
            12 => Evaluate(board, Masks[3]),  // Row 3
            1 => Evaluate(board, Masks[5]),   // Column 1
            3 => Evaluate(board, Masks[9]),   // Anti-diagonal
            2 => Evaluate(board, Masks[0]) || Evaluate(board, Masks[6]),  // Row 0 or Column 2
            11 => Evaluate(board, Masks[7]),  // Column 3
            4 => Evaluate(board, Masks[1]),   // Row 1
            8 => Evaluate(board, Masks[4]) || Evaluate(board, Masks[2]),  // Column 0 or Row 2
            _ => false
        };
    }

    /// <summary>
    /// Evaluates whether a winning line (specified by mask) has a win.
    /// A win occurs when all 4 pieces share at least one characteristic.
    /// </summary>
    private static bool Evaluate(long board, long mask)
    {
        long relevantBits = board & mask;

        // Extract the 4 pieces from the masked positions
        // The mask has 4 groups of 4 bits set
        int[] pieces = new int[4];
        int idx = 0;

        for (int pos = 0; pos < 16 && idx < 4; pos++)
        {
            if (((mask >> (pos << 2)) & 0xF) != 0)
            {
                pieces[idx++] = (int)((relevantBits >> (pos << 2)) & 0xF);
            }
        }

        // Check if all 4 pieces share any characteristic
        // They share a characteristic if AND or NAND of all pieces for that bit is all 0s or all 1s
        for (int bit = 0; bit < 4; bit++)
        {
            int bitMask = 1 << bit;
            int first = pieces[0] & bitMask;
            bool allSame = true;

            for (int i = 1; i < 4; i++)
            {
                if ((pieces[i] & bitMask) != first)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame) return true;
        }

        return false;
    }

    /// <summary>
    /// Clears the memoization cache.
    /// </summary>
    public static void ClearCache()
    {
        for (int i = 0; i < MaxLookupIndex; i++)
        {
            Cache[i].Clear();
            MinimaxCache[i].Clear();
        }
        RationalCache.Clear();
    }
    
    /// <summary>
    /// Performs minimax evaluation using the optimized board representation.
    /// Uses memoization and parallel processing for efficiency.
    /// </summary>
    public static MinimaxResult EvaluateMinimaxOptimized(long board, int turn, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return MinimaxResult.Unknown;

        long signature = 0;
        if (turn < MaxLookupIndex)
        {
            signature = GetSignature(board, turn);
            if (MinimaxCache[turn].TryGetValue(signature, out var cached))
                return cached;
        }

        int indexShuffled = IndexShuffle[turn];
        int branchCount = 16 - turn;
        
        // Use parallel processing when we have enough branches
        if (branchCount >= 6)
        {
            int bestResult = -2; // Start worse than Lose (-1)
            int hasDraw = 0;
            
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 4
            };

            try
            {
                Parallel.For(turn, 16, parallelOptions, (n, loopState) =>
                {
                    if (cancellationToken.IsCancellationRequested || Volatile.Read(ref bestResult) == 1)
                    {
                        loopState.Stop();
                        return;
                    }

                    long newBoard = Swap(board, indexShuffled, IndexShuffle[n]);
                    
                    // Check if this move creates a win
                    if (PartialEvaluate(newBoard, indexShuffled))
                    {
                        // Current player wins
                        Interlocked.Exchange(ref bestResult, 1);
                        loopState.Stop();
                        return;
                    }
                    
                    if (turn == 15)
                    {
                        // Draw - no win, no more moves
                        Interlocked.Exchange(ref hasDraw, 1);
                    }
                    else
                    {
                        var opponentResult = EvaluateMinimaxOptimized(newBoard, turn + 1, cancellationToken);
                        
                        if (opponentResult == MinimaxResult.Unknown)
                            return;
                        
                        // If opponent loses, we win
                        if (opponentResult == MinimaxResult.Lose)
                        {
                            Interlocked.Exchange(ref bestResult, 1);
                            loopState.Stop();
                            return;
                        }
                        
                        if (opponentResult == MinimaxResult.Draw)
                            Interlocked.Exchange(ref hasDraw, 1);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                return MinimaxResult.Unknown;
            }

            if (cancellationToken.IsCancellationRequested)
                return MinimaxResult.Unknown;

            MinimaxResult result;
            if (bestResult == 1)
                result = MinimaxResult.Win;
            else if (hasDraw == 1)
                result = MinimaxResult.Draw;
            else
                result = MinimaxResult.Lose;
            
            if (!cancellationToken.IsCancellationRequested && turn < MaxLookupIndex)
                MinimaxCache[turn][signature] = result;
                
            return result;
        }
        else
        {
            // Sequential processing for fewer branches
            bool foundDraw = false;

            for (int n = turn; n < 16; n++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return MinimaxResult.Unknown;

                long newBoard = Swap(board, indexShuffled, IndexShuffle[n]);
                
                // Check if this move creates a win
                if (PartialEvaluate(newBoard, indexShuffled))
                {
                    // Current player wins - cache and return immediately
                    if (turn < MaxLookupIndex)
                        MinimaxCache[turn][signature] = MinimaxResult.Win;
                    return MinimaxResult.Win;
                }
                
                if (turn == 15)
                {
                    foundDraw = true;
                }
                else
                {
                    var opponentResult = EvaluateMinimaxOptimized(newBoard, turn + 1, cancellationToken);
                    
                    if (opponentResult == MinimaxResult.Unknown)
                        return MinimaxResult.Unknown;
                    
                    // If opponent loses, we win
                    if (opponentResult == MinimaxResult.Lose)
                    {
                        if (turn < MaxLookupIndex)
                            MinimaxCache[turn][signature] = MinimaxResult.Win;
                        return MinimaxResult.Win;
                    }
                    
                    if (opponentResult == MinimaxResult.Draw)
                        foundDraw = true;
                }
            }

            var finalResult = foundDraw ? MinimaxResult.Draw : MinimaxResult.Lose;
            
            if (turn < MaxLookupIndex && !cancellationToken.IsCancellationRequested)
                MinimaxCache[turn][signature] = finalResult;

            return finalResult;
        }
    }
    
    /// <summary>
    /// Performs minimax evaluation to determine the guaranteed outcome with optimal play.
    /// Returns the result from the current player's perspective.
    /// Uses the optimized board representation for better performance.
    /// </summary>
    public static MinimaxResult EvaluateMinimax(GameState gameState, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return MinimaxResult.Unknown;
            
        if (gameState.IsGameOver)
        {
            if (gameState.IsDraw)
                return MinimaxResult.Draw;
            // If someone won, the current player lost (since they didn't make the winning move)
            return MinimaxResult.Lose;
        }
        
        // Convert GameState to optimized board representation and use optimized minimax
        return EvaluateMinimaxFromGameState(gameState, cancellationToken);
    }
    
    /// <summary>
    /// Evaluates minimax from a GameState by converting to the optimized board format.
    /// </summary>
    private static MinimaxResult EvaluateMinimaxFromGameState(GameState gameState, CancellationToken cancellationToken)
    {
        if (gameState.PieceToPlay.HasValue)
        {
            // Current player needs to place a piece
            bool hasDraw = false;
            var emptyCells = gameState.Board.GetEmptyCells().ToList();
            
            // Parallelize if enough cells
            if (emptyCells.Count >= 6)
            {
                int bestResult = -2;
                int foundDraw = 0;
                
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount * 4
                };

                try
                {
                    Parallel.ForEach(emptyCells, parallelOptions, (cell, loopState) =>
                    {
                        if (cancellationToken.IsCancellationRequested || Volatile.Read(ref bestResult) == 1)
                        {
                            loopState.Stop();
                            return;
                        }
                        
                        var testState = gameState.Clone();
                        testState.PlacePiece(cell.Row, cell.Col);
                        
                        if (testState.IsGameOver && testState.Winner != 0)
                        {
                            Interlocked.Exchange(ref bestResult, 1);
                            loopState.Stop();
                            return;
                        }
                        
                        var opponentResult = EvaluateMinimaxFromGameState(testState, cancellationToken);
                        
                        if (opponentResult == MinimaxResult.Lose)
                        {
                            Interlocked.Exchange(ref bestResult, 1);
                            loopState.Stop();
                            return;
                        }
                        
                        if (opponentResult == MinimaxResult.Draw)
                            Interlocked.Exchange(ref foundDraw, 1);
                    });
                }
                catch (OperationCanceledException)
                {
                    return MinimaxResult.Unknown;
                }

                if (cancellationToken.IsCancellationRequested)
                    return MinimaxResult.Unknown;

                if (bestResult == 1) return MinimaxResult.Win;
                if (foundDraw == 1) return MinimaxResult.Draw;
                return MinimaxResult.Lose;
            }
            else
            {
                foreach (var cell in emptyCells)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return MinimaxResult.Unknown;
                        
                    var testState = gameState.Clone();
                    testState.PlacePiece(cell.Row, cell.Col);
                    
                    if (testState.IsGameOver && testState.Winner != 0)
                        return MinimaxResult.Win;
                    
                    var opponentResult = EvaluateMinimaxFromGameState(testState, cancellationToken);
                    
                    if (opponentResult == MinimaxResult.Unknown)
                        return MinimaxResult.Unknown;
                    if (opponentResult == MinimaxResult.Lose)
                        return MinimaxResult.Win;
                    if (opponentResult == MinimaxResult.Draw)
                        hasDraw = true;
                }
                
                return hasDraw ? MinimaxResult.Draw : MinimaxResult.Lose;
            }
        }
        else
        {
            // Current player needs to give a piece
            var availablePieces = gameState.GetAvailablePieces().ToList();
            bool hasDraw = false;
            
            if (availablePieces.Count >= 6)
            {
                int bestResult = -2;
                int foundDraw = 0;
                
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount * 4
                };

                try
                {
                    Parallel.ForEach(availablePieces, parallelOptions, (piece, loopState) =>
                    {
                        if (cancellationToken.IsCancellationRequested || Volatile.Read(ref bestResult) == 1)
                        {
                            loopState.Stop();
                            return;
                        }
                        
                        var testState = gameState.Clone();
                        testState.GivePiece(piece);
                        
                        var opponentResult = EvaluateMinimaxFromGameState(testState, cancellationToken);
                        
                        if (opponentResult == MinimaxResult.Lose)
                        {
                            Interlocked.Exchange(ref bestResult, 1);
                            loopState.Stop();
                            return;
                        }
                        
                        if (opponentResult == MinimaxResult.Draw)
                            Interlocked.Exchange(ref foundDraw, 1);
                    });
                }
                catch (OperationCanceledException)
                {
                    return MinimaxResult.Unknown;
                }

                if (cancellationToken.IsCancellationRequested)
                    return MinimaxResult.Unknown;

                if (bestResult == 1) return MinimaxResult.Win;
                if (foundDraw == 1) return MinimaxResult.Draw;
                return MinimaxResult.Lose;
            }
            else
            {
                foreach (var piece in availablePieces)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return MinimaxResult.Unknown;
                        
                    var testState = gameState.Clone();
                    testState.GivePiece(piece);
                    
                    var opponentResult = EvaluateMinimaxFromGameState(testState, cancellationToken);
                    
                    if (opponentResult == MinimaxResult.Unknown)
                        return MinimaxResult.Unknown;
                    if (opponentResult == MinimaxResult.Lose)
                        return MinimaxResult.Win;
                    if (opponentResult == MinimaxResult.Draw)
                        hasDraw = true;
                }
                
                return hasDraw ? MinimaxResult.Draw : MinimaxResult.Lose;
            }
        }
    }
    
    /// <summary>
    /// Analyzes a piece selection with both outcome counting and minimax evaluation.
    /// </summary>
    public static AnalysisResult AnalyzePieceSelectionFull(GameState gameState, Piece piece, CancellationToken cancellationToken = default)
    {
        if (!gameState.IsPieceAvailable(piece))
            return new AnalysisResult(new GameOutcomes(0, 0, 0), MinimaxResult.Unknown);
        
        if (cancellationToken.IsCancellationRequested)
            return new AnalysisResult(new GameOutcomes(0, 0, 0), MinimaxResult.Unknown);
        
        // Clone and give the piece
        var testState = gameState.Clone();
        testState.GivePiece(piece);
        
        // Run both analyses in parallel
        var outcomesTask = Task.Run(() => AnalyzeFromGameState(testState, cancellationToken), cancellationToken);
        var minimaxTask = Task.Run(() => EvaluateMinimax(testState, cancellationToken), cancellationToken);
        
        try
        {
            Task.WaitAll(new Task[] { outcomesTask, minimaxTask }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new AnalysisResult(new GameOutcomes(0, 0, 0), MinimaxResult.Unknown);
        }
        
        var outcomes = outcomesTask.Result;
        var minimax = minimaxTask.Result;
        
        // The minimax result is from the opponent's perspective (they place next)
        // So we need to invert it for the current player
        var currentPlayerMinimax = minimax switch
        {
            MinimaxResult.Win => MinimaxResult.Lose,
            MinimaxResult.Lose => MinimaxResult.Win,
            _ => minimax
        };
        
        return new AnalysisResult(outcomes, currentPlayerMinimax);
    }
    
    /// <summary>
    /// Analyzes a placement with both outcome counting and minimax evaluation.
    /// </summary>
    public static AnalysisResult AnalyzePlacementFull(GameState gameState, int row, int col, CancellationToken cancellationToken = default)
    {
        if (!gameState.PieceToPlay.HasValue)
            return new AnalysisResult(new GameOutcomes(0, 0, 0), MinimaxResult.Unknown);
        
        if (!gameState.Board.IsEmpty(row, col))
            return new AnalysisResult(new GameOutcomes(0, 0, 0), MinimaxResult.Unknown);
        
        if (cancellationToken.IsCancellationRequested)
            return new AnalysisResult(new GameOutcomes(0, 0, 0), MinimaxResult.Unknown);
        
        // Clone and place the piece
        var testState = gameState.Clone();
        testState.PlacePiece(row, col);
        
        // Check if this placement wins
        if (testState.IsGameOver && testState.Winner != 0)
        {
            var winOutcomes = testState.Winner == 1 
                ? new GameOutcomes(1, 0, 0) 
                : new GameOutcomes(0, 1, 0);
            return new AnalysisResult(winOutcomes, MinimaxResult.Win);
        }
        
        // Run both analyses in parallel
        var outcomesTask = Task.Run(() => AnalyzeFromGameState(testState, cancellationToken), cancellationToken);
        var minimaxTask = Task.Run(() => EvaluateMinimax(testState, cancellationToken), cancellationToken);
        
        try
        {
            Task.WaitAll(new Task[] { outcomesTask, minimaxTask }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new AnalysisResult(new GameOutcomes(0, 0, 0), MinimaxResult.Unknown);
        }
        
        var outcomes = outcomesTask.Result;
        var minimax = minimaxTask.Result;
        
        // The minimax result is from the opponent's perspective (they select a piece next)
        // So we need to invert it for the current player
        var currentPlayerMinimax = minimax switch
        {
            MinimaxResult.Win => MinimaxResult.Lose,
            MinimaxResult.Lose => MinimaxResult.Win,
            _ => minimax
        };
        
        return new AnalysisResult(outcomes, currentPlayerMinimax);
    }
}
