using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace EdulinkerPen
{
    public class MultiTouchInkManager
    {
        private readonly InkCanvas _inkCanvas;
        private readonly Dictionary<int, Stroke> _activeStrokes;
        
        public System.Windows.Media.Color CurrentColor { get; set; } = Colors.Red;
        public double BrushSize { get; set; } = 4.0;
        public bool IsEraserMode { get; set; } = false;

        public MultiTouchInkManager(InkCanvas inkCanvas)
        {
            _inkCanvas = inkCanvas;
            _activeStrokes = new Dictionary<int, Stroke>();
        }

        public void HandleTouchDown(object sender, TouchEventArgs e)
        {
            if (IsEraserMode) return; // For MVP, touch erasing might be complex, default to single mouse erase initially or implement later

            e.Handled = true;
            int touchId = e.TouchDevice.Id;
            
            System.Windows.Point position = e.GetTouchPoint(_inkCanvas).Position;
            var points = new StylusPointCollection(new[] { new StylusPoint(position.X, position.Y) });
            
            var drawingAttributes = new DrawingAttributes
            {
                Color = CurrentColor,
                Width = BrushSize,
                Height = BrushSize,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse
            };

            var newStroke = new Stroke(points, drawingAttributes);
            _activeStrokes[touchId] = newStroke;
            _inkCanvas.Strokes.Add(newStroke);
        }

        public void HandleTouchMove(object sender, TouchEventArgs e)
        {
            int touchId = e.TouchDevice.Id;
            if (_activeStrokes.TryGetValue(touchId, out Stroke? stroke))
            {
                e.Handled = true;
                System.Windows.Point position = e.GetTouchPoint(_inkCanvas).Position;
                stroke.StylusPoints.Add(new StylusPoint(position.X, position.Y));
            }
        }

        public void HandleTouchUp(object sender, TouchEventArgs e)
        {
            int touchId = e.TouchDevice.Id;
            if (_activeStrokes.ContainsKey(touchId))
            {
                _activeStrokes.Remove(touchId);
                e.Handled = true;
            }
        }
    }
}
