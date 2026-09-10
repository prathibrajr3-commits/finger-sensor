using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AirGestureAI.Configuration;
using AirGestureAI.Plugins;
using AirGestureAI.WorkflowEngine;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Captures and applies session state to and from the live application services and WPF UI window.
    /// </summary>
    public sealed class SessionStateProvider : ISessionStateProvider
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionStateProvider"/> class.
        /// </summary>
        /// <param name="serviceProvider">The root service provider.</param>
        public SessionStateProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public Task<SessionState> CaptureCurrentStateAsync()
        {
            var config = (AppConfig)_serviceProvider.GetService(typeof(AppConfig))!;
            var pluginMgr = (PluginManager)_serviceProvider.GetService(typeof(PluginManager))!;
            var wfService = (WorkflowEngineService)_serviceProvider.GetService(typeof(WorkflowEngineService))!;

            var state = new SessionState
            {
                Version = "4.1",
                Timestamp = DateTime.UtcNow,
                SelectedCamera = config?.CameraIndex ?? 0,
                Theme = config != null && config.EnableHighContrast ? "HighContrast" : "Dark",
                ActiveProject = config?.ActiveModel ?? "DefaultProject",
                UserPreferences = new Dictionary<string, string>()
            };

            if (config != null)
            {
                state.UserPreferences["Language"] = config.Language ?? "en";
                state.UserPreferences["EnableAdaptiveLearning"] = config.EnableAdaptiveLearning.ToString();
                state.UserPreferences["EnablePerformanceMode"] = config.EnablePerformanceMode.ToString();
            }

            // Collect active plugins
            if (pluginMgr?.LoadResults != null)
            {
                state.LoadedPluginIds = pluginMgr.LoadResults
                    .Where(r => r.Success && r.Plugin != null)
                    .Select(r => r.Plugin!.GetType().Name)
                    .ToList();
            }

            // Collect workflows
            if (wfService?.Workflows != null)
            {
                int idx = 0;
                string? prevId = null;
                foreach (var wf in wfService.Workflows)
                {
                    foreach (var step in wf.Steps)
                    {
                        var nodeId = $"node_{idx}";
                        state.Workflow.Nodes.Add(new WorkflowNodeState
                        {
                            Id = nodeId,
                            Label = step.Action ?? string.Empty,
                            Parameter = step.Parameters ?? string.Empty,
                            NodeType = "Action",
                            CanvasX = 60 + idx * 180,
                            CanvasY = 120
                        });

                        if (prevId != null)
                        {
                            state.Workflow.Connections.Add(new WorkflowConnectionState
                            {
                                SourceId = prevId,
                                TargetId = nodeId
                            });
                        }
                        prevId = nodeId;
                        idx++;
                    }
                }
            }

            // Capture window layout bounds safely on UI thread dispatcher
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWin = Application.Current.MainWindow;
                    if (mainWin != null)
                    {
                        state.WindowLayout.X = mainWin.Left;
                        state.WindowLayout.Y = mainWin.Top;
                        state.WindowLayout.Width = mainWin.Width;
                        state.WindowLayout.Height = mainWin.Height;
                    }
                });
            }

            return Task.FromResult(state);
        }

        /// <inheritdoc />
        public Task ApplyStateAsync(SessionState state)
        {
            if (state == null) return Task.CompletedTask;

            var config = (AppConfig)_serviceProvider.GetService(typeof(AppConfig))!;
            var wfService = (WorkflowEngineService)_serviceProvider.GetService(typeof(WorkflowEngineService))!;

            // Apply camera
            if (config != null)
            {
                config.CameraIndex = state.SelectedCamera;

                if (state.UserPreferences != null)
                {
                    if (state.UserPreferences.TryGetValue("Language", out var lang))
                        config.Language = lang;
                    if (state.UserPreferences.TryGetValue("EnableAdaptiveLearning", out var al) && bool.TryParse(al, out var alb))
                        config.EnableAdaptiveLearning = alb;
                    if (state.UserPreferences.TryGetValue("EnablePerformanceMode", out var pm) && bool.TryParse(pm, out var pmb))
                        config.EnablePerformanceMode = pmb;
                }
            }

            // Apply workflows
            if (wfService != null)
            {
                wfService.ClearWorkflows();

                if (state.Workflow?.Nodes != null && state.Workflow.Nodes.Count > 0)
                {
                    var wf = new Workflow { Name = "Recovered Workflow", Trigger = "recovered" };
                    var orderedNodes = state.Workflow.Nodes.OrderBy(n => n.CanvasX).ToList();

                    foreach (var node in orderedNodes)
                    {
                        wf.Steps.Add(new WorkflowStep
                        {
                            Action = node.Label ?? "Action",
                            Target = "System",
                            Parameters = node.Parameter ?? string.Empty,
                            Status = "Pending"
                        });
                    }

                    wfService.AddWorkflow(wf);
                }
            }

            // Apply window layout safely on UI thread dispatcher
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var mainWin = Application.Current.MainWindow;
                    if (mainWin != null)
                    {
                        if (state.WindowLayout.Width >= 320) mainWin.Width = state.WindowLayout.Width;
                        if (state.WindowLayout.Height >= 240) mainWin.Height = state.WindowLayout.Height;
                        if (state.WindowLayout.X >= -20000 && state.WindowLayout.X <= 20000) mainWin.Left = state.WindowLayout.X;
                        if (state.WindowLayout.Y >= -20000 && state.WindowLayout.Y <= 20000) mainWin.Top = state.WindowLayout.Y;
                    }
                }));
            }

            return Task.CompletedTask;
        }
    }
}
