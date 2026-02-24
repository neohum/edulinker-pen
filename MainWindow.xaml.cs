using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.IO;
using System.Windows.Forms; // Using WinForms for NotifyIcon

namespace EdulinkerPen
{
    public partial class MainWindow : Window
    {
        private MultiTouchInkManager? _multiTouchManager;
        private ToolbarWindow? _toolbar;
        private System.Windows.Input.Cursor? _penCursor;
        private NotifyIcon? _notifyIcon;
        private readonly UpdateService _updateService = new();
        private UpdateInfo? _pendingUpdate;
        private System.Windows.Media.Color _lastColor = System.Windows.Media.Colors.Black;

        public MainWindow()
        {
            InitializeComponent();
            LoadCustomCursor();
        }

        private void LoadCustomCursor()
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "pen.cur");
                if (File.Exists(path))
                {
                    _penCursor = new System.Windows.Input.Cursor(path);
                }
            }
            catch 
            {
                // Fallback to default if icon fails to load
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            _multiTouchManager = new MultiTouchInkManager(MainCanvas);
            SetPenMode(); // Default to drawing mode

            // Launch the floating toolbar
            _toolbar = new ToolbarWindow(this);
            _toolbar.Show();
            
            SetupTrayIcon();
            _ = CheckForUpdateOnStartupAsync();
        }

        private async System.Threading.Tasks.Task CheckForUpdateOnStartupAsync()
        {
            try
            {
                var info = await _updateService.CheckForUpdateAsync();
                if (info == null) return;

                if (await _updateService.DownloadUpdateAsync(info))
                {
                    _pendingUpdate = info;
                    ShowUpdateBalloon(info);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Update check failed: {ex.Message}");
            }
        }

        private void ShowUpdateBalloon(UpdateInfo info)
        {
            if (_notifyIcon == null) return;

            _notifyIcon.BalloonTipTitle = "Edulinker-Pen 업데이트";
            _notifyIcon.BalloonTipText = $"새 버전 v{info.NewVersion}이 준비되었습니다. 클릭하여 업데이트하세요.";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(5000);
        }

        private void NotifyIcon_BalloonTipClicked(object? sender, EventArgs e)
        {
            PromptAndApplyUpdate();
        }

        private void PromptAndApplyUpdate()
        {
            if (_pendingUpdate == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Edulinker-Pen v{_pendingUpdate.NewVersion} 버전이 준비되었습니다.\n업데이트를 적용하시겠습니까?\n\n(앱이 재시작됩니다)",
                "업데이트 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _updateService.ApplyUpdate(_pendingUpdate);
            }
        }

        private async void CheckForUpdateManual_Click(object? sender, EventArgs e)
        {
            try
            {
                var info = await _updateService.CheckForUpdateAsync();
                if (info == null)
                {
                    System.Windows.MessageBox.Show(
                        "현재 최신 버전을 사용 중입니다.",
                        "업데이트 확인",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (await _updateService.DownloadUpdateAsync(info))
                {
                    _pendingUpdate = info;
                    PromptAndApplyUpdate();
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "업데이트 다운로드에 실패했습니다.\n나중에 다시 시도해 주세요.",
                        "업데이트 오류",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Manual update check failed: {ex.Message}");
                System.Windows.MessageBox.Show(
                    "업데이트 확인 중 오류가 발생했습니다.\n나중에 다시 시도해 주세요.",
                    "업데이트 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            
            try 
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "pen.ico");
                if (File.Exists(iconPath))
                    _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
                else
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            } 
            catch { _notifyIcon.Icon = System.Drawing.SystemIcons.Application; }
            
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Edulinker Pen";
            _notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;

            var ctxMenu = new ContextMenuStrip();
            var updateItem = new ToolStripMenuItem("업데이트 확인...");
            updateItem.Click += CheckForUpdateManual_Click;
            ctxMenu.Items.Add(updateItem);
            ctxMenu.Items.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem("edulinker-pen 종료");
            exitItem.Click += (s, args) => { System.Windows.Application.Current.Shutdown(); };
            ctxMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = ctxMenu;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _toolbar?.Close();
        }

        public void SetCursorMode()
        {
            Win32Interop.EnableClickThrough(this);
            MainCanvas.IsHitTestVisible = false;
            CustomCursorImage.Visibility = Visibility.Hidden;
        }

        public void HideCustomCursor()
        {
            CustomCursorImage.Visibility = Visibility.Hidden;
        }

        public void ShowCustomCursor()
        {
            if (MainCanvas.Cursor == System.Windows.Input.Cursors.None)
            {
                CustomCursorImage.Visibility = Visibility.Visible;
            }
        }

        public void SetPenMode()
        {
            Win32Interop.DisableClickThrough(this);
            MainCanvas.IsHitTestVisible = true;
            MainCanvas.Cursor = _penCursor ?? System.Windows.Input.Cursors.Pen;
            CustomCursorImage.Visibility = Visibility.Hidden;

            MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
            MainCanvas.DefaultDrawingAttributes.IsHighlighter = false;
            MainCanvas.DefaultDrawingAttributes.Color = _lastColor;
            MainCanvas.DefaultDrawingAttributes.Width = 4;
            MainCanvas.DefaultDrawingAttributes.Height = 4;

            if (_multiTouchManager != null)
            {
                _multiTouchManager.IsEraserMode = false;
                _multiTouchManager.CurrentColor = _lastColor;
                _multiTouchManager.BrushSize = 4;
            }
        }

        public void SetHighlighterMode()
        {
            Win32Interop.DisableClickThrough(this);
            MainCanvas.IsHitTestVisible = true;
            MainCanvas.Cursor = _penCursor ?? System.Windows.Input.Cursors.Pen;
            CustomCursorImage.Visibility = Visibility.Hidden;

            MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
            MainCanvas.DefaultDrawingAttributes.IsHighlighter = true;
            MainCanvas.DefaultDrawingAttributes.Width = 20;
            MainCanvas.DefaultDrawingAttributes.Height = 20;

            if (_multiTouchManager != null)
            {
                _multiTouchManager.IsEraserMode = false;
                _multiTouchManager.BrushSize = 20;
            }
        }

        public void SetEraserMode()
        {
            Win32Interop.DisableClickThrough(this);
            MainCanvas.IsHitTestVisible = true;
            MainCanvas.Cursor = System.Windows.Input.Cursors.Cross;
            CustomCursorImage.Visibility = Visibility.Hidden;

            MainCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
            if (_multiTouchManager != null)
                _multiTouchManager.IsEraserMode = true;
        }

        public void ClearCanvas()
        {
            MainCanvas.Strokes.Clear();
        }

        public void SetColor(System.Windows.Media.Color color)
        {
            _lastColor = color;
            MainCanvas.DefaultDrawingAttributes.Color = color;
            if (_multiTouchManager != null)
                _multiTouchManager.CurrentColor = color;
        }

        private void MainCanvas_TouchDown(object sender, TouchEventArgs e) => _multiTouchManager?.HandleTouchDown(sender, e);
        private void MainCanvas_TouchMove(object sender, TouchEventArgs e) => _multiTouchManager?.HandleTouchMove(sender, e);
        private void MainCanvas_TouchUp(object sender, TouchEventArgs e) => _multiTouchManager?.HandleTouchUp(sender, e);

        private void MainCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (CustomCursorImage.Visibility == Visibility.Visible)
            {
                var pos = e.GetPosition(CursorCanvas);
                // Adjusting the drawn icon so its "tip" (bottom-left) sits at the mouse coordinate
                Canvas.SetLeft(CustomCursorImage, pos.X);
                Canvas.SetTop(CustomCursorImage, pos.Y - CustomCursorImage.Height);
            }
        }

        private void MainCanvas_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (MainCanvas.Cursor == System.Windows.Input.Cursors.None)
            {
                CustomCursorImage.Visibility = Visibility.Visible;
            }
        }

        private void MainCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CustomCursorImage.Visibility = Visibility.Hidden;
        }
    }
}