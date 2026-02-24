using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EdulinkerPen
{
    public partial class ToolbarWindow : Window
    {
        private MainWindow _mainWindow;

        public ToolbarWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            this.Owner = mainWindow;
            
            // Start at the top right of the screen
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
            this.Top = 20;
        }

        private void ToolbarBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _mainWindow.HideCustomCursor();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _mainWindow.ShowCustomCursor();
        }

        private void BtnCursor_Click(object sender, RoutedEventArgs e) => _mainWindow.SetCursorMode();
        private void BtnPen_Click(object sender, RoutedEventArgs e) => _mainWindow.SetPenMode();
        private void BtnHighlighter_Click(object sender, RoutedEventArgs e) => _mainWindow.SetHighlighterMode();
        private void BtnEraser_Click(object sender, RoutedEventArgs e) => _mainWindow.SetEraserMode();
        private void BtnClear_Click(object sender, RoutedEventArgs e) => _mainWindow.ClearCanvas();

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                _mainWindow.SetColor(brush.Color);
            }
        }

        private void TxtAbout_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var version = UpdateService.GetCurrentVersion();
            var versionStr = $"{version.Major}.{version.Minor}.{version.Build}";
            System.Windows.MessageBox.Show($"Edulinker-Pen v{versionStr}\n\nMain Pen Icon Attribution:\n펜 아이콘 제작자: Those Icons - Flaticon\n(https://www.flaticon.com/kr/free-icons/)\n\n제작: schoolworks 팀\n관련문의: neohum77@gmail.com", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
