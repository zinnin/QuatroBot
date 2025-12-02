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

    public MainWindow()
    {
        InitializeComponent();
        InitializeBoard();
        InitializePieces();
        UpdateUI();
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

    private void BoardCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (!_gameState.PieceToPlay.HasValue) return;
        if (_gameState.IsGameOver) return;

        int index = (int)button.Tag;
        int row = index / 4;
        int col = index % 4;

        if (_gameState.PlacePiece(row, col))
        {
            UpdateUI();
        }
    }

    private void BoardCell_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_analysisEnabled) return;
        if (sender is not Button button) return;
        if (!_gameState.PieceToPlay.HasValue) return;
        if (_gameState.IsGameOver) return;

        int index = (int)button.Tag;
        int row = index / 4;
        int col = index % 4;

        if (!_gameState.Board.IsEmpty(row, col)) return;

        AnalysisText.Text = $"Analyzing placement at ({row}, {col})...";
        
        // Run analysis in background
        var outcomes = MoveAnalyzer.AnalyzePlacement(_gameState, row, col);
        UpdateAnalysisDisplay(outcomes, $"Place at ({row}, {col})");
    }

    private void BoardCell_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_analysisEnabled) return;
        ClearAnalysisDisplay();
    }

    private void PieceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (_gameState.PieceToPlay.HasValue) return;
        if (_gameState.IsGameOver) return;

        byte pieceValue = (byte)button.Tag;
        var piece = new Piece(pieceValue);

        if (_gameState.GivePiece(piece))
        {
            UpdateUI();
        }
    }

    private void PieceButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_analysisEnabled) return;
        if (sender is not Button button) return;
        if (_gameState.PieceToPlay.HasValue) return;
        if (_gameState.IsGameOver) return;

        byte pieceValue = (byte)button.Tag;
        var piece = new Piece(pieceValue);

        if (!_gameState.IsPieceAvailable(piece)) return;

        AnalysisText.Text = $"Analyzing piece {piece}...";
        
        // Run analysis
        var outcomes = MoveAnalyzer.AnalyzePieceSelection(_gameState, piece);
        UpdateAnalysisDisplay(outcomes, $"Give piece {piece}");
    }

    private void PieceButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_analysisEnabled) return;
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
            AnalysisColumn.Width = new GridLength(0);
            AnalysisPanel.Visibility = Visibility.Collapsed;
            this.Width = 900;
        }
    }

    private void UpdateAnalysisDisplay(GameOutcomes outcomes, string description)
    {
        AnalysisText.Text = description;
        Player1WinsText.Text = $"P1 Wins: {outcomes.Player1Wins:N0}";
        Player2WinsText.Text = $"P2 Wins: {outcomes.Player2Wins:N0}";
        DrawsText.Text = $"Draws: {outcomes.Draws:N0}";
        TotalGamesText.Text = $"Total: {outcomes.TotalGames:N0}";
    }

    private void ClearAnalysisDisplay()
    {
        AnalysisText.Text = "Hover over pieces or board positions to see analysis.";
        Player1WinsText.Text = "P1 Wins: -";
        Player2WinsText.Text = "P2 Wins: -";
        DrawsText.Text = "Draws: -";
        TotalGamesText.Text = "Total: -";
    }

    private void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
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
    }
}