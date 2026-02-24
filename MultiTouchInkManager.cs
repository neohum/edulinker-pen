using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EdulinkerPen
{
    public class MultiTouchInkManager
    {
        private readonly InkCanvas _inkCanvas;
        private readonly Canvas _draftCanvas;
        private readonly DraftVisualHost _visualHost;

        // Active stroke points for rendering and finalized stroke creation
        private readonly Dictionary<int, StylusPointCollection> _activePoints = new();
        
        private const int MouseId = -1;

        public System.Windows.Media.Color CurrentColor { get; set; } = System.Windows.Media.Colors.Black;
        public double BrushSize { get; set; } = 4.0;
        public bool IsEraserMode { get; set; }
        public bool IsHighlighter { get; set; }

        public MultiTouchInkManager(InkCanvas inkCanvas, Canvas draftCanvas)
        {
            _inkCanvas = inkCanvas;
            _draftCanvas = draftCanvas;
            
            _visualHost = new DraftVisualHost();
            _draftCanvas.Children.Add(_visualHost);
        }

        private DrawingAttributes CreateDrawingAttributes()
        {
            return new DrawingAttributes
            {
                Color = CurrentColor,
                Width = BrushSize,
                Height = BrushSize,
                FitToCurve = false,
                StylusTip = StylusTip.Ellipse,
                IsHighlighter = IsHighlighter
            };
        }

        private void StartDrafting(int id, System.Windows.Point position)
        {
            var points = new StylusPointCollection(new[] { new StylusPoint(position.X, position.Y) });
            _activePoints[id] = points;
            
            // Draw initial dot
            _visualHost.RenderActiveStrokes(_activePoints, CurrentColor, BrushSize, IsHighlighter);
        }

        private void UpdateDrafting(int id, System.Windows.Point position)
        {
            if (_activePoints.TryGetValue(id, out var points))
            {
                points.Add(new StylusPoint(position.X, position.Y));
                _visualHost.RenderActiveStrokes(_activePoints, CurrentColor, BrushSize, IsHighlighter);
            }
        }

        private void EndDrafting(int id)
        {
            if (_activePoints.TryGetValue(id, out var points))
            {
                // Add the smooth final stroke to the InkCanvas collection
                if (points.Count > 0)
                {
                    var stroke = new Stroke(points, CreateDrawingAttributes());
                    _inkCanvas.Strokes.Add(stroke);
                }

                _activePoints.Remove(id);
                _visualHost.RenderActiveStrokes(_activePoints, CurrentColor, BrushSize, IsHighlighter);
            }
        }

        // --- DrawingVisual Host ---

        private class DraftVisualHost : FrameworkElement
        {
            private readonly DrawingVisual _visual;

            public DraftVisualHost()
            {
                _visual = new DrawingVisual();
                AddVisualChild(_visual);
                IsHitTestVisible = false; // Never block input
            }

            public void RenderActiveStrokes(Dictionary<int, StylusPointCollection> activePoints, System.Windows.Media.Color color, double brushSize, bool isHighlighter)
            {
                using var dc = _visual.RenderOpen();

                var brush = new SolidColorBrush(color);
                if (isHighlighter)
                {
                    brush.Opacity = 0.5;
                }
                brush.Freeze(); // Optimize

                var pen = new System.Windows.Media.Pen(brush, brushSize)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                pen.Freeze(); // Optimize

                foreach (var points in activePoints.Values)
                {
                    if (points.Count == 0) continue;
                    
                    if (points.Count == 1)
                    {
                        // Draw a dot for a single point
                        dc.DrawEllipse(brush, null, new System.Windows.Point(points[0].X, points[0].Y), brushSize / 2, brushSize / 2);
                        continue;
                    }

                    // Create a lightweight geometry for the stroke
                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        ctx.BeginFigure(new System.Windows.Point(points[0].X, points[0].Y), false, false);
                        var pointList = new List<System.Windows.Point>(points.Count - 1);
                        for (int i = 1; i < points.Count; i++)
                        {
                            pointList.Add(new System.Windows.Point(points[i].X, points[i].Y));
                        }
                        ctx.PolyLineTo(pointList, true, true);
                    }
                    geometry.Freeze(); // Crucial for performance
                    
                    dc.DrawGeometry(null, pen, geometry);
                }
            }

            protected override int VisualChildrenCount => 1;
            protected override Visual GetVisualChild(int index) => _visual;
        }

        // --- Mouse handling ---

        public void HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsEraserMode) return;
            if (e.StylusDevice != null) return; // Ignore promoted touch/pen events

            var position = e.GetPosition(_inkCanvas);
            StartDrafting(MouseId, position);
            _inkCanvas.CaptureMouse();
        }

        public void HandleMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (IsEraserMode) return;
            if (e.StylusDevice != null) return; // Ignore promoted touch/pen events
            if (e.LeftButton != MouseButtonState.Pressed) return;
            
            var position = e.GetPosition(_inkCanvas);
            UpdateDrafting(MouseId, position);
        }

        public void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.StylusDevice != null) return; // Ignore promoted touch/pen events
            EndDrafting(MouseId);
            _inkCanvas.ReleaseMouseCapture();
        }

        // --- Touch handling (multi-touch) ---

        public void HandleTouchDown(object sender, TouchEventArgs e)
        {
            if (IsEraserMode) return;

            e.Handled = true;
            int touchId = e.TouchDevice.Id;
            var position = e.GetTouchPoint(_inkCanvas).Position;
            
            StartDrafting(touchId, position);
        }

        public void HandleTouchMove(object sender, TouchEventArgs e)
        {
            if (IsEraserMode) return;

            int touchId = e.TouchDevice.Id;
            var position = e.GetTouchPoint(_inkCanvas).Position;
            
            UpdateDrafting(touchId, position);
            e.Handled = true;
        }

        public void HandleTouchUp(object sender, TouchEventArgs e)
        {
            int touchId = e.TouchDevice.Id;
            EndDrafting(touchId);
            e.Handled = true;
        }
    }
}
