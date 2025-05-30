using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace DeskBurst
{
    public partial class FireworksWindow : Window
    {
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static partial IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newStyle);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        private readonly Random _random = new();
        private readonly List<Firework> _fireworks = [];
        private readonly DispatcherTimer _timer;
        private readonly DateTime _startTime;
        private const int DISPLAY_TIME = 10000; // 10 seconds
        private readonly Screen _targetScreen;
        private int _initialFireworksRemaining = 15;
        private int _launchingFireworksRemaining = 10;

        private static readonly Color[] FireworkColors =
        [
            Colors.Red,
            Colors.White,
            Colors.Blue,
            Colors.Green,
            Colors.Gold,
            Colors.Purple,
            Colors.Orange,
            Colors.Pink
        ];

        public FireworksWindow(Screen targetScreen)
        {
            InitializeComponent();
            _startTime = DateTime.Now;
            _targetScreen = targetScreen;

            // Position window on the target screen
            var bounds = targetScreen.Bounds;
            this.Left = bounds.X;
            this.Top = bounds.Y;
            this.Width = bounds.Width;
            this.Height = bounds.Height;

            // Ensure the window is properly sized
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.SizeToContent = SizeToContent.Manual;

            Debug.WriteLine($"Creating fireworks window for screen: {bounds}");

            // Set up timer
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Add first batch of fireworks
            AddFirework(true);
            AddFirework(false);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Make the window click-through
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            if (exStyle == IntPtr.Zero)
            {
                exStyle = (IntPtr)(WS_EX_TRANSPARENT | WS_EX_LAYERED);
            }
            else
            {
                exStyle |= (IntPtr)(WS_EX_TRANSPARENT | WS_EX_LAYERED);
            }
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);

            var bounds = _targetScreen.Bounds;
            this.Left = bounds.X;
            this.Top = bounds.Y;
            this.Width = bounds.Width;
            this.Height = bounds.Height;

            Debug.WriteLine($"Window initialized for screen: {bounds}");
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if ((DateTime.Now - _startTime).TotalMilliseconds >= DISPLAY_TIME)
            {
                _timer.Stop();
                Close();
                return;
            }

            if (_initialFireworksRemaining > 0 && _random.Next(100) < 20)
            {
                AddFirework(true);
                _initialFireworksRemaining--;
            }

            if (_launchingFireworksRemaining > 0 && _random.Next(100) < 10)
            {
                AddFirework(false);
                _launchingFireworksRemaining--;
            }

            for (int i = _fireworks.Count - 1; i >= 0; i--)
            {
                var firework = _fireworks[i];
                firework.Update();

                if (firework.IsDead)
                {
                    _fireworks.RemoveAt(i);
                    if (_random.Next(100) < 70) // 70% chance to add a new firework
                    {
                        AddFirework(false);
                    }
                }
            }
        }

        private void AddFirework(bool explodeImmediately)
        {
            double x = _random.NextDouble() * ActualWidth;
            double y;

            if (explodeImmediately)
            {
                // For immediate fireworks, use the entire screen height
                y = _random.NextDouble() * ActualHeight;
            }
            else
            {
                // For launching fireworks, start from bottom with some variation
                y = ActualHeight - (_random.NextDouble() * 50); // Random height within 50 pixels from bottom
            }

            var firework = new Firework(x, y, _random, FireworksCanvas, FireworkColors[_random.Next(FireworkColors.Length)]);
            if (explodeImmediately)
            {
                firework.ExplodeImmediately();
            }
            _fireworks.Add(firework);
        }
    }

    public class Firework
    {
        private readonly List<Particle> _particles = [];
        private readonly Random _random;
        private readonly double _x;
        private double _y;
        private bool _exploded;
        private double _velocityY;
        private readonly Color _color;
        private readonly Canvas _canvas;
        private int _updateCount = 0;
        private const int MAX_UPDATES_BEFORE_EXPLODE = 100; // Safety limit
        private readonly Ellipse _dot;

        public bool IsDead => _exploded && _particles.Count == 0;

        public Firework(double x, double y, Random random, Canvas canvas, Color color)
        {
            _x = x;
            _y = y;
            _random = random;
            _canvas = canvas;
            _velocityY = -20 - random.Next(8);
            _color = color;

            _dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(_color)
            };
            Canvas.SetLeft(_dot, _x - 4);
            Canvas.SetTop(_dot, _y - 4);
            _canvas.Children.Add(_dot);
        }

        public void ExplodeImmediately()
        {
            _exploded = true;
            Explode();
        }

        public void Update()
        {
            if (!_exploded)
            {
                _y += _velocityY;
                _velocityY += 0.35;
                _updateCount++;

                Canvas.SetLeft(_dot, _x - 4);
                Canvas.SetTop(_dot, _y - 4);

                // Force explosion if it's been too long or reached the top
                if (_velocityY >= 0 || _updateCount >= MAX_UPDATES_BEFORE_EXPLODE)
                {
                    Explode();
                }
            }
            else
            {
                for (int i = _particles.Count - 1; i >= 0; i--)
                {
                    var particle = _particles[i];
                    particle.Update();

                    if (particle.IsDead)
                    {
                        _particles.RemoveAt(i);
                    }
                }
            }
        }

        private void Explode()
        {
            _exploded = true;
            _canvas.Children.Remove(_dot);

            for (int i = 0; i < 150; i++)
            {
                double angle = _random.NextDouble() * Math.PI * 2;
                double speed = 3 + _random.NextDouble() * 5;
                _particles.Add(new Particle(
                    _x,
                    _y,
                    Math.Cos(angle) * speed,
                    Math.Sin(angle) * speed,
                    _color,
                    _canvas
                ));
            }
        }
    }

    public class Particle
    {
        private double _x, _y;
        private readonly double _velocityX;
        private double _velocityY;
        private readonly Color _color;
        private double _life = 1.0;
        private readonly Ellipse _ellipse;
        private readonly Canvas _canvas;
        private const double MAX_LIFE = 1.0;

        public bool IsDead => _life <= 0;

        public Particle(double x, double y, double velocityX, double velocityY, Color color, Canvas canvas)
        {
            _x = x;
            _y = y;
            _velocityX = velocityX;
            _velocityY = velocityY;
            _color = color;
            _canvas = canvas;

            _ellipse = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(color)
            };
            Canvas.SetLeft(_ellipse, _x - 2);
            Canvas.SetTop(_ellipse, _y - 2);
            _canvas.Children.Add(_ellipse);
        }

        public void Update()
        {
            _x += _velocityX;
            _y += _velocityY;
            _velocityY += 0.1; // gravity
            _life -= 0.015; // Slower fade out

            Canvas.SetLeft(_ellipse, _x - 2);
            Canvas.SetTop(_ellipse, _y - 2);

            var brush = (SolidColorBrush)_ellipse.Fill;
            brush.Color = Color.FromArgb(
                (byte)(255 * (_life / MAX_LIFE)),
                _color.R,
                _color.G,
                _color.B
            );

            if (IsDead)
            {
                _canvas.Children.Remove(_ellipse);
            }
        }
    }
}