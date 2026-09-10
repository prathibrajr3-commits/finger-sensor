using AirGestureAI.ViewModels;

namespace AirGestureAI.Views
{
    /// <summary>
    /// Code-behind for <see cref="PluginBrowserView"/>.
    /// </summary>
    public partial class PluginBrowserView : System.Windows.Controls.UserControl
    {
        /// <summary>
        /// Initialises a new instance of <see cref="PluginBrowserView"/>.
        /// </summary>
        /// <param name="viewModel">The ViewModel injected via DI.</param>
        public PluginBrowserView(PluginBrowserViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        }
    }
}
