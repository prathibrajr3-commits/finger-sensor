using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AirGestureAI.ViewModels;

namespace AirGestureAI.Views
{
    // ── Value Converters ──────────────────────────────────────────────────────

    /// <summary>
    /// Converts the current wizard step index to <see cref="Visibility.Visible"/>
    /// for a specific target step, and <see cref="Visibility.Collapsed"/> for all others.
    /// </summary>
    public sealed class IndexToVisibilityConverter : IValueConverter
    {
        private readonly int _targetIndex;

        private IndexToVisibilityConverter(int index) => _targetIndex = index;

        /// <summary>Gets the converter instance for wizard step 0 (Welcome).</summary>
        public static IndexToVisibilityConverter Step0 { get; } = new(0);

        /// <summary>Gets the converter instance for wizard step 1 (Environment).</summary>
        public static IndexToVisibilityConverter Step1 { get; } = new(1);

        /// <summary>Gets the converter instance for wizard step 2 (Calibration).</summary>
        public static IndexToVisibilityConverter Step2 { get; } = new(2);

        /// <summary>Gets the converter instance for wizard step 3 (Gestures).</summary>
        public static IndexToVisibilityConverter Step3 { get; } = new(3);

        /// <summary>Gets the converter instance for wizard step 4 (Complete).</summary>
        public static IndexToVisibilityConverter Step4 { get; } = new(4);

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is int i && i == _targetIndex ? Visibility.Visible : Visibility.Collapsed;

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Converts a bool to <see cref="Visibility.Visible"/> when <c>true</c>.</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>Gets the singleton instance.</summary>
        public static BoolToVisibilityConverter Instance { get; } = new();

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Converts a bool to <see cref="Visibility.Collapsed"/> when <c>true</c> (inverse).</summary>
    public sealed class BoolToInverseVisibilityConverter : IValueConverter
    {
        /// <summary>Gets the singleton instance.</summary>
        public static BoolToInverseVisibilityConverter Instance { get; } = new();

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Collapsed : Visibility.Visible;

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    // ── Code-Behind ───────────────────────────────────────────────────────────

    /// <summary>
    /// Code-behind for <see cref="SetupWizardWindow"/>.
    /// Wires up <see cref="SetupWizardViewModel.WizardCompleted"/> to close the window.
    /// </summary>
    public partial class SetupWizardWindow : Window
    {
        /// <summary>
        /// Initialises a new instance of <see cref="SetupWizardWindow"/> with the given ViewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel injected via DI.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="viewModel"/> is null.</exception>
        public SetupWizardWindow(SetupWizardViewModel viewModel)
        {
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            DataContext = viewModel;

            viewModel.WizardCompleted += () =>
            {
                Dispatcher.Invoke(() => DialogResult = true);
            };
        }
    }
}
