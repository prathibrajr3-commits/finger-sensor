using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AirGestureAI.Services;

namespace AirGestureAI.Views
{
    /// <summary>
    /// Code-behind for <see cref="NotificationWindow"/>.
    /// Handles positioning in the bottom-right corner of the screen,
    /// setting custom border and icon properties based on <see cref="NotificationType"/>,
    /// and auto-dismiss timing.
    /// </summary>
    public partial class NotificationWindow : Window
    {
        private readonly DispatcherTimer _timer;

        /// <summary>
        /// Initialises the <see cref="NotificationWindow"/> and starts the auto-dismiss timer.
        /// </summary>
        public NotificationWindow(string title, string message, NotificationType type, int durationMs)
        {
            InitializeComponent();

            TitleText.Text   = title;
            MessageText.Text = message;

            ConfigureType(type);
            PositionWindow();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            _timer.Tick += (s, e) => { _timer.Stop(); Close(); };
            _timer.Start();
        }

        private void ConfigureType(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.GestureTrigger:
                    IconText.Text = "⚡";
                    RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x7B, 0x61, 0xFF)); // Purple
                    break;
                case NotificationType.Info:
                    IconText.Text = "ℹ️";
                    RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0xB7, 0xCA)); // Teal
                    break;
                case NotificationType.Warning:
                    IconText.Text = "⚠️";
                    RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x43)); // Orange
                    break;
                case NotificationType.Error:
                    IconText.Text = "❌";
                    RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)); // Red
                    break;
            }
        }

        private void PositionWindow()
        {
            // Position in bottom-right corner of primary screen working area (above taskbar)
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 10;
            Top  = workArea.Bottom - Height - 10;
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            Close();
        }
    }
}
