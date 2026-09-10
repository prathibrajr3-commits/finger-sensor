using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using AirGestureAI.Utilities;

namespace AirGestureAI.ViewModels
{
    // ── Node Types ─────────────────────────────────────────────────────────────

    /// <summary>Specifies the kind of node in a visual workflow.</summary>
    public enum WorkflowNodeType
    {
        /// <summary>Performs a keyboard or mouse action.</summary>
        Action,
        /// <summary>Inserts a configurable delay.</summary>
        Delay,
        /// <summary>Branches on a condition.</summary>
        Condition,
        /// <summary>Repeats child nodes a specified number of times.</summary>
        Loop,
    }

    // ── Workflow Node ViewModel ────────────────────────────────────────────────

    /// <summary>Represents a single node in the visual workflow editor canvas.</summary>
    public sealed class WorkflowNodeViewModel : ViewModelBase
    {
        private string           _label      = string.Empty;
        private string           _parameter  = string.Empty;
        private WorkflowNodeType _nodeType   = WorkflowNodeType.Action;
        private bool             _isSelected;
        private double           _canvasX;
        private double           _canvasY;

        /// <summary>Gets the unique node identifier.</summary>
        public string Id { get; } = Guid.NewGuid().ToString();

        /// <summary>Gets or sets the display label for this node.</summary>
        public string Label { get => _label; set => SetField(ref _label, value); }

        /// <summary>Gets or sets the action parameter (shortcut, delay ms, condition expression, loop count).</summary>
        public string Parameter { get => _parameter; set => SetField(ref _parameter, value); }

        /// <summary>Gets or sets the node type.</summary>
        public WorkflowNodeType NodeType { get => _nodeType; set { SetField(ref _nodeType, value); OnPropertyChanged(nameof(NodeIcon)); } }

        /// <summary>Gets whether this node is currently selected on the canvas.</summary>
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        /// <summary>Gets or sets the horizontal canvas position (px from left).</summary>
        public double CanvasX { get => _canvasX; set => SetField(ref _canvasX, value); }

        /// <summary>Gets or sets the vertical canvas position (px from top).</summary>
        public double CanvasY { get => _canvasY; set => SetField(ref _canvasY, value); }

        /// <summary>Gets the emoji icon for the node type.</summary>
        public string NodeIcon => NodeType switch
        {
            WorkflowNodeType.Action    => "⚡",
            WorkflowNodeType.Delay     => "⏱️",
            WorkflowNodeType.Condition => "❓",
            WorkflowNodeType.Loop      => "🔁",
            _                          => "•"
        };

        /// <summary>Gets the accent colour for the node header based on type.</summary>
        public string NodeColor => NodeType switch
        {
            WorkflowNodeType.Action    => "#FF7B61FF",
            WorkflowNodeType.Delay     => "#FF2AB7CA",
            WorkflowNodeType.Condition => "#FFFF9F43",
            WorkflowNodeType.Loop      => "#FF26DE81",
            _                          => "#FF888888"
        };
    }

    // ── Workflow Connection ────────────────────────────────────────────────────

    /// <summary>Represents a directed edge between two nodes in the workflow canvas.</summary>
    public sealed class WorkflowConnectionViewModel : ViewModelBase
    {
        /// <summary>Gets or sets the source node identifier.</summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>Gets or sets the target node identifier.</summary>
        public string TargetId { get; set; } = string.Empty;
    }

    // ── Workflow ViewModel ─────────────────────────────────────────────────────

    /// <summary>
    /// ViewModel for the visual Workflow Editor.
    /// Supports adding, removing, connecting, and reordering nodes via drag-and-drop.
    /// Persists workflows as JSON via <see cref="WorkflowEditorService"/>.
    /// </summary>
    public sealed class WorkflowEditorViewModel : ViewModelBase
    {
        private WorkflowNodeViewModel? _selectedNode;
        private string                  _workflowName = "New Workflow";
        private string                  _statusMessage = string.Empty;
        private bool                    _isBusy;

        /// <summary>Initialises the <see cref="WorkflowEditorViewModel"/>.</summary>
        public WorkflowEditorViewModel()
        {
            Nodes       = new ObservableCollection<WorkflowNodeViewModel>();
            Connections = new ObservableCollection<WorkflowConnectionViewModel>();

            AddActionCommand    = new RelayCommand(_ => AddNode(WorkflowNodeType.Action));
            AddDelayCommand     = new RelayCommand(_ => AddNode(WorkflowNodeType.Delay));
            AddConditionCommand = new RelayCommand(_ => AddNode(WorkflowNodeType.Condition));
            AddLoopCommand      = new RelayCommand(_ => AddNode(WorkflowNodeType.Loop));
            DeleteNodeCommand   = new RelayCommand(_ => DeleteSelected(), _ => SelectedNode is not null);
            ClearCommand        = new RelayCommand(_ => ClearCanvas());
            SaveCommand         = new RelayCommand(async _ => await SaveWorkflowAsync());
            LoadCommand         = new RelayCommand(async _ => await LoadWorkflowAsync());
            MoveUpCommand       = new RelayCommand(_ => MoveSelectedUp(),   _ => CanMoveUp);
            MoveDownCommand     = new RelayCommand(_ => MoveSelectedDown(), _ => CanMoveDown);
        }

        // ── Bindable Properties ───────────────────────────────────────────────

        /// <summary>Gets the collection of workflow nodes on the canvas.</summary>
        public ObservableCollection<WorkflowNodeViewModel> Nodes { get; }

        /// <summary>Gets the connections between nodes.</summary>
        public ObservableCollection<WorkflowConnectionViewModel> Connections { get; }

        /// <summary>Gets or sets the currently selected node.</summary>
        public WorkflowNodeViewModel? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode is not null) _selectedNode.IsSelected = false;
                SetField(ref _selectedNode, value);
                if (_selectedNode is not null) _selectedNode.IsSelected = true;
                OnPropertyChanged(nameof(CanMoveUp));
                OnPropertyChanged(nameof(CanMoveDown));
            }
        }

        /// <summary>Gets or sets the workflow name.</summary>
        public string WorkflowName
        {
            get => _workflowName;
            set => SetField(ref _workflowName, value);
        }

        /// <summary>Gets or sets the status message at the bottom of the editor.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        /// <summary>Gets or sets whether a long-running operation is active.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetField(ref _isBusy, value);
        }

        /// <summary>Gets whether the selected node can be moved up in sequence.</summary>
        public bool CanMoveUp => SelectedNode is not null && Nodes.IndexOf(SelectedNode) > 0;

        /// <summary>Gets whether the selected node can be moved down in sequence.</summary>
        public bool CanMoveDown => SelectedNode is not null && Nodes.IndexOf(SelectedNode) < Nodes.Count - 1;

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Adds an Action node.</summary>  public ICommand AddActionCommand    { get; }
        public ICommand AddActionCommand    { get; }

        /// <summary>Adds a Delay node.</summary>
        public ICommand AddDelayCommand     { get; }

        /// <summary>Adds a Condition node.</summary>
        public ICommand AddConditionCommand { get; }

        /// <summary>Adds a Loop node.</summary>
        public ICommand AddLoopCommand      { get; }

        /// <summary>Deletes the selected node and its connections.</summary>
        public ICommand DeleteNodeCommand   { get; }

        /// <summary>Clears all nodes from the canvas.</summary>
        public ICommand ClearCommand        { get; }

        /// <summary>Saves the workflow to disk.</summary>
        public ICommand SaveCommand         { get; }

        /// <summary>Opens a workflow from disk.</summary>
        public ICommand LoadCommand         { get; }

        /// <summary>Moves the selected node up in the sequence.</summary>
        public ICommand MoveUpCommand       { get; }

        /// <summary>Moves the selected node down in the sequence.</summary>
        public ICommand MoveDownCommand     { get; }

        /// <summary>
        /// Selects a node from the canvas. Set by the code-behind after initialisation
        /// to allow the view to pass click targets to the ViewModel.
        /// </summary>
        public ICommand? SelectNodeCommand  { get; set; }

        // ── Node management ───────────────────────────────────────────────────

        private void AddNode(WorkflowNodeType type)
        {
            double offsetX = 60 + (Nodes.Count % 5) * 160;
            double offsetY = 60 + (Nodes.Count / 5) * 120;

            var node = new WorkflowNodeViewModel
            {
                NodeType  = type,
                Label     = GetDefaultLabel(type),
                Parameter = GetDefaultParameter(type),
                CanvasX   = offsetX,
                CanvasY   = offsetY,
            };
            Nodes.Add(node);

            // Auto-connect to the previous node
            if (Nodes.Count > 1)
            {
                var prev = Nodes[Nodes.Count - 2];
                Connections.Add(new WorkflowConnectionViewModel { SourceId = prev.Id, TargetId = node.Id });
            }

            SelectedNode  = node;
            StatusMessage = $"Added {type} node: '{node.Label}'";
        }

        private void DeleteSelected()
        {
            if (_selectedNode is null) return;
            var id = _selectedNode.Id;
            Nodes.Remove(_selectedNode);

            // Remove all connections referencing this node
            var toRemove = Connections.Where(c => c.SourceId == id || c.TargetId == id).ToList();
            foreach (var c in toRemove) Connections.Remove(c);

            SelectedNode  = null;
            StatusMessage = "Node deleted.";
        }

        private void ClearCanvas()
        {
            Nodes.Clear();
            Connections.Clear();
            SelectedNode  = null;
            StatusMessage = "Canvas cleared.";
        }

        private void MoveSelectedUp()
        {
            if (_selectedNode is null) return;
            int idx = Nodes.IndexOf(_selectedNode);
            if (idx <= 0) return;
            Nodes.Move(idx, idx - 1);
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
        }

        private void MoveSelectedDown()
        {
            if (_selectedNode is null) return;
            int idx = Nodes.IndexOf(_selectedNode);
            if (idx >= Nodes.Count - 1) return;
            Nodes.Move(idx, idx + 1);
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private async Task SaveWorkflowAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Save Workflow",
                Filter     = "Workflow JSON|*.workflow.json",
                FileName   = $"{WorkflowName}.workflow.json",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() != true) return;

            IsBusy = true;
            StatusMessage = "Saving workflow…";
            try
            {
                var payload = new
                {
                    Name        = WorkflowName,
                    SavedAt     = DateTime.UtcNow,
                    Nodes       = Nodes.Select(n => new
                    {
                        n.Id, n.Label, n.Parameter, n.NodeType, n.CanvasX, n.CanvasY
                    }),
                    Connections = Connections.Select(c => new { c.SourceId, c.TargetId })
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(dlg.FileName, json);
                StatusMessage = $"Saved to {dlg.FileName}";
                Logger.Info($"WorkflowEditor: Saved '{WorkflowName}' to {dlg.FileName}");
            }
            catch (Exception ex)
            {
                Logger.Error("WorkflowEditorViewModel: SaveWorkflowAsync failed", ex);
                StatusMessage = $"Save failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task LoadWorkflowAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Open Workflow",
                Filter = "Workflow JSON|*.workflow.json|JSON Files|*.json"
            };
            if (dlg.ShowDialog() != true) return;

            IsBusy = true;
            StatusMessage = "Loading workflow…";
            try
            {
                var json = await System.IO.File.ReadAllTextAsync(dlg.FileName);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                ClearCanvas();
                WorkflowName = root.TryGetProperty("Name", out var nameEl) ? nameEl.GetString() ?? "Workflow" : "Workflow";

                // Load nodes
                var nodeMap = new Dictionary<string, WorkflowNodeViewModel>();
                if (root.TryGetProperty("Nodes", out var nodesEl))
                {
                    foreach (var n in nodesEl.EnumerateArray())
                    {
                        var node = new WorkflowNodeViewModel
                        {
                            Label     = n.TryGetProperty("Label",     out var l) ? l.GetString() ?? ""   : "",
                            Parameter = n.TryGetProperty("Parameter", out var p) ? p.GetString() ?? ""   : "",
                            NodeType  = n.TryGetProperty("NodeType",  out var t)
                                            ? Enum.Parse<WorkflowNodeType>(t.GetString() ?? "Action")
                                            : WorkflowNodeType.Action,
                            CanvasX   = n.TryGetProperty("CanvasX",   out var x) ? x.GetDouble() : 60,
                            CanvasY   = n.TryGetProperty("CanvasY",   out var y) ? y.GetDouble() : 60,
                        };
                        Nodes.Add(node);
                        if (n.TryGetProperty("Id", out var idEl)) nodeMap[idEl.GetString() ?? ""] = node;
                    }
                }

                // Load connections
                if (root.TryGetProperty("Connections", out var connEl))
                {
                    foreach (var c in connEl.EnumerateArray())
                    {
                        Connections.Add(new WorkflowConnectionViewModel
                        {
                            SourceId = c.TryGetProperty("SourceId", out var s) ? s.GetString() ?? "" : "",
                            TargetId = c.TryGetProperty("TargetId", out var tg) ? tg.GetString() ?? "" : "",
                        });
                    }
                }

                StatusMessage = $"Loaded '{WorkflowName}' ({Nodes.Count} nodes).";
                Logger.Info($"WorkflowEditor: Loaded '{WorkflowName}' from {dlg.FileName}");
            }
            catch (Exception ex)
            {
                Logger.Error("WorkflowEditorViewModel: LoadWorkflowAsync failed", ex);
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string GetDefaultLabel(WorkflowNodeType type) => type switch
        {
            WorkflowNodeType.Action    => "Action",
            WorkflowNodeType.Delay     => "Wait",
            WorkflowNodeType.Condition => "If",
            WorkflowNodeType.Loop      => "Repeat",
            _                          => "Step"
        };

        private static string GetDefaultParameter(WorkflowNodeType type) => type switch
        {
            WorkflowNodeType.Action    => "Ctrl+C",
            WorkflowNodeType.Delay     => "500",
            WorkflowNodeType.Condition => "true",
            WorkflowNodeType.Loop      => "3",
            _                          => ""
        };
    }
}
