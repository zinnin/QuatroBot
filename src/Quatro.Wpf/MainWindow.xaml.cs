using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Quatro.Core;

namespace Quatro.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private GameState _gameState = new();
    private readonly Button[] _boardButtons = new Button[16];
    private readonly Button[] _pieceButtons = new Button[16];
    private bool _analysisEnabled = false;
    private CancellationTokenSource? _analysisCts;
    private BotPlayer? _player1Bot = null;
    private BotPlayer? _player2Bot = null;
    private System.Windows.Threading.DispatcherTimer? _botMoveTimer;

    public MainWindow()
    {
        InitializeComponent();
        InitializeBoard();
        InitializePieces();
        InitializeBotTimer();
        UpdateUI();
    }

    private void InitializeBotTimer()
    {
        _botMoveTimer = new System.Windows.Threading.DispatcherTimer();
        _botMoveTimer.Interval = TimeSpan.FromMilliseconds(500); // Delay for visual feedback
        _botMoveTimer.Tick += BotMoveTimer_Tick;
    }

    private void InitializeBoard()
    {
        for (int i = 0; i < 16; i++)
        {
            var button = new Button
            {
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x54)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x74)),
                BorderThickness = new Thickness(2),
                Tag = i
            };
            button.Click += BoardCell_Click;
            button.MouseEnter += BoardCell_MouseEnter;
            button.MouseLeave += BoardCell_MouseLeave;
            _boardButtons[i] = button;
            BoardGrid.Children.Add(button);
        }
    }

    private void InitializePieces()
    {
        for (byte i = 0; i < 16; i++)
        {
            var piece = new Piece(i);
            var button = new Button
            {
                Margin = new Thickness(3),
                Background = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x54)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x74)),
                BorderThickness = new Thickness(2),
                Content = CreatePieceVisual(piece),
                Tag = i
            };
            button.Click += PieceButton_Click;
            button.MouseEnter += PieceButton_MouseEnter;
            button.MouseLeave += PieceButton_MouseLeave;
            _pieceButtons[i] = button;
            PiecesGrid.Children.Add(button);
        }
    }

    private static UIElement CreatePieceVisual(Piece piece)
    {
        var height = piece.IsTall ? 50 : 30;
        var color = piece.IsDark ? Colors.SaddleBrown : Colors.BurlyWood;
        var fillColor = piece.IsSolid ? color : Colors.Transparent;
        var strokeColor = color;

        Shape shape;
        if (piece.IsRound)
        {
            shape = new Ellipse
            {
                Width = 40,
                Height = height,
                Fill = new SolidColorBrush(fillColor),
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 3
            };
        }
        else
        {
            shape = new Rectangle
            {
                Width = 40,
                Height = height,
                Fill = new SolidColorBrush(fillColor),
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 3
            };
        }

        return shape;
    }

    private void CancelPendingAnalysis()
    {
        try
        {
            _analysisCts?.Cancel();
        }
        finally
        {
            _analysisCts?.Dispose();
            _analysisCts = null;
        }
    }

    private void BoardCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (!_gameState.PieceToPlay.HasValue) return;
        if (_gameState.IsGameOver) return;

        // Cancel any pending analysis when making a move
        CancelPendingAnalysis();

        int index = (int)button.Tag;
        int row = index / 4;
        int col = index % 4;

        if (_gameState.PlacePiece(row, col))
        {
            UpdateUI();
        }
    }

    private async void BoardCell_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_analysisEnabled) return;
        if (sender is not Button button) return;
        if (!_gameState.PieceToPlay.HasValue) return;
        if (_gameState.IsGameOver) return;

        int index = (int)button.Tag;
        int row = index / 4;
        int col = index % 4;

        if (!_gameState.Board.IsEmpty(row, col)) return;

        // Cancel any previous analysis
        CancelPendingAnalysis();
        _analysisCts = new CancellationTokenSource();
        var token = _analysisCts.Token;

        AnalysisText.Text = $"Analyzing placement at ({row}, {col})...";
        ShowAnalysisLoading();
        
        // Capture state for background thread
        var gameStateCopy = _gameState.Clone();
        
        try
        {
            // Run analysis on background thread
            var result = await Task.Run(() => 
                MoveAnalyzer.AnalyzePlacementFull(gameStateCopy, row, col, token), token);
            
            // Update UI on main thread (only if not cancelled)
            if (!token.IsCancellationRequested)
            {
                UpdateAnalysisDisplay(result, $"Place at ({row}, {col})");
            }
        }
        catch (OperationCanceledException)
        {
            // Analysis was cancelled, ignore
        }
    }

    private void BoardCell_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_analysisEnabled) return;
        CancelPendingAnalysis();
        ClearAnalysisDisplay();
    }

    private void PieceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (_gameState.PieceToPlay.HasValue) return;
        if (_gameState.IsGameOver) return;

        // Cancel any pending analysis when making a move
        CancelPendingAnalysis();

        byte pieceValue = (byte)button.Tag;
        var piece = new Piece(pieceValue);

        if (_gameState.GivePiece(piece))
        {
            UpdateUI();
        }
    }

    private async void PieceButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_analysisEnabled) return;
        if (sender is not Button button) return;
        if (_gameState.PieceToPlay.HasValue) return;
        if (_gameState.IsGameOver) return;

        byte pieceValue = (byte)button.Tag;
        var piece = new Piece(pieceValue);

        if (!_gameState.IsPieceAvailable(piece)) return;

        // Cancel any previous analysis
        CancelPendingAnalysis();
        _analysisCts = new CancellationTokenSource();
        var token = _analysisCts.Token;

        AnalysisText.Text = $"Analyzing piece {piece}...";
        ShowAnalysisLoading();
        
        // Capture state for background thread
        var gameStateCopy = _gameState.Clone();
        
        try
        {
            // Run analysis on background thread
            var result = await Task.Run(() => 
                MoveAnalyzer.AnalyzePieceSelectionFull(gameStateCopy, piece, token), token);
            
            // Update UI on main thread (only if not cancelled)
            if (!token.IsCancellationRequested)
            {
                UpdateAnalysisDisplay(result, $"Give piece {piece}");
            }
        }
        catch (OperationCanceledException)
        {
            // Analysis was cancelled, ignore
        }
    }

    private void PieceButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_analysisEnabled) return;
        CancelPendingAnalysis();
        ClearAnalysisDisplay();
    }

    private void ShowAnalysisMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _analysisEnabled = ShowAnalysisMenuItem.IsChecked;
        
        if (_analysisEnabled)
        {
            AnalysisColumn.Width = new GridLength(250);
            AnalysisPanel.Visibility = Visibility.Visible;
            this.Width = 1100;
        }
        else
        {
            CancelPendingAnalysis();
            AnalysisColumn.Width = new GridLength(0);
            AnalysisPanel.Visibility = Visibility.Collapsed;
            this.Width = 900;
        }
    }

    private void ShowAnalysisLoading()
    {
        OptimalResultText.Text = "Calculating...";
        OptimalResultText.Foreground = new SolidColorBrush(Colors.White);
        Player1WinsText.Text = "P1 Wins: calculating...";
        Player2WinsText.Text = "P2 Wins: calculating...";
        DrawsText.Text = "Draws: calculating...";
        TotalGamesText.Text = "Total: calculating...";
    }

    private void UpdateAnalysisDisplay(AnalysisResult result, string description)
    {
        AnalysisText.Text = description;
        
        // Update optimal play result
        switch (result.OptimalResult)
        {
            case MinimaxResult.Win:
                OptimalResultText.Text = "You can WIN with optimal play!";
                OptimalResultText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0xFF, 0x88));
                break;
            case MinimaxResult.Lose:
                OptimalResultText.Text = "You will LOSE with optimal play";
                OptimalResultText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x88));
                break;
            case MinimaxResult.Draw:
                OptimalResultText.Text = "DRAW with optimal play";
                OptimalResultText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x88));
                break;
            default:
                OptimalResultText.Text = "Unknown";
                OptimalResultText.Foreground = new SolidColorBrush(Colors.White);
                break;
        }
        
        // Update outcome counts
        Player1WinsText.Text = $"P1 Wins: {result.Outcomes.Player1Wins:N0}";
        Player2WinsText.Text = $"P2 Wins: {result.Outcomes.Player2Wins:N0}";
        DrawsText.Text = $"Draws: {result.Outcomes.Draws:N0}";
        TotalGamesText.Text = $"Total: {result.Outcomes.TotalGames:N0}";
    }

    private void ClearAnalysisDisplay()
    {
        AnalysisText.Text = "Hover over pieces or board positions to see analysis.";
        OptimalResultText.Text = "-";
        OptimalResultText.Foreground = new SolidColorBrush(Colors.White);
        Player1WinsText.Text = "P1 Wins: -";
        Player2WinsText.Text = "P2 Wins: -";
        DrawsText.Text = "Draws: -";
        TotalGamesText.Text = "Total: -";
    }

    private void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
        CancelPendingAnalysis();
        _gameState = new GameState();
        UpdateUI();
        ClearAnalysisDisplay();
    }

    private void UpdateUI()
    {
        // Update board
        for (int i = 0; i < 16; i++)
        {
            var cellValue = _gameState.Board[i];
            if (cellValue == Board.EmptyCell)
            {
                _boardButtons[i].Content = null;
                _boardButtons[i].Background = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x54));
            }
            else
            {
                _boardButtons[i].Content = CreatePieceVisual(new Piece(cellValue));
                _boardButtons[i].Background = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x44));
            }
        }

        // Highlight winning line
        var winningLine = WinChecker.GetWinningLine(_gameState.Board);
        if (winningLine != null)
        {
            foreach (var index in winningLine)
            {
                _boardButtons[index].Background = new SolidColorBrush(Color.FromRgb(0x4E, 0x8E, 0x4E));
            }
        }

        // Update available pieces
        for (byte i = 0; i < 16; i++)
        {
            var piece = new Piece(i);
            _pieceButtons[i].IsEnabled = _gameState.IsPieceAvailable(piece) && 
                                          !_gameState.PieceToPlay.HasValue &&
                                          !_gameState.IsGameOver;
            _pieceButtons[i].Opacity = _gameState.IsPieceAvailable(piece) ? 1.0 : 0.3;
        }

        // Update selected piece display
        if (_gameState.PieceToPlay.HasValue)
        {
            SelectedPieceDisplay.Child = CreatePieceVisual(_gameState.PieceToPlay.Value);
            SelectedPieceDisplay.Background = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x64));
        }
        else
        {
            SelectedPieceDisplay.Child = null;
            SelectedPieceDisplay.Background = Brushes.Transparent;
        }

        // Update status text
        if (_gameState.IsGameOver)
        {
            if (_gameState.IsDraw)
            {
                StatusText.Text = "Game Over - It's a Draw!";
            }
            else
            {
                StatusText.Text = $"Game Over - Player {_gameState.Winner} Wins!";
            }
        }
        else if (_gameState.PieceToPlay.HasValue)
        {
            var currentPlayer = _gameState.IsPlayer1Turn ? 1 : 2;
            StatusText.Text = $"Player {currentPlayer}: Place the piece on the board";
        }
        else
        {
            var currentPlayer = _gameState.IsPlayer1Turn ? 1 : 2;
            StatusText.Text = $"Player {currentPlayer}: Select a piece to give";
        }

        // Check if we should trigger a bot move
        TriggerBotMoveIfNeeded();
    }

    private void Player1Level_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string levelStr)
        {
            int level = int.Parse(levelStr);
            _player1Bot = new BotPlayer((BotLevel)level);
            
            // Update menu checkmarks
            Player1HumanMenuItem.IsChecked = false;
            Player1Level1MenuItem.IsChecked = level == 1;
            Player1Level2MenuItem.IsChecked = level == 2;
            Player1Level3MenuItem.IsChecked = level == 3;
            Player1Level4MenuItem.IsChecked = level == 4;
            Player1Level5MenuItem.IsChecked = level == 5;
            
            // Trigger bot move if it's player 1's turn
            TriggerBotMoveIfNeeded();
        }
    }

    private void Player1Human_Click(object sender, RoutedEventArgs e)
    {
        _player1Bot = null;
        Player1HumanMenuItem.IsChecked = true;
        Player1Level1MenuItem.IsChecked = false;
        Player1Level2MenuItem.IsChecked = false;
        Player1Level3MenuItem.IsChecked = false;
        Player1Level4MenuItem.IsChecked = false;
        Player1Level5MenuItem.IsChecked = false;
    }

    private void Player2Level_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string levelStr)
        {
            int level = int.Parse(levelStr);
            _player2Bot = new BotPlayer((BotLevel)level);
            
            // Update menu checkmarks
            Player2HumanMenuItem.IsChecked = false;
            Player2Level1MenuItem.IsChecked = level == 1;
            Player2Level2MenuItem.IsChecked = level == 2;
            Player2Level3MenuItem.IsChecked = level == 3;
            Player2Level4MenuItem.IsChecked = level == 4;
            Player2Level5MenuItem.IsChecked = level == 5;
            
            // Trigger bot move if it's player 2's turn
            TriggerBotMoveIfNeeded();
        }
    }

    private void Player2Human_Click(object sender, RoutedEventArgs e)
    {
        _player2Bot = null;
        Player2HumanMenuItem.IsChecked = true;
        Player2Level1MenuItem.IsChecked = false;
        Player2Level2MenuItem.IsChecked = false;
        Player2Level3MenuItem.IsChecked = false;
        Player2Level4MenuItem.IsChecked = false;
        Player2Level5MenuItem.IsChecked = false;
    }

    private void TriggerBotMoveIfNeeded()
    {
        if (_gameState.IsGameOver)
            return;

        var currentBot = _gameState.IsPlayer1Turn ? _player1Bot : _player2Bot;
        
        if (currentBot != null && _botMoveTimer != null && !_botMoveTimer.IsEnabled)
        {
            _botMoveTimer.Start();
        }
    }

    private async void BotMoveTimer_Tick(object? sender, EventArgs e)
    {
        _botMoveTimer?.Stop();

        if (_gameState.IsGameOver)
            return;

        var currentBot = _gameState.IsPlayer1Turn ? _player1Bot : _player2Bot;
        
        if (currentBot == null)
            return;

        try
        {
            if (_gameState.PieceToPlay.HasValue)
            {
                // Bot needs to place the piece
                StatusText.Text = $"Player {(_gameState.IsPlayer1Turn ? 1 : 2)} (Bot): Thinking...";
                
                // Run bot decision on background thread
                var placement = await Task.Run(() => currentBot.SelectPlacement(_gameState));
                
                // Make the move on UI thread
                _gameState.PlacePiece(placement.row, placement.col);
                UpdateUI();
            }
            else
            {
                // Bot needs to select a piece
                StatusText.Text = $"Player {(_gameState.IsPlayer1Turn ? 1 : 2)} (Bot): Thinking...";
                
                // Run bot decision on background thread
                var piece = await Task.Run(() => currentBot.SelectPieceToGive(_gameState));
                
                // Make the move on UI thread
                _gameState.GivePiece(piece);
                UpdateUI();
            }
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show($"Bot made an invalid move: {ex.Message}", "Bot Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unexpected bot error: {ex.Message}\n\nPlease restart the game.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}