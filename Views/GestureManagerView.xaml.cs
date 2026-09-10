using AirGestureAI.ViewModels;

namespace AirGestureAI.Views
{
    /// <summary>
    /// Code-behind for <see cref="GestureManagerView"/>.
    /// The view is purely data-bound; no logic lives here.
    /// </summary>
    public partial class GestureManagerView : System.Windows.Controls.UserControl
    {
        /// <summary>
        /// Initialises a new instance of <see cref="GestureManagerView"/>.
        /// </summary>
        /// <param name="viewModel">The ViewModel injected via DI.</param>
        public GestureManagerView(GestureManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        }
    }
}
