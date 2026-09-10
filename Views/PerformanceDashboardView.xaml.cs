using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using AirGestureAI.ViewModels;

namespace AirGestureAI.Views
{
    /// <summary>
    /// Normalises a double value against a maximum parameter for visual bar heights.
    /// </summary>
    public sealed class NormaliseConverter : IValueConverter
    {
        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double val && parameter is string paramStr && double.TryParse(paramStr, out double max))
            {
                // Max height of the bar is 80px
                double height = (val / max) * 80.0;
                return Math.Clamp(height, 2.0, 80.0);
            }
            return 2.0;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Interaction logic for PerformanceDashboardView.xaml
    /// </summary>
    public partial class PerformanceDashboardView : UserControl
    {
        /// <summary>
        /// Initialises a new instance of <see cref="PerformanceDashboardView"/>.
        /// </summary>
        /// <param name="viewModel">The ViewModel injected via DI.</param>
        public PerformanceDashboardView(PerformanceDashboardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
    }
}
