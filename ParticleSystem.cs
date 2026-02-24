using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace EdulinkerPen
{
    public enum ParticleType
    {
        Petal,
        Star
    }

    /// <summary>
    /// A simple particle system that emits falling flower petals or stars on a WPF Canvas.
    /// Activate by calling SetEmitPosition() when the user moves the cursor,
    /// and toggle IsEmitting on/off to start/stop particle generation.
    /// </summary>
    public class ParticleSystem
    {
        private readonly Canvas _canvas;
        private readonly Random _rng = new();
        private readonly List<Particle> _particles = new();
        private bool _isHooked = false;

        // How many new petals to spawn each frame while emitting
        private const int ParticlesPerFrame = 3;
        private Point _emitPos;

        // Petal colors – cherry blossom palette
        private static readonly Color[] PetalColors =
        {
            Color.FromRgb(255, 182, 193), // light pink
            Color.FromRgb(255, 105, 180), // hot pink
            Color.FromRgb(255, 220, 220), // pale pink
            Color.FromRgb(255, 165, 0),   // orange
            Color.FromRgb(255, 240, 150), // pale yellow
        };

        private bool _isEmitting;
        public bool IsEmitting
        {
            get => _isEmitting;
            set
            {
                _isEmitting = value;
                if (_isEmitting)
                {
                    Start();
                }
            }
        }
        
        public ParticleType CurrentType { get; set; } = ParticleType.Petal;

        public ParticleSystem(Canvas canvas)
        {
            _canvas = canvas;
        }

        public void SetEmitPosition(Point pos)
        {
            _emitPos = pos;
        }

        public void Start()
        {
            if (!_isHooked)
            {
                CompositionTarget.Rendering += OnRendering;
                _isHooked = true;
            }
        }

        public void Stop()
        {
            IsEmitting = false;
            // Keep ticking until all particles are gone, then unhook
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (IsEmitting)
            {
                for (int i = 0; i < ParticlesPerFrame; i++)
                    SpawnPetal();
            }

            // Update all particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Life += 1;
                p.Vy += 0.15;                      // gravity
                p.Vx += (p.TargetVx - p.Vx) * 0.05; // ease towards target Vx
                
                // Add flutter/sway
                double sway = Math.Sin(p.Life * p.SwaySpeed) * p.SwayAmount;
                
                p.X += p.Vx + sway;
                p.Y += p.Vy;
                p.Rotation += p.RotationSpeed;
                p.Opacity -= 0.012;

                if (p.Opacity <= 0 || p.Y > _canvas.ActualHeight + 50)
                {
                    _canvas.Children.Remove(p.Shape);
                    _particles.RemoveAt(i);
                    continue;
                }

                p.Shape.Opacity = p.Opacity;
                Canvas.SetLeft(p.Shape, p.X);
                Canvas.SetTop(p.Shape, p.Y);

                if (p.Shape.RenderTransform is RotateTransform rt)
                    rt.Angle = p.Rotation;
            }

            // Unhook when idle
            if (!IsEmitting && _particles.Count == 0)
            {
                CompositionTarget.Rendering -= OnRendering;
                _isHooked = false;
            }
        }

        private void SpawnPetal()
        {
            var color = PetalColors[_rng.Next(PetalColors.Length)];
            Shape shape;
            double size;

            if (CurrentType == ParticleType.Star)
            {
                size = 10 + _rng.NextDouble() * 10; // Stars are a bit bigger
                shape = new Path
                {
                    Data = Geometry.Parse("M12 2l1.5 4h4l-3.5 2.5 1.5 4L12 10l-3.5 2.5 1.5-4L6.5 6h4z"),
                    Fill = System.Windows.Media.Brushes.Gold,
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform,
                    Opacity = 1.0,
                    IsHitTestVisible = false
                };
            }
            else
            {
                size = 5 + _rng.NextDouble() * 8;
                shape = new Ellipse
                {
                    Width = size,
                    Height = size * 1.5,
                    Fill = new SolidColorBrush(color),
                    Opacity = 1.0,
                    IsHitTestVisible = false
                };
            }

            shape.RenderTransformOrigin = new Point(0.5, 0.5);
            shape.RenderTransform = new RotateTransform(_rng.NextDouble() * 360);

            double targetVx = (_rng.NextDouble() - 0.5) * 4;    // horizontal drift target
            double vy = -(_rng.NextDouble() * 4 + 1);      // initial upward burst

            var particle = new Particle
            {
                Shape = shape,
                X = _emitPos.X - size / 2,
                Y = _emitPos.Y - size / 2,
                Vx = targetVx * 2, // start faster, slow down
                TargetVx = targetVx,
                Vy = vy,
                Opacity = 1.0,
                Rotation = _rng.NextDouble() * 360,
                RotationSpeed = (_rng.NextDouble() - 0.5) * 8,
                SwaySpeed = 0.05 + _rng.NextDouble() * 0.1,
                SwayAmount = _rng.NextDouble() * 2.5
            };

            Canvas.SetLeft(shape, particle.X);
            Canvas.SetTop(shape, particle.Y);

            _canvas.Children.Add(shape);
            _particles.Add(particle);
        }

        public void Clear()
        {
            foreach (var p in _particles)
                _canvas.Children.Remove(p.Shape);
            _particles.Clear();
        }

        private class Particle
        {
            public UIElement Shape = null!;
            public double X, Y, Vx, Vy;
            public double TargetVx;
            public double Opacity;
            public double Rotation, RotationSpeed;
            public double SwaySpeed, SwayAmount;
            public int Life;
        }
    }
}
