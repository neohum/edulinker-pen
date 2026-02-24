using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EdulinkerPen
{
    public partial class ToolbarWindow : Window
    {
        private MainWindow _mainWindow;
        private bool _isExpanded = true;
        private bool _wasDragged;
        private System.Windows.Point _dragStartPoint;
        private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(250));

        public ToolbarWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            this.Owner = mainWindow;
            this.Loaded += ToolbarWindow_Loaded;
        }

        private void ToolbarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position at top right after size is known
            this.Left = SystemParameters.PrimaryScreenWidth - this.ActualWidth - 20;
            this.Top = 20;

            // Prevent toolbar from stealing focus from the main drawing window
            Win32Interop.MakeNonActivating(this);
        }

        // --- Menu Icon: click to toggle, drag to move ---

        private void MenuIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _wasDragged = false;
        }

        private void MenuIcon_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _wasDragged) return;

            var pos = e.GetPosition(this);
            if (System.Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                System.Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _wasDragged = true;
                this.DragMove();
            }
        }

        private void MenuIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_wasDragged)
            {
                if (_isExpanded)
                    CollapseToolbar();
                else
                    ExpandToolbar();
            }
            _wasDragged = false;
        }

        // --- Expand / Collapse ---

        private void ExpandToolbar()
        {
            if (_isExpanded || ToolsPanelScale == null) return;
            _isExpanded = true;

            _mainWindow.SetPenMode();

            var anim = new DoubleAnimation(0, 1, AnimationDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ToolsPanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void CollapseToolbar()
        {
            if (!_isExpanded || ToolsPanelScale == null) return;
            _isExpanded = false;

            _mainWindow.SetCursorMode();

            var anim = new DoubleAnimation(1, 0, AnimationDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            ToolsPanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        // --- Cursor visibility ---

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _mainWindow.HideCustomCursor();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _mainWindow.ShowCustomCursor();
        }

        // --- Tool buttons ---

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
