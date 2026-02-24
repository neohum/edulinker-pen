using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private bool _isHorizontalLayout = false;
        private int _layoutMode = 0; // 0=vertical, 1=horizontal, 2=both
        private ToolbarWindow? _secondaryToolbar;
        private ToolbarWindow? _primaryToolbar; // non-null if this is a secondary
        private System.Windows.Point _dragStartPoint;
        private System.Windows.Controls.Button? _selectedColorButton;
        private System.Windows.Controls.Button? _selectedToolButton;
        private System.Windows.Controls.Button? _selectedBrushSizeButton;
        private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(250));

        public ToolbarWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            this.Owner = mainWindow;
            this.Loaded += ToolbarWindow_Loaded;
        }

        // Constructor for secondary toolbar (used in "both" mode)
        public ToolbarWindow(MainWindow mainWindow, ToolbarWindow primaryToolbar)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _primaryToolbar = primaryToolbar;
            this.Owner = mainWindow;
            this.Loaded += SecondaryToolbar_Loaded;
        }

        private void ToolbarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position at top right after size is known
            this.Left = SystemParameters.PrimaryScreenWidth - this.ActualWidth - 20;
            this.Top = 20;

            // Prevent toolbar from stealing focus from the main drawing window
            Win32Interop.MakeNonActivating(this);

            // Highlight the color matching the saved/current color
            HighlightMatchingColor(_mainWindow.CurrentColor);

            // Highlight default tool (pen)
            UpdateToolHighlight(BtnPen);
        }

        private void SecondaryToolbar_Loaded(object sender, RoutedEventArgs e)
        {
            // Prevent toolbar from stealing focus from the main drawing window
            Win32Interop.MakeNonActivating(this);

            // Defer horizontal layout to after initial render completes
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isHorizontalLayout = true;
                ApplyLayout();
                UpdateLayout();

                // Position at top-left after layout is applied and size is known
                this.Left = 20;
                this.Top = 20;

                // Highlight same state as primary
                HighlightMatchingColor(_mainWindow.CurrentColor);
                if (_primaryToolbar?._selectedToolButton != null)
                {
                    string toolName = _primaryToolbar._selectedToolButton.Name;
                    SyncToolSelection(toolName);
                }
                else
                {
                    UpdateToolHighlight(BtnPen);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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

        // --- Settings popup (layout mode selection) ---

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
        }

        private void LayoutMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int mode))
            {
                if (_primaryToolbar != null)
                {
                    // Secondary toolbar → delegate to primary
                    _primaryToolbar._layoutMode = mode;
                    _primaryToolbar.ApplyLayoutMode();
                }
                else
                {
                    _layoutMode = mode;
                    ApplyLayoutMode();
                }
                SettingsPopup.IsOpen = false;
            }
        }

        private void ApplyLayoutMode()
        {
            // Close existing secondary if any
            if (_secondaryToolbar != null)
            {
                _secondaryToolbar.Close();
                _secondaryToolbar = null;
            }

            switch (_layoutMode)
            {
                case 0: // Vertical only
                    _isHorizontalLayout = false;
                    ApplyLayout();
                    break;
                case 1: // Horizontal only
                    _isHorizontalLayout = true;
                    ApplyLayout();
                    break;
                case 2: // Both: primary=vertical, secondary=horizontal
                    _isHorizontalLayout = false;
                    ApplyLayout();
                    _secondaryToolbar = new ToolbarWindow(_mainWindow, this);
                    _secondaryToolbar.Show();
                    break;
            }
        }

        private void ApplyLayout()
        {
            if (_isHorizontalLayout)
            {
                OuterPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                MenuIconBorder.Margin = new Thickness(0, 0, 8, 0);
                ToolsPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;

                foreach (UIElement child in ToolsPanel.Children)
                {
                    if (child is FrameworkElement fe && child is not Popup)
                        fe.Margin = new Thickness(0, 0, 6, 0);
                }

                Separator1.Width = 1;
                Separator1.Height = double.NaN;
                Separator1.Margin = new Thickness(4, 0, 8, 0);
                Separator1.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                Separator1.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;

                Separator2.Width = 1;
                Separator2.Height = double.NaN;
                Separator2.Margin = new Thickness(4, 0, 8, 0);
                Separator2.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                Separator2.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;

                Separator3.Width = 1;
                Separator3.Height = double.NaN;
                Separator3.Margin = new Thickness(4, 0, 8, 0);
                Separator3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                Separator3.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;

                TxtAbout.Margin = new Thickness(10, 0, 0, 0);
            }
            else
            {
                OuterPanel.Orientation = System.Windows.Controls.Orientation.Vertical;
                MenuIconBorder.Margin = new Thickness(0, 0, 0, 8);
                ToolsPanel.Orientation = System.Windows.Controls.Orientation.Vertical;

                foreach (UIElement child in ToolsPanel.Children)
                {
                    if (child is FrameworkElement fe && child is not Popup)
                        fe.Margin = new Thickness(0, 0, 0, 6);
                }

                Separator1.Height = 1;
                Separator1.Width = double.NaN;
                Separator1.Margin = new Thickness(0, 4, 0, 8);
                Separator1.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                Separator1.VerticalAlignment = System.Windows.VerticalAlignment.Center;

                Separator2.Height = 1;
                Separator2.Width = double.NaN;
                Separator2.Margin = new Thickness(0, 4, 0, 8);
                Separator2.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                Separator2.VerticalAlignment = System.Windows.VerticalAlignment.Center;

                Separator3.Height = 1;
                Separator3.Width = double.NaN;
                Separator3.Margin = new Thickness(0, 4, 0, 8);
                Separator3.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                Separator3.VerticalAlignment = System.Windows.VerticalAlignment.Center;

                TxtAbout.Margin = new Thickness(0, 10, 0, 0);
            }

            // Reset scale animation to expanded state
            ToolsPanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ToolsPanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ToolsPanelScale.ScaleX = 1;
            ToolsPanelScale.ScaleY = 1;
            _isExpanded = true;
        }

        // --- Expand / Collapse ---

        private void ExpandToolbar()
        {
            if (_isExpanded || ToolsPanelScale == null) return;
            _isExpanded = true;

            _mainWindow.SetPenMode();
            UpdateToolHighlight(BtnPen);
            GetPairedToolbar()?.SyncToolSelection("BtnPen");

            var prop = _isHorizontalLayout ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty;
            var anim = new DoubleAnimation(0, 1, AnimationDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ToolsPanelScale.BeginAnimation(prop, anim);
        }

        private void CollapseToolbar()
        {
            if (!_isExpanded || ToolsPanelScale == null) return;
            _isExpanded = false;

            _mainWindow.SetCursorMode();

            var prop = _isHorizontalLayout ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty;
            var anim = new DoubleAnimation(1, 0, AnimationDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            ToolsPanelScale.BeginAnimation(prop, anim);
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

        private void BtnPen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedToolButton == BtnPen)
            {
                // Already selected, toggle popup
                PenPopup.IsOpen = !PenPopup.IsOpen;
            }
            else
            {
                _mainWindow.SetPenMode();
                UpdateToolHighlight(BtnPen);
                GetPairedToolbar()?.SyncToolSelection("BtnPen");
                PenPopup.IsOpen = true;
                BrushSizePopup.IsOpen = false;
                BackgroundPopup.IsOpen = false;
            }
        }

        private void PenType_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string typeStr)
            {
                if (typeStr == "Normal")
                {
                    _mainWindow.SetPenMode();
                    
                    // Restore default pen icon
                    PenIconPath.Data = Geometry.Parse("M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z");
                    PenIconPath.Fill = null;
                }
                else if (typeStr == "MagicPetal")
                {
                    _mainWindow.SetMagicPenMode();
                    _mainWindow.SetMagicPenType(ParticleType.Petal);
                    
                    // Change icon to magic pen petal
                    PenIconPath.Data = Geometry.Parse("M12,4 C18,4 18,20 12,20 C6,20 6,4 12,4 Z");
                    PenIconPath.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 182, 193)); // Pink
                }
                else if (typeStr == "MagicStar")
                {
                    _mainWindow.SetMagicPenMode();
                    _mainWindow.SetMagicPenType(ParticleType.Star);
                    
                    // Change icon to magic pen
                    PenIconPath.Data = Geometry.Parse("M12 2l1.5 4h4l-3.5 2.5 1.5 4L12 10l-3.5 2.5 1.5-4L6.5 6h4z");
                    PenIconPath.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)); // Gold
                }
                PenPopup.IsOpen = false;
            }
        }

        private void BtnHighlighter_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SetHighlighterMode();
            UpdateToolHighlight(BtnHighlighter);
            GetPairedToolbar()?.SyncToolSelection("BtnHighlighter");
        }

        private void BtnEraser_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SetEraserMode();
            UpdateToolHighlight(BtnEraser);
            GetPairedToolbar()?.SyncToolSelection("BtnEraser");
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e) => _mainWindow.ClearCanvas();

        private void UpdateToolHighlight(System.Windows.Controls.Button selected)
        {
            // Deselect previous
            if (_selectedToolButton != null)
            {
                var prevBorder = _selectedToolButton.Template.FindName("ButtonBorder", _selectedToolButton) as Border;
                if (prevBorder != null)
                {
                    prevBorder.Background = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#184A90E2"));
                    prevBorder.BorderBrush = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#254A90E2"));
                    prevBorder.BorderThickness = new Thickness(1);
                }
            }

            _selectedToolButton = selected;

            // Highlight new with visible accent color
            if (_selectedToolButton != null)
            {
                _selectedToolButton.ApplyTemplate();
                var border = _selectedToolButton.Template.FindName("ButtonBorder", _selectedToolButton) as Border;
                if (border != null)
                {
                    border.Background = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#354A90E2"));
                    border.BorderBrush = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#604A90E2"));
                    border.BorderThickness = new Thickness(1.5);
                }
            }
        }

        // --- Brush Size ---

        private void BtnBrushSize_Click(object sender, RoutedEventArgs e)
        {
            BrushSizePopup.IsOpen = !BrushSizePopup.IsOpen;
        }

        private void BrushSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tagStr && double.TryParse(tagStr, out double size))
            {
                _mainWindow.SetBrushSize(size);
                BrushSizePopup.IsOpen = false;

                // Update popup highlight
                if (_selectedBrushSizeButton != null)
                {
                    var prevBorder = _selectedBrushSizeButton.Template.FindName("ButtonBorder", _selectedBrushSizeButton) as Border;
                    if (prevBorder != null)
                        prevBorder.Background = new SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#184A90E2"));
                }
                _selectedBrushSizeButton = btn;
                btn.ApplyTemplate();
                var border = btn.Template.FindName("ButtonBorder", btn) as Border;
                if (border != null)
                    border.Background = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#304A90E2"));

                // Update toolbar icon to reflect selected size
                double iconSize = size switch { 2 => 4, 4 => 8, 8 => 14, 12 => 20, _ => 8 };
                BrushSizeIcon.Width = iconSize;
                BrushSizeIcon.Height = iconSize;
                System.Windows.Controls.Canvas.SetLeft(BrushSizeIcon, (24 - iconSize) / 2);
                System.Windows.Controls.Canvas.SetTop(BrushSizeIcon, (24 - iconSize) / 2);

                GetPairedToolbar()?.SyncBrushSizeIcon(size);
            }
        }

        // --- Background ---

        private void BtnBackground_Click(object sender, RoutedEventArgs e)
        {
            BackgroundPopup.IsOpen = !BackgroundPopup.IsOpen;
        }

        private void BgTransparent_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SetBackground(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            BgIconRect.Fill = System.Windows.Media.Brushes.Transparent;
            BackgroundPopup.IsOpen = false;
            GetPairedToolbar()?.SyncBackgroundIcon(System.Windows.Media.Brushes.Transparent);
        }

        private void BgWhite_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SetBackground(System.Windows.Media.Colors.White);
            BgIconRect.Fill = System.Windows.Media.Brushes.White;
            BackgroundPopup.IsOpen = false;
            GetPairedToolbar()?.SyncBackgroundIcon(System.Windows.Media.Brushes.White);
        }

        private void BgBlack_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SetBackground(System.Windows.Media.Colors.Black);
            BgIconRect.Fill = System.Windows.Media.Brushes.Black;
            BackgroundPopup.IsOpen = false;
            GetPairedToolbar()?.SyncBackgroundIcon(System.Windows.Media.Brushes.Black);
        }

        // --- Colors ---

        private void BtnColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPopup.IsOpen = !ColorPopup.IsOpen;
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                _mainWindow.SetColor(brush.Color);
                UpdateColorHighlight(btn);
                ColorIcon.Fill = new SolidColorBrush(brush.Color);
                ColorPopup.IsOpen = false;
                GetPairedToolbar()?.SyncColorSelection(brush.Color);
            }
        }

        private void UpdateColorHighlight(System.Windows.Controls.Button? selected)
        {
            if (_selectedColorButton != null)
            {
                var prevBorder = _selectedColorButton.Template.FindName("ColorBorder", _selectedColorButton) as Border;
                if (prevBorder != null)
                {
                    prevBorder.BorderBrush = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#404A90E2"));
                    prevBorder.BorderThickness = new Thickness(1.5);
                }
            }

            _selectedColorButton = selected;

            if (_selectedColorButton != null)
            {
                var border = _selectedColorButton.Template.FindName("ColorBorder", _selectedColorButton) as Border;
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A90E2"));
                    border.BorderThickness = new Thickness(2.5);
                }
            }
        }

        private void HighlightMatchingColor(System.Windows.Media.Color color)
        {
            ColorIcon.Fill = new SolidColorBrush(color);
            foreach (var child in ColorPanel.Children)
            {
                if (child is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
                {
                    if (brush.Color == color)
                    {
                        btn.ApplyTemplate();
                        UpdateColorHighlight(btn);
                        return;
                    }
                }
            }
        }

        // --- Sync between paired toolbars ---

        private ToolbarWindow? GetPairedToolbar()
        {
            if (_primaryToolbar != null) return _primaryToolbar;
            return _secondaryToolbar;
        }

        public void SyncToolSelection(string toolName)
        {
            var btn = toolName switch
            {
                "BtnPen" => BtnPen,
                "BtnHighlighter" => BtnHighlighter,
                "BtnEraser" => BtnEraser,
                _ => null
            };
            if (btn != null) UpdateToolHighlight(btn);
        }

        public void SyncColorSelection(System.Windows.Media.Color color)
        {
            HighlightMatchingColor(color);
            ColorIcon.Fill = new SolidColorBrush(color);
        }

        public void SyncBrushSizeIcon(double size)
        {
            double iconSize = size switch { 2 => 4, 4 => 8, 8 => 14, 12 => 20, _ => 8 };
            BrushSizeIcon.Width = iconSize;
            BrushSizeIcon.Height = iconSize;
            System.Windows.Controls.Canvas.SetLeft(BrushSizeIcon, (24 - iconSize) / 2);
            System.Windows.Controls.Canvas.SetTop(BrushSizeIcon, (24 - iconSize) / 2);
        }

        public void SyncBackgroundIcon(System.Windows.Media.Brush fill)
        {
            BgIconRect.Fill = fill;
        }

        private void TxtAbout_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var version = UpdateService.GetCurrentVersion();
            var versionStr = $"{version.Major}.{version.Minor}.{version.Build}";
            System.Windows.MessageBox.Show($"Edulinker-Pen v{versionStr}\n\nMain Pen Icon Attribution:\n펜 아이콘 제작자: Those Icons - Flaticon\n(https://www.flaticon.com/kr/free-icons/)\n\n제작: schoolworks 팀\n관련문의: neohum77@gmail.com", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
