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
        for (int i = 0; i < MaxLookupIndex; i++)
        {
            Cache[i] = new ConcurrentDictionary<long, GameOutcomes>();
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
    /// Uses parallel processing at early turns for better CPU utilization.
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
        // Parallelize at early turns (0-4) where there's significant work per branch
        // At turn 4, branchCount is 12, still good for parallelization
        if (turn < 5 && branchCount >= 8)
        {
            long p1Wins = 0, p2Wins = 0, draws = 0;
            
            // Use 2x processor count for better throughput on CPU-bound work
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2
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
            // Sequential processing for deeper turns (already enough parallelism from above)
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
        
        return AnalyzeFromGameState(testState, cancellationToken);
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
        
        return AnalyzeFromGameState(testState, cancellationToken);
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
        
        // Use 2x processor count for better throughput on CPU-bound work
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount * 2
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
        }
    }
}
