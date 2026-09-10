using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AirGestureAI.Configuration;

namespace AirGestureAI.Cursor
{
    /// <summary>
    /// Transparent, borderless, always-on-top overlay window that renders the virtual cursor.
    /// Click-through is achieved by setting WS_EX_TRANSPARENT at the Win32 layer so all mouse
    /// events pass through to the application beneath the overlay.
    /// </summary>
    public partial class CursorWindow : Window
    {
        // ── Win32 constants ────────────────────────────────────────────────────
        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW  = 0x00000080;
        private const int WS_EX_NOACTIVATE  = 0x08000000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        // ── Configuration ──────────────────────────────────────────────────────
        private readonly AppConfig _config;

        // ── Animation guards ──────────────────────────────────────────────────
        private DoubleAnimation? _activeAnimation;
        private bool _isFadingIn;
        private bool _isFadingOut;

        // ── Rendering tick for cursor position ────────────────────────────────
        // Half the window width/height; used to centre the window on a screen coordinate.
        private double _halfWidth;
        private double _halfHeight;

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Raised when the fade-in animation completes.</summary>
        public event EventHandler? FadeInCompleted;

        /// <summary>Raised when the fade-out animation completes.</summary>
        public event EventHandler? FadeOutCompleted;

        /// <summary>
        /// Initializes the overlay window and binds configuration-driven visual properties.
        /// </summary>
        public CursorWindow(AppConfig config)
        {
            _config = config;
            InitializeComponent();
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
        }

        // ── Initialisation ────────────────────────────────────────────────────

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            // Apply Win32 extended window styles:
            // WS_EX_TRANSPARENT – passes all mouse events through.
            // WS_EX_TOOLWINDOW  – hides from Alt+Tab switcher.
            // WS_EX_NOACTIVATE  – prevents window from stealing focus on show.
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _halfWidth  = Width  / 2.0;
            _halfHeight = Height / 2.0;

            // Apply configurable stroke thickness.
            CursorEllipse.StrokeThickness = _config.CursorStrokeThickness;

            // Apply configurable cursor size (keeps shadow/highlight centred).
            double size   = _config.CursorSize;
            double pad    = (Width - size) / 2.0;
            CursorEllipse.Width  = size;
            CursorEllipse.Height = size;
            System.Windows.Controls.Canvas.SetLeft(CursorEllipse, pad);
            System.Windows.Controls.Canvas.SetTop(CursorEllipse,  pad);

            CursorShadow.Width  = size;
            CursorShadow.Height = size;
            System.Windows.Controls.Canvas.SetLeft(CursorShadow, pad);
            System.Windows.Controls.Canvas.SetTop(CursorShadow,  pad);

            // Apply configurable fill opacity to the gradient stops.
            double alpha = _config.CursorFillOpacity;
            FillGradientCenter.Color = ChangeAlpha(FillGradientCenter.Color, (byte)(alpha * 255));
            FillGradientEdge.Color   = ChangeAlpha(FillGradientEdge.Color,   (byte)(alpha * 0.55 * 255));

            Utilities.Logger.Info("CursorWindow loaded and visual properties applied.");
        }

        private static Color ChangeAlpha(Color c, byte alpha) =>
            Color.FromArgb(alpha, c.R, c.G, c.B);

        /// <summary>
        /// Moves the overlay window so that its centre is at <paramref name="screenPoint"/>
        /// (device-independent pixels).
        /// </summary>
        public void UpdatePosition(Point screenPoint)
        {
            Left = screenPoint.X - _halfWidth;
            Top  = screenPoint.Y - _halfHeight;
        }

        // ── Visual Color State Transitions ────────────────────────────────────

        /// <summary>
        /// Smoothly transitions the cursor colors to reflect the current cursor state:
        /// <list type="bullet">
        ///   <item>Blue: Ready / Tracking</item>
        ///   <item>Yellow: Hovering / Counting</item>
        ///   <item>Green: Selected</item>
        ///   <item>Red: Hand Lost</item>
        /// </list>
        /// </summary>
        public void SetState(AirGestureAI.Models.CursorState state)
        {
            Dispatcher.VerifyAccess();

            Color strokeColor;
            Color centerFillColor;
            Color edgeFillColor;
            Color shadowColor;

            double alpha = _config.CursorFillOpacity;

            switch (state)
            {
                case AirGestureAI.Models.CursorState.Hovering:
                    strokeColor     = Color.FromRgb(0xFF, 0xC1, 0x07); // Yellow
                    centerFillColor = Color.FromArgb((byte)(alpha * 255), 0xFF, 0xC1, 0x07);
                    edgeFillColor   = Color.FromArgb((byte)(alpha * 0.55 * 255), 0xB3, 0x86, 0x00);
                    shadowColor     = Color.FromRgb(0xFF, 0xC1, 0x07);
                    break;

                case AirGestureAI.Models.CursorState.Selected:
                    strokeColor     = Color.FromRgb(0x19, 0x87, 0x54); // Green
                    centerFillColor = Color.FromArgb((byte)(alpha * 255), 0x19, 0x87, 0x54);
                    edgeFillColor   = Color.FromArgb((byte)(alpha * 0.55 * 255), 0x0E, 0x52, 0x32);
                    shadowColor     = Color.FromRgb(0x19, 0x87, 0x54);
                    break;

                case AirGestureAI.Models.CursorState.HandLost:
                    strokeColor     = Color.FromRgb(0xDC, 0x35, 0x45); // Red
                    centerFillColor = Color.FromArgb((byte)(alpha * 255), 0xDC, 0x35, 0x45);
                    edgeFillColor   = Color.FromArgb((byte)(alpha * 0.55 * 255), 0x84, 0x20, 0x29);
                    shadowColor     = Color.FromRgb(0xDC, 0x35, 0x45);
                    break;

                case AirGestureAI.Models.CursorState.Ready:
                case AirGestureAI.Models.CursorState.Tracking:
                case AirGestureAI.Models.CursorState.Appearing:
                default:
                    strokeColor     = Color.FromRgb(0x0D, 0x6E, 0xFD); // Blue
                    centerFillColor = Color.FromArgb((byte)(alpha * 255), 0x0D, 0x6E, 0xFD);
                    edgeFillColor   = Color.FromArgb((byte)(alpha * 0.55 * 255), 0x0A, 0x4F, 0xC4);
                    shadowColor     = Color.FromRgb(0x1A, 0x6B, 0xD9);
                    break;
            }

            var duration   = TimeSpan.FromMilliseconds(200);
            var strokeAnim = new ColorAnimation(strokeColor, duration);
            var centerAnim = new ColorAnimation(centerFillColor, duration);
            var edgeAnim   = new ColorAnimation(edgeFillColor, duration);
            var shadowAnim = new ColorAnimation(shadowColor, duration);

            CursorStrokeBrush.BeginAnimation(SolidColorBrush.ColorProperty, strokeAnim);
            FillGradientCenter.BeginAnimation(GradientStop.ColorProperty, centerAnim);
            FillGradientEdge.BeginAnimation(GradientStop.ColorProperty, edgeAnim);
            ShadowEffect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, shadowAnim);
        }

        // ── Visibility / Fade ─────────────────────────────────────────────────

        /// <summary>
        /// Fades the cursor in. If <paramref name="immediate"/> is <see langword="true"/>
        /// the cursor appears instantly with no animation.
        /// </summary>
        public void FadeIn(bool immediate = false)
        {
            if (_isFadingIn) return; // already appearing

            _isFadingOut = false;
            _isFadingIn  = true;

            if (!IsVisible) Show();

            if (immediate)
            {
                StopCurrentAnimation();
                Opacity     = 1.0;
                _isFadingIn = false;
                FadeInCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            var anim = new DoubleAnimation
            {
                To           = 1.0,
                Duration     = TimeSpan.FromMilliseconds(_config.CursorFadeInMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            anim.Completed += (_, _) =>
            {
                _isFadingIn = false;
                FadeInCompleted?.Invoke(this, EventArgs.Empty);
            };

            StopCurrentAnimation();
            _activeAnimation = anim;
            BeginAnimation(OpacityProperty, anim);
        }

        /// <summary>
        /// Fades the cursor out and hides the window once fully transparent.
        /// </summary>
        public void FadeOut()
        {
            if (_isFadingOut) return; // already disappearing

            _isFadingIn  = false;
            _isFadingOut = true;

            var anim = new DoubleAnimation
            {
                To           = 0.0,
                Duration     = TimeSpan.FromMilliseconds(_config.CursorFadeOutMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.HoldEnd
            };

            anim.Completed += (_, _) =>
            {
                _isFadingOut = false;
                Hide();
                Opacity = 0.0;
                FadeOutCompleted?.Invoke(this, EventArgs.Empty);
            };

            StopCurrentAnimation();
            _activeAnimation = anim;
            BeginAnimation(OpacityProperty, anim);
        }

        /// <summary>
        /// Instantly hides the cursor without animation.
        /// </summary>
        public void HideImmediately()
        {
            StopCurrentAnimation();
            _isFadingIn  = false;
            _isFadingOut = false;
            Opacity = 0.0;
            Hide();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void StopCurrentAnimation()
        {
            if (_activeAnimation != null)
            {
                // Freeze opacity at current value before stopping.
                double current = Opacity;
                BeginAnimation(OpacityProperty, null);
                Opacity = current;
                _activeAnimation = null;
            }
        }
    }
}
