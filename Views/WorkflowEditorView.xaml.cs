using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AirGestureAI.ViewModels;

namespace AirGestureAI.Views
{
    /// <summary>
    /// Code-behind for <see cref="WorkflowEditorView"/>.
    /// Wires node selection on mouse-click from the canvas.
    /// </summary>
    public partial class WorkflowEditorView : UserControl
    {
        /// <summary>
        /// Initialises a new instance of <see cref="WorkflowEditorView"/> with DI-injected ViewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel to bind to.</param>
        public WorkflowEditorView(WorkflowEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));

            // Register the node-selection command on the ViewModel
            viewModel.SelectNodeCommand = new RelayCommand(param =>
            {
                if (param is WorkflowNodeViewModel node)
                    viewModel.SelectedNode = node;
            });
        }
    }
}
